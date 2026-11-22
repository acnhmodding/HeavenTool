using HeavenTool.IO.FileFormats.BFRES;
using HeavenTool.Shaders;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Buffers.Binary;
using System.Text.RegularExpressions;
using NMatrix = System.Numerics.Matrix4x4;

namespace HeavenTool.Rendering.BFRES;

public sealed partial class BfresRenderer
{
    private sealed record NativeProgramGpu(int Program, TranslatedShader Source, int VertexConstants, int FragmentConstants, List<(int Binding, string Name)> Blocks);
    // Buffers holds the blocks this mesh owns; Bindings resolves every block binding, shared ones included.
    private sealed record NativeMeshGpu(int Vao, int Vbo, NativeProgramGpu Program, Dictionary<string, int> Buffers,
        (int Binding, int Buffer)[] Bindings, List<(int Unit, int Texture, int Sampler, TextureTarget Target)> Samplers);
    // Constants and binding metadata belong to the archive program, even when two programs have identical code.
    private readonly Dictionary<(SwitchShadingModel Model, int Index), NativeProgramGpu> nativePrograms = [];
    // Archive programs with the same code share one GL program, as compiling is most of a scene's load time. Its block
    // bindings and texture units follow from the GLSL names alone, so they hold for every archive program using it, and
    // it outlives the scene so a reload or another model reuses it.
    private sealed class GlProgram(int id, int vertex, int fragment)
    {
        public int Id = id, Vertex = vertex, Fragment = fragment;
        public string? Error;
    }
    private readonly Dictionary<(string Vertex, string Fragment), GlProgram> glPrograms = [];
    // Maxwell has 18 constant buffers and 32 texture slots a stage; the fragment stage's come after the vertex stage's.
    private const int ConstantBuffers = 18, TextureSlots = 32;
    private bool? parallelCompile;
    private readonly Dictionary<BfresMesh, NativeMeshGpu> nativeMeshes = [];
    private readonly Dictionary<BfresMesh, NativeMeshGpu> nativeDepthMeshes = [];
    private readonly Dictionary<BfresMesh, NativeMeshGpu> nativeShadowMeshes = [];
    private readonly Dictionary<BfresMesh, NativeMeshGpu> nativeCubeMeshes = [];
    private IEnumerable<KeyValuePair<BfresMesh, NativeMeshGpu>> AllNativeMeshes =>
        nativeMeshes.Concat(nativeDepthMeshes).Concat(nativeShadowMeshes).Concat(nativeCubeMeshes);
    // Studio blocks depend only on the lighting preset, so every mesh shares one buffer per block name.
    private readonly Dictionary<string, int> studioBuffers = [];
    private IReadOnlyDictionary<BfresMesh, BfresMeshShaders> nativePending = new Dictionary<BfresMesh, BfresMeshShaders>();
    private bool nativeDirty;
    private int nativeContext, whiteTexture, blackTexture, shadowTexture, environmentTexture, projectionTexture, lightPrepassTexture;
    private AcnhLightingPreset lighting = AcnhLightingPreset.Day;
    private bool lightingDirty;
    public void SetLighting(AcnhLightingPreset value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lighting = value;
        lightingDirty = shadowDirty = probeDirty = true;
        Invalidate();
    }
    public int NativeDrawCount { get; private set; }
    public int FallbackDrawCount { get; private set; }
    public event Action? DrawStatusChanged;
    public IReadOnlyList<string> NativeShaderWarnings { get; private set; } = [];
    public event Action? NativeShaderWarningsChanged;

    private void SetNativeShaderWarnings(IReadOnlyList<string> warnings)
    {
        if (NativeShaderWarnings.SequenceEqual(warnings)) return;
        NativeShaderWarnings = warnings;
        NativeShaderWarningsChanged?.Invoke();
    }
    public void SetNativeShaders(IReadOnlyDictionary<BfresMesh, BfresMeshShaders> shaders)
    {
        nativePending = shaders;
        nativeDirty = true;
        shadowDirty = worldAoDirty = probeDirty = true;
        SetNativeShaderWarnings([]);
        Invalidate();
    }

    private void UploadNativeScene()
    {
        ReleaseNativeScene();
        var warnings = new List<string>();
        nativeContext = CreateBuffer(new byte[ParkUniforms.BufferSize]);
        // Every program starts compiling before any is waited on, so the driver can compile them side by side.
        EnableParallelCompile();
        foreach (var mesh in meshes)
            if (nativePending.TryGetValue(mesh.Source, out var shaders))
                foreach (var translated in new[] { shaders.Material, shaders.ZOnly, shaders.DepthShadow, shaders.CubeMap })
                    if (translated != null) StartGlProgram(translated);
        foreach (var mesh in meshes)
        {
            if (!nativePending.TryGetValue(mesh.Source, out var shaders)) continue;
            UploadNativePass(mesh, shaders.Material, nativeMeshes, "Material (using simplified shading)", warnings);
            UploadNativePass(mesh, shaders.ZOnly, nativeDepthMeshes, "Z-only", warnings);
            UploadNativePass(mesh, shaders.DepthShadow, nativeShadowMeshes, "Depth-shadow", warnings);
            UploadNativePass(mesh, shaders.CubeMap, nativeCubeMeshes, "Cube-map", warnings);
        }
        nativeDirty = false;
        SetNativeShaderWarnings(warnings);
    }

    private void UploadNativePass(GpuMesh mesh, TranslatedShader? translated, Dictionary<BfresMesh, NativeMeshGpu> target, string pass, List<string> warnings)
    {
        if (translated == null) return;
        try
        {
            target.Add(mesh.Source, CreateNativeMesh(mesh, translated));
        }
        catch (Exception ex)
        {
            warnings.Add($"{mesh.Source.Name}: {pass} shader could not be uploaded. {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"{mesh.Source.Name} ({pass}): {ex}");
        }
    }

    private void EnableParallelCompile()
    {
        if (parallelCompile != null) return;
        parallelCompile = false;
        for (int i = 0, count = GL.GetInteger(GetPName.NumExtensions); i < count; i++)
            if (GL.GetString(StringNameIndexed.Extensions, i) == "GL_ARB_parallel_shader_compile") parallelCompile = true;
        // As many compiler threads as the driver will use.
        if (parallelCompile == true) GL.Arb.MaxShaderCompilerThreads(uint.MaxValue);
    }

    // Compiles and links without asking for the result, which would wait for it.
    private GlProgram StartGlProgram(TranslatedShader translated)
    {
        var key = (translated.Vertex, translated.Fragment);
        if (glPrograms.TryGetValue(key, out var program)) return program;
        int vertex = GL.CreateShader(ShaderType.VertexShader), fragment = GL.CreateShader(ShaderType.FragmentShader), id = GL.CreateProgram();
        GL.ShaderSource(vertex, translated.Vertex);
        GL.ShaderSource(fragment, translated.Fragment);
        GL.CompileShader(vertex);
        GL.CompileShader(fragment);
        GL.AttachShader(id, vertex);
        GL.AttachShader(id, fragment);
        GL.LinkProgram(id);
        return glPrograms[key] = new(id, vertex, fragment);
    }

    // Waits for the program, and binds its blocks and texture units the first time. A failure is kept, so it is not compiled again.
    private int FinishGlProgram(TranslatedShader translated)
    {
        var program = StartGlProgram(translated);
        if (program.Vertex != 0)
        {
            int id = program.Id;
            GL.GetProgram(id, GetProgramParameterName.LinkStatus, out int linked);
            if (linked == 0)
                program.Error = string.Join(Environment.NewLine, new[] { GL.GetShaderInfoLog(program.Vertex), GL.GetShaderInfoLog(program.Fragment), GL.GetProgramInfoLog(id) }
                    .Where(log => !string.IsNullOrWhiteSpace(log)));
            GL.DetachShader(id, program.Vertex);
            GL.DetachShader(id, program.Fragment);
            GL.DeleteShader(program.Vertex);
            GL.DeleteShader(program.Fragment);
            program.Vertex = program.Fragment = 0;
            if (linked != 0)
            {
                for (int n = 0; n < ConstantBuffers; n++)
                {
                    BindBlock(id, $"vp_c{n}", n);
                    BindBlock(id, $"fp_c{n}", ConstantBuffers + n);
                }
                GL.UseProgram(id);
                // Screen-space lookups divide the pixel position by the emulator's resolution scale, which is 1 here.
                int scale = GL.GetUniformLocation(id, "fp_renderScale");
                if (scale >= 0) GL.Uniform1(scale, 1f);
                for (int slot = 0; slot < TextureSlots; slot++)
                    foreach (var prefix in new[] { "vp", "fp" })
                    {
                        int location = GL.GetUniformLocation(id, TextureName(prefix, slot));
                        if (location >= 0) GL.Uniform1(location, NativeTextureUnit(prefix, slot));
                    }
            }
        }
        if (program.Error != null) throw new InvalidOperationException(program.Error);
        return program.Id;
    }

    private static void BindBlock(int program, string name, int binding)
    {
        int index = GL.GetUniformBlockIndex(program, name);
        if (index >= 0) GL.UniformBlockBinding(program, index, binding);
    }

    private static string TextureName(string prefix, int slot) => $"{prefix}_tex_tcb_{slot * 2 + 8:X}";
    private static int NativeTextureUnit(string prefix, int slot) => (prefix == "vp" ? 0 : TextureSlots) + slot;
    // The game's blocks start at the stage's third constant buffer.
    private static int BlockBinding(string prefix, int location) => (prefix == "vp" ? 0 : ConstantBuffers) + location + 3;

    private NativeProgramGpu CreateNativeProgram(TranslatedShader translated)
    {
        var key = (translated.Model, translated.Binary.Index);
        if (nativePrograms.TryGetValue(key, out var cached)) return cached;
        GL.GetInteger(GetPName.MaxUniformBlockSize, out int maxBlockSize);
        if (maxBlockSize < ParkUniforms.BufferSize) throw new NotSupportedException("Native shader translation requires 64 KiB uniform blocks.");
        int id = FinishGlProgram(translated), vertexConstants = 0, fragmentConstants = 0;
        try
        {
            var blocks = new List<(int, string)>();
            foreach (var resource in translated.Binary.Buffers)
                foreach (var (prefix, location) in new[] { ("vp", resource.Vertex), ("fp", resource.Fragment) })
                    if (location >= 0 && GL.GetUniformBlockIndex(id, $"{prefix}_c{location + 3}") >= 0)
                        blocks.Add((BlockBinding(prefix, location), resource.Name));
            vertexConstants = CreateBuffer(ParkUniforms.Constants(translated.Binary.Vertex!));
            fragmentConstants = CreateBuffer(ParkUniforms.Constants(translated.Binary.Fragment!));
            var gpu = new NativeProgramGpu(id, translated, vertexConstants, fragmentConstants, blocks);
            nativePrograms.Add(key, gpu);
            vertexConstants = fragmentConstants = 0;
            return gpu;
        }
        finally
        {
            if (vertexConstants != 0) GL.DeleteBuffer(vertexConstants);
            if (fragmentConstants != 0) GL.DeleteBuffer(fragmentConstants);
        }
    }

    private NativeMeshGpu CreateNativeMesh(GpuMesh mesh, TranslatedShader translated)
    {
        var gpu = CreateNativeProgram(translated);
        int vao = GL.GenVertexArray(), vbo = GL.GenBuffer();
        var buffers = new Dictionary<string, int>();
        var samplers = new List<(int, int, int, TextureTarget)>();
        try
        {
            var attrs = translated.Model.Attributes.Where(x => x.Location >= 0).ToArray();
            int count = mesh.Source.Positions.Length, stride = attrs.Length * 16;
            var vertices = new float[checked(count * attrs.Length * 4)];
            for (int a = 0; a < attrs.Length; a++)
            {
                string sourceName = mesh.Source.Material.AttributeAssignments.TryGetValue(attrs[a].Name, out var assigned) ? assigned : attrs[a].Name;
                mesh.Source.NativeAttributes.TryGetValue(sourceName, out var values);
                for (int i = 0; i < count; i++)
                {
                    System.Numerics.Vector4 value = values.Length > i ? values.Span[i]
                        : sourceName.StartsWith("_c") ? System.Numerics.Vector4.One
                        : sourceName == "_t0" ? new(1, 0, 0, 1) : default;
                    if (sourceName == "_p0") value.W = 1;
                    // Maxwell consumes integer bone indices as raw register bits, rather than numeric floats.
                    if (sourceName.StartsWith("_i", StringComparison.Ordinal))
                        value = new(BitConverter.Int32BitsToSingle((int)value.X), BitConverter.Int32BitsToSingle((int)value.Y),
                            BitConverter.Int32BitsToSingle((int)value.Z), BitConverter.Int32BitsToSingle((int)value.W));
                    int p = (i * attrs.Length + a) * 4;
                    vertices[p] = value.X; vertices[p + 1] = value.Y; vertices[p + 2] = value.Z; vertices[p + 3] = value.W;
                }
            }
            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * 4, vertices, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, mesh.IndexBuffer);
            for (int a = 0; a < attrs.Length; a++)
            {
                GL.EnableVertexAttribArray(attrs[a].Location);
                GL.VertexAttribPointer(attrs[a].Location, 4, VertexAttribPointerType.Float, false, stride, a * 16);
            }
            var bindings = new List<(int, int)> { (1, gpu.VertexConstants), (ConstantBuffers + 1, gpu.FragmentConstants) };
            foreach (var (binding, name) in gpu.Blocks)
            {
                if (name is not ("gsys_material" or "gsys_shape" or "gsys_skeleton"))
                {
                    if (name != "gsys_context" && !studioBuffers.ContainsKey(name)) studioBuffers[name] = CreateBuffer(StudioBlock(name));
                    bindings.Add((binding, name == "gsys_context" ? nativeContext : studioBuffers[name]));
                    continue;
                }
                byte[] bytes = name == "gsys_material" ? ParkUniforms.Material(mesh.Source.Material, translated.Model.UniformBlocks.Single(x => x.Name == name))
                    : name == "gsys_shape" ? ParkUniforms.Shape(mesh.Source) : ParkUniforms.Skeleton(mesh.Source);
                buffers[name] = CreateBuffer(bytes);
                bindings.Add((binding, buffers[name]));
            }
            GL.GetInteger(GetPName.MaxCombinedTextureImageUnits, out int maxUnits);
            foreach (var resource in translated.Binary.Samplers)
            {
                var materialSampler = mesh.Source.Material.Sampler(resource.Name);
                foreach (var (prefix, slot) in new[] { ("vp", resource.Vertex), ("fp", resource.Fragment) })
                {
                    if (slot < 0 || GL.GetUniformLocation(gpu.Program, TextureName(prefix, slot)) < 0) continue;
                    int unit = NativeTextureUnit(prefix, slot);
                    if (slot >= TextureSlots || unit >= maxUnits) throw new NotSupportedException("Native shader needs more texture units than this GPU supports.");
                    var target = SamplerTarget(prefix == "vp" ? translated.Vertex : translated.Fragment, TextureName(prefix, slot));
                    int texture = 0;
                    if (target == TextureTarget.Texture2DArray)
                        texture = DefaultNativeArrayTexture(resource.Name);
                    else
                    {
                        if (materialSampler?.Texture != null) texture = UploadTexture(materialSampler.Texture);
                        if (texture == 0) texture = DefaultNativeTexture(resource.Name);
                    }
                    int sampler = materialSampler != null ? CreateSampler(materialSampler) : 0;
                    samplers.Add((unit, texture, sampler, target));
                }
            }
            return new(vao, vbo, gpu, buffers, [.. bindings], samplers);
        }
        catch
        {
            GL.DeleteVertexArray(vao);
            GL.DeleteBuffer(vbo);
            foreach (int buffer in buffers.Values) GL.DeleteBuffer(buffer);
            foreach (var (_, _, sampler, _) in samplers) if (sampler != 0) GL.DeleteSampler(sampler);
            throw;
        }
    }

    private static int CreateBuffer(byte[] bytes)
    {
        int id = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.UniformBuffer, id);
        GL.BufferData(BufferTarget.UniformBuffer, bytes.Length, bytes, BufferUsageHint.DynamicDraw);
        return id;
    }

    private static NMatrix Numerics(Matrix4 m) => new(m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);
    
    private void UpdateNativeLighting()
    {
        if (!lightingDirty) return;
        foreach (var (name, id) in studioBuffers)
        {
            byte[] bytes = StudioBlock(name);
            GL.BindBuffer(BufferTarget.UniformBuffer, id);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, bytes.Length, bytes);
        }
        lightingDirty = false;
    }

    private void UpdateNativeContext(Matrix4 view, Matrix4 projection, float near, float far)
    {
        if (nativeContext == 0) return;
        byte[] bytes = ParkUniforms.Context(Numerics(view), Numerics(projection), near, far, new(width, height),
            Numerics(shadowMatrix), 1f / ShadowSize, lighting.ProjectionShadow);
        GL.BindBuffer(BufferTarget.UniformBuffer, nativeContext);
        GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, bytes.Length, bytes);
    }

    private void DrawSceneMesh(GpuMesh mesh)
    {
        if (!nativeMeshes.TryGetValue(mesh.Source, out var native))
        {
            GL.Disable(EnableCap.FramebufferSrgb);
            GL.UseProgram(program);
            DrawMaterialMesh(mesh);
            FallbackDrawCount++;
            return;
        }
        ApplyRasterState(mesh.Material.Profile);
        MatchPrepassedDepth(mesh);
        GL.Enable(EnableCap.FramebufferSrgb);
        DrawNativeMesh(mesh, native);
        NativeDrawCount++;
    }

    // A pass drawing a map, such as the shadow map, draws from its own view's context, and must not sample the shadow map it
    // may be writing.
    private void DrawNativeMesh(GpuMesh mesh, NativeMeshGpu native, int passContext = 0)
    {
        GL.UseProgram(native.Program.Program);
        foreach (var (binding, buffer) in native.Bindings)
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, binding, passContext != 0 && buffer == nativeContext ? passContext : buffer);
        foreach (var (unit, bound, sampler, target) in native.Samplers)
        {
            if (target != TextureTarget.Texture2D)
            {
                GL.ActiveTexture(TextureUnit.Texture0 + unit);
                GL.BindTexture(target, bound);
                GL.BindSampler(unit, sampler);
                continue;
            }
            // The environment probe's capture reads the shadow map, but not the camera's screen-space occlusion.
            int texture = passContext == shadowContext && bound == shadowMap ? DefaultNativeTexture("shadow")
                : passContext != 0 && passContext == probeContext && bound != 0 && bound == ssaoMaps[1] ? DefaultNativeTexture("white")
                : bound != 0 && bound == projectionTexture ? ProjectionTexture(bound)
                : EnvironmentTexture(bound, passContext);
            GL.ActiveTexture(TextureUnit.Texture0 + unit);
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.BindSampler(unit, sampler);
        }
        GL.BindVertexArray(native.Vao);
        GL.DrawElements(PrimitiveType.Triangles, mesh.Count, DrawElementsType.UnsignedInt, 0);
    }

    // The translator declares each texture with the type the Maxwell code samples it as.
    private static TextureTarget SamplerTarget(string glsl, string name) =>
        Regex.Match(glsl, $@"uniform\s+(\w+)\s+{name}\s*;").Groups[1].Value switch
        {
            "sampler2DArray" or "sampler2DArrayShadow" or "isampler2DArray" or "usampler2DArray" => TextureTarget.Texture2DArray,
            "samplerCube" or "samplerCubeShadow" => TextureTarget.TextureCubeMap,
            "sampler3D" => TextureTarget.Texture3D,
            _ => TextureTarget.Texture2D,
        };

    // gsys_light_prepass is the half-size, two-layer target ExtendsGBufferWaterDepth and ExtendsGBuffer draw into after
    // clearing it to 0. The studio has no field models drawn in those passes, so it holds only the clear.
    private int DefaultNativeArrayTexture(string name)
    {
        if (name != "gsys_light_prepass") return 0;
        if (lightPrepassTexture == 0)
        {
            lightPrepassTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, lightPrepassTexture);
            GL.TexImage3D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.Rgba16f, 1, 1, 2, 0, PixelFormat.Rgba, PixelType.Float, new float[8]);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        }
        return lightPrepassTexture;
    }

    private int DefaultNativeTexture(string name)
    {
        if (name == "gsys_color_buffer") return SceneColorMap();
        if (name == "gsys_normalized_linear_depth") return NormalizedLinearDepthMap();
        if (name == "gsys_cube_map")
        {
            if (environmentTexture == 0) environmentTexture = LoadStudioEnvironment();
            return environmentTexture;
        }
        if (name == "gsys_depth_shadow") return ShadowMap();
        // A white stand-in of its own, so a draw can swap in the lighting's projected shade.
        if (name == "gsys_projection0")
        {
            if (projectionTexture == 0)
            {
                projectionTexture = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, projectionTexture);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, new byte[] { 255, 255, 255, 255 });
                TextureParameters(TextureMinFilter.Linear);
            }
            return projectionTexture;
        }
        if (occlusion && name == "gsys_user2") return WorldAoMap();
        if (occlusion && name == "gsys_ssao") return SsaoMap();
        // Placeholder for the other shadow maps: its comparison always passes, which the game shaders read as lit.
        if (name.Contains("shadow"))
        {
            if (shadowTexture == 0)
            {
                shadowTexture = GL.GenTexture();
                GL.BindTexture(TextureTarget.Texture2D, shadowTexture);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32f, 1, 1, 0, PixelFormat.DepthComponent, PixelType.Float, new float[] { 1 });
                TextureParameters(TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRefToTexture);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);
            }
            return shadowTexture;
        }
        // The game resets both user samplers to Black2D: the ground color until FieldUnitMapDrawer supplies it, and the water's
        // wave map until WaveSimulation publishes it.
        bool black = name is "gsys_user0" or "gsys_user1" || name.Contains("color_buffer");
        int id = black ? blackTexture : whiteTexture;
        if (id == 0)
        {
            id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, id);
            byte[] bytes = black ? [0, 0, 0, 255] : [255, 255, 255, 255];
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, bytes);
            TextureParameters(TextureMinFilter.Linear);
            if (black) blackTexture = id; else whiteTexture = id;
        }
        return id;
    }

    // The lighting's projected shade, mipmapped and repeating; the white stand-in when there is none.
    private int ProjectionTexture(int standIn) =>
        lighting.ProjectionShadow is { } shade && UploadTexture(shade.Texture) is var texture and not 0 ? texture : standIn;

    private static void TextureParameters(TextureMinFilter filter)
    {
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)filter);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }

    private static int LoadStudioEnvironment()
    {
        using var stream = typeof(BfresRenderer).Assembly.GetManifestResourceStream("HeavenTool.Rendering.Resources.StudioEnvironment.dds") ?? throw new InvalidOperationException("Studio environment texture is missing.");
        using var data = new MemoryStream();
        stream.CopyTo(data);
        var bytes = data.ToArray();
        int width = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16)), height = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12)), mips = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28));
        if (!bytes.AsSpan(0, 4).SequenceEqual("DDS "u8) || width != 512 || height != 256 || mips != 5) throw new InvalidDataException("Unsupported studio DDS.");
        int id = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, id);
        int offset = 128;
        for (int i = 0; i < mips; i++)
        {
            int count = width * height * 4;
            GL.TexImage2D(TextureTarget.Texture2D, i, PixelInternalFormat.Rgba8, width, height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, bytes.AsSpan(offset, count).ToArray());
            offset += count;
            width = Math.Max(1, width / 2);
            height = Math.Max(1, height / 2);
        }
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, mips - 1);
        TextureParameters(TextureMinFilter.LinearMipmapLinear);
        return id;
    }

    private void ReleaseNativeScene()
    {
        foreach (var (_, mesh) in AllNativeMeshes)
        {
            GL.DeleteVertexArray(mesh.Vao);
            GL.DeleteBuffer(mesh.Vbo);
            foreach (int buffer in mesh.Buffers.Values) GL.DeleteBuffer(buffer);
            foreach (var (_, _, sampler, _) in mesh.Samplers) if (sampler != 0) GL.DeleteSampler(sampler);
        }
        nativeMeshes.Clear();
        nativeDepthMeshes.Clear();
        nativeShadowMeshes.Clear();
        nativeCubeMeshes.Clear();
        foreach (int buffer in studioBuffers.Values) GL.DeleteBuffer(buffer);
        studioBuffers.Clear();
        foreach (var p in nativePrograms.Values) { GL.DeleteBuffer(p.VertexConstants); GL.DeleteBuffer(p.FragmentConstants); }
        nativePrograms.Clear();
        if (nativeContext != 0) GL.DeleteBuffer(nativeContext);
        nativeContext = 0;
        foreach (int texture in new[] { whiteTexture, blackTexture, shadowTexture, environmentTexture, projectionTexture, lightPrepassTexture }) if (texture != 0) GL.DeleteTexture(texture);
        ReleaseShadows();
        ReleaseOcclusion();
        ReleaseNormalizedLinearDepth();
        ReleaseSceneColor();
        whiteTexture = blackTexture = shadowTexture = environmentTexture = projectionTexture = lightPrepassTexture = 0;
        nativeDirty = nativePending.Count > 0;
    }

    private void ReleaseNativePrograms()
    {
        foreach (var program in glPrograms.Values)
        {
            if (program.Vertex != 0) { GL.DeleteShader(program.Vertex); GL.DeleteShader(program.Fragment); }
            GL.DeleteProgram(program.Id);
        }
        glPrograms.Clear();
        parallelCompile = null;
    }
}

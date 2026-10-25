using HeavenTool.IO.FileFormats.BFRES;
using HeavenTool.Shaders;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Buffers.Binary;
using NMatrix = System.Numerics.Matrix4x4;

namespace HeavenTool.Forms.BFRES;

internal sealed partial class BfresViewport
{
    private sealed record NativeProgramGpu(int Program, TranslatedShader Source, int VertexConstants, int FragmentConstants, List<(int Binding, string Name)> Blocks);
    // Buffers holds the blocks this mesh owns; Bindings resolves every block binding, shared ones included.
    private sealed record NativeMeshGpu(int Vao, int Vbo, NativeProgramGpu Program, Dictionary<string, int> Buffers,
        (int Binding, int Buffer)[] Bindings, List<(int Texture, int Sampler)> Samplers);
    private readonly Dictionary<string, NativeProgramGpu> nativePrograms = [];
    private readonly Dictionary<BfresMesh, NativeMeshGpu> nativeMeshes = [];
    // Studio blocks depend only on the lighting preset, so every mesh shares one buffer per block name.
    private readonly Dictionary<string, int> studioBuffers = [];
    private IReadOnlyDictionary<BfresMesh, TranslatedShader> nativePending = new Dictionary<BfresMesh, TranslatedShader>();
    private bool nativeDirty;
    private int nativeContext, whiteTexture, blackTexture, shadowTexture, environmentTexture;
    private AcnhLightingPreset? lighting;
    private bool lightingDirty;
    public void SetLighting(AcnhLightingPreset? value) { lighting = value; lightingDirty = shadowDirty = true; Invalidate(); }
    public int NativeDrawCount { get; private set; }
    public int FallbackDrawCount { get; private set; }
    public int UnlitDrawCount { get; private set; }
    public event Action? DrawStatusChanged;
    public void SetNativeShaders(IReadOnlyDictionary<BfresMesh, TranslatedShader> shaders)
    {
        nativePending = shaders; nativeDirty = true; Invalidate();
    }

    private void UploadNativeScene()
    {
        ReleaseNativeScene();
        nativeContext = CreateBuffer(new byte[ParkUniforms.BufferSize]);
        foreach (var mesh in meshes)
        {
            if (!nativePending.TryGetValue(mesh.Source, out var translated)) continue;
            try { nativeMeshes.Add(mesh.Source, CreateNativeMesh(mesh, translated)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"{mesh.Source.Name}: {ex}"); }
        }
        nativeDirty = false;
    }

    private NativeProgramGpu CreateNativeProgram(TranslatedShader translated)
    {
        if (nativePrograms.TryGetValue(translated.Key, out var cached)) return cached;
        GL.GetInteger(GetPName.MaxUniformBlockSize, out int maxBlockSize);
        if (maxBlockSize < ParkUniforms.BufferSize) throw new NotSupportedException("Native shader translation requires 64 KiB uniform blocks.");
        int id = 0, vertexConstants = 0, fragmentConstants = 0;
        try
        {
            id = CreateProgram(translated.Vertex, translated.Fragment);
            var blocks = new List<(int, string)>();
            void Bind(string name, int binding)
            {
                int index = GL.GetUniformBlockIndex(id, name);
                if (index >= 0) GL.UniformBlockBinding(id, index, binding);
            }
            Bind("vp_c1", 0); Bind("fp_c1", 1);
            foreach (var resource in translated.Binary.Buffers)
            {
                int binding = blocks.Count + 2;
                bool used = false;
                foreach (var (prefix, location) in new[] { ("vp", resource.Vertex), ("fp", resource.Fragment) })
                {
                    if (location < 0) continue;
                    string name = $"{prefix}_c{location + 3}";
                    int index = GL.GetUniformBlockIndex(id, name);
                    if (index < 0) continue;
                    GL.UniformBlockBinding(id, index, binding); used = true;
                }
                if (used) blocks.Add((binding, resource.Name));
            }
            vertexConstants = CreateBuffer(ParkUniforms.Constants(translated.Binary.Vertex!));
            fragmentConstants = CreateBuffer(ParkUniforms.Constants(translated.Binary.Fragment!));
            var gpu = new NativeProgramGpu(id, translated, vertexConstants, fragmentConstants, blocks);
            nativePrograms.Add(translated.Key, gpu);
            id = 0;
            vertexConstants = fragmentConstants = 0;
            return gpu;
        }
        finally
        {
            if (id != 0) GL.DeleteProgram(id);
            if (vertexConstants != 0) GL.DeleteBuffer(vertexConstants); if (fragmentConstants != 0) GL.DeleteBuffer(fragmentConstants);
        }
    }

    private NativeMeshGpu CreateNativeMesh(GpuMesh mesh, TranslatedShader translated)
    {
        var gpu = CreateNativeProgram(translated);
        int vao = GL.GenVertexArray(), vbo = GL.GenBuffer();
        var buffers = new Dictionary<string, int>();
        var samplers = new List<(int, int)>();
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
            GL.BindVertexArray(vao); GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * 4, vertices, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, mesh.IndexBuffer);
            for (int a = 0; a < attrs.Length; a++)
            {
                GL.EnableVertexAttribArray(attrs[a].Location);
                GL.VertexAttribPointer(attrs[a].Location, 4, VertexAttribPointerType.Float, false, stride, a * 16);
            }
            var bindings = new List<(int, int)> { (0, gpu.VertexConstants), (1, gpu.FragmentConstants) };
            foreach (var (binding, name) in gpu.Blocks)
            {
                if (name is not ("gsys_material" or "gsys_shape" or "gsys_skeleton"))
                {
                    if (name != "gsys_context" && !studioBuffers.ContainsKey(name)) studioBuffers[name] = CreateBuffer(ParkUniforms.StudioBlock(name, lighting));
                    bindings.Add((binding, name == "gsys_context" ? nativeContext : studioBuffers[name]));
                    continue;
                }
                byte[] bytes = name == "gsys_material" ? ParkUniforms.Material(mesh.Source.Material, translated.Model.UniformBlocks.Single(x => x.Name == name))
                    : name == "gsys_shape" ? ParkUniforms.Shape(mesh.Source) : ParkUniforms.Skeleton(mesh.Source);
                buffers[name] = CreateBuffer(bytes);
                bindings.Add((binding, buffers[name]));
            }
            GL.UseProgram(gpu.Program);
            foreach (var resource in translated.Binary.Samplers)
            {
                var materialSampler = mesh.Source.Material.Sampler(resource.Name);
                foreach (var (prefix, slot) in new[] { ("vp", resource.Vertex), ("fp", resource.Fragment) })
                {
                    if (slot < 0) continue;
                    int location = GL.GetUniformLocation(gpu.Program, $"{prefix}_tex_tcb_{slot * 2 + 8:X}");
                    if (location < 0) continue;
                    int texture = materialSampler?.Texture != null ? UploadTexture(materialSampler.Texture) : 0;
                    if (texture == 0) texture = DefaultNativeTexture(resource.Name);
                    int sampler = materialSampler != null ? CreateSampler(materialSampler) : 0;
                    // The unit order depends only on the program, so meshes sharing it agree on this value.
                    GL.Uniform1(location, samplers.Count);
                    samplers.Add((texture, sampler));
                }
            }
            GL.GetInteger(GetPName.MaxCombinedTextureImageUnits, out int maxUnits);
            if (samplers.Count > maxUnits) throw new NotSupportedException("Native shader needs more texture units than this GPU supports.");
            return new(vao, vbo, gpu, buffers, [.. bindings], samplers);
        }
        catch
        {
            GL.DeleteVertexArray(vao); GL.DeleteBuffer(vbo);
            foreach (int buffer in buffers.Values) GL.DeleteBuffer(buffer);
            foreach (var (_, sampler) in samplers) if (sampler != 0) GL.DeleteSampler(sampler);
            throw;
        }
    }

    private static int CreateBuffer(byte[] bytes)
    {
        int id = GL.GenBuffer(); GL.BindBuffer(BufferTarget.UniformBuffer, id);
        GL.BufferData(BufferTarget.UniformBuffer, bytes.Length, bytes, BufferUsageHint.DynamicDraw);
        return id;
    }

    private static NMatrix Numerics(Matrix4 m) => new(m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);
    
    private void UpdateNativeLighting()
    {
        if (!lightingDirty) return;
        foreach (var (name, id) in studioBuffers)
        {
            byte[] bytes = ParkUniforms.StudioBlock(name, lighting);
            GL.BindBuffer(BufferTarget.UniformBuffer, id);
            GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, bytes.Length, bytes);
        }
        lightingDirty = false;
    }

    private void UpdateNativeContext(Matrix4 view, Matrix4 projection, float near, float far)
    {
        if (nativeContext == 0) return;
        byte[] bytes = ParkUniforms.Context(Numerics(view), Numerics(projection), near, far, ClientSize.Width / (float)ClientSize.Height,
            Numerics(shadowMatrix), 1f / ShadowSize);
        GL.BindBuffer(BufferTarget.UniformBuffer, nativeContext);
        GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, bytes.Length, bytes);
    }

    private void DrawSceneMesh(GpuMesh mesh)
    {
        if (lighting == null || !nativeMeshes.TryGetValue(mesh.Source, out var native))
        {
            GL.Disable(EnableCap.FramebufferSrgb);
            GL.UseProgram(program);
            DrawMaterialMesh(mesh);
            if (lighting == null) UnlitDrawCount++; else FallbackDrawCount++;
            return;
        }
        ApplyRasterState(mesh.Material.Profile);
        GL.Enable(EnableCap.FramebufferSrgb);
        DrawNativeMesh(mesh, native, false);
        NativeDrawCount++;
    }

    // The shadow pass draws from the light's context and must not sample the map it is writing.
    private void DrawNativeMesh(GpuMesh mesh, NativeMeshGpu native, bool shadowPass)
    {
        GL.UseProgram(native.Program.Program);
        foreach (var (binding, buffer) in native.Bindings)
            GL.BindBufferBase(BufferRangeTarget.UniformBuffer, binding, shadowPass && buffer == nativeContext ? shadowContext : buffer);
        for (int i = 0; i < native.Samplers.Count; i++)
        {
            var (texture, sampler) = native.Samplers[i];
            if (shadowPass && texture == shadowMap) texture = DefaultNativeTexture("shadow");
            GL.ActiveTexture(TextureUnit.Texture0 + i); GL.BindTexture(TextureTarget.Texture2D, texture); GL.BindSampler(i, sampler);
        }
        GL.BindVertexArray(native.Vao);
        GL.DrawElements(PrimitiveType.Triangles, mesh.Count, DrawElementsType.UnsignedInt, 0);
    }

    private int DefaultNativeTexture(string name)
    {
        if (name == "gsys_cube_map")
        {
            if (environmentTexture == 0) environmentTexture = LoadStudioEnvironment();
            return environmentTexture;
        }
        if (name == "gsys_depth_shadow") return ShadowMap();
        // Placeholder for the other shadow maps: its comparison always passes, which the game shaders read as lit.
        if (name.Contains("shadow"))
        {
            if (shadowTexture == 0)
            {
                shadowTexture = GL.GenTexture(); GL.BindTexture(TextureTarget.Texture2D, shadowTexture);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32f, 1, 1, 0, PixelFormat.DepthComponent, PixelType.Float, new float[] { 1 });
                TextureParameters(TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRefToTexture);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);
            }
            return shadowTexture;
        }
        // A white projection texture casts no projected shadow.
        bool black = name.Contains("color_buffer");
        int id = black ? blackTexture : whiteTexture;
        if (id == 0)
        {
            id = GL.GenTexture(); GL.BindTexture(TextureTarget.Texture2D, id);
            byte[] bytes = black ? [0, 0, 0, 255] : [255, 255, 255, 255];
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, bytes);
            TextureParameters(TextureMinFilter.Linear);
            if (black) blackTexture = id; else whiteTexture = id;
        }
        return id;
    }

    private static void TextureParameters(TextureMinFilter filter)
    {
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)filter);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }

    private static int LoadStudioEnvironment()
    {
        using var stream = typeof(BfresViewport).Assembly.GetManifestResourceStream("HeavenTool.Resources.BFRES.StudioEnvironment.dds") ?? throw new InvalidOperationException("Studio environment texture is missing.");
        using var data = new MemoryStream(); stream.CopyTo(data); var bytes = data.ToArray();
        int width = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16)), height = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12)), mips = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28));
        if (!bytes.AsSpan(0, 4).SequenceEqual("DDS "u8) || width != 512 || height != 256 || mips != 5) throw new InvalidDataException("Unsupported studio DDS.");
        int id = GL.GenTexture(); GL.BindTexture(TextureTarget.Texture2D, id);
        int offset = 128;
        for (int i = 0; i < mips; i++)
        {
            int count = width * height * 4;
            GL.TexImage2D(TextureTarget.Texture2D, i, PixelInternalFormat.Rgba8, width, height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, bytes.AsSpan(offset, count).ToArray());
            offset += count; width = Math.Max(1, width / 2); height = Math.Max(1, height / 2);
        }
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, mips - 1);
        TextureParameters(TextureMinFilter.LinearMipmapLinear);
        return id;
    }

    private void ReleaseNativeScene()
    {
        foreach (var mesh in nativeMeshes.Values)
        {
            GL.DeleteVertexArray(mesh.Vao); GL.DeleteBuffer(mesh.Vbo);
            foreach (int buffer in mesh.Buffers.Values) GL.DeleteBuffer(buffer);
            foreach (var (_, sampler) in mesh.Samplers) if (sampler != 0) GL.DeleteSampler(sampler);
        }
        nativeMeshes.Clear();
        foreach (int buffer in studioBuffers.Values) GL.DeleteBuffer(buffer);
        studioBuffers.Clear();
        foreach (var p in nativePrograms.Values) { GL.DeleteProgram(p.Program); GL.DeleteBuffer(p.VertexConstants); GL.DeleteBuffer(p.FragmentConstants); }
        nativePrograms.Clear();
        if (nativeContext != 0) GL.DeleteBuffer(nativeContext);
        nativeContext = 0;
        foreach (int texture in new[] { whiteTexture, blackTexture, shadowTexture, environmentTexture }) if (texture != 0) GL.DeleteTexture(texture);
        ReleaseShadows();
        whiteTexture = blackTexture = shadowTexture = environmentTexture = 0;
        nativeDirty = nativePending.Count > 0;
    }
}

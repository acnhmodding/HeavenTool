using HeavenTool.IO.FileFormats.BFRES;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace HeavenTool.Rendering.BFRES;

/// <summary>
/// BFRES preview renderer shared by desktop hosts. All methods are called on the host UI thread.
/// Only Render and ReleaseGraphics use OpenGL; the host must make its context current for them.
/// </summary>
public sealed partial class BfresRenderer
{
    public sealed record PreparedMesh(BfresMesh Source, float[] Vertices, Vector3 Center);
    public sealed record PreparedScene(IReadOnlyList<PreparedMesh> Meshes, Vector3 Center, float Radius);
    private sealed record GpuMesh(BfresMesh Source, int Vao, int VertexBuffer, int IndexBuffer, int Count, MaterialGpu Material, Vector3 Center);
    private readonly List<GpuMesh> meshes = [];
    private readonly Dictionary<BntxTexture, int> textures = [];
    private readonly Dictionary<BfresMaterial, MaterialGpu> materials = [];
    private readonly List<GpuMesh> transparent = [];
    private readonly System.Comparison<GpuMesh> transparentOrder;
    private Vector3 cameraPosition;
    private PreparedScene pending = new([], Vector3.Zero, 1);
    private readonly HashSet<BfresMesh> hidden = [];
    private bool dirty = true;
    private int program, matrixUniform;
    private Vector3 center;
    private float radius = 1, distance = 3, yaw = .6f, pitch = .3f;
    private (float Radius, float MinimumDistance)? pendingFrame;
    private bool wireframe, orthographic, showGround;
    private float roll;
    private BfresMesh? selectedMesh;
    private int width, height, outputFramebuffer, outputColor, outputDepth;
    private float renderScale = 1;
    private Vector2? pendingBonePick;
    public event Action? Invalidated;

    private void Invalidate() => Invalidated?.Invoke();

    public BfresRenderer() => transparentOrder = CompareTransparent;

    public static PreparedScene PrepareScene(IReadOnlyList<BfresMesh> scene, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var prepared = new List<PreparedMesh>(scene.Count);
        Vector3 min = new(float.MaxValue), max = new(float.MinValue);
        bool any = false;
        foreach (var source in scene)
        {
            var positions = source.Positions.Span;
            var normals = source.Normals.Span;
            var uv = source.TexCoords.Span;
            var vertices = new float[checked(positions.Length * 8)];
            Vector3 meshMin = new(float.MaxValue), meshMax = new(float.MinValue);
            for (int i = 0; i < positions.Length; i++)
            {
                if ((i & 4095) == 0) cancellation.ThrowIfCancellationRequested();
                var p = positions[i];
                min = Vector3.ComponentMin(min, new(p.X, p.Y, p.Z));
                max = Vector3.ComponentMax(max, new(p.X, p.Y, p.Z));
                any = true;
                meshMin = Vector3.ComponentMin(meshMin, new(p.X, p.Y, p.Z));
                meshMax = Vector3.ComponentMax(meshMax, new(p.X, p.Y, p.Z));
                vertices[i * 8] = p.X; vertices[i * 8 + 1] = p.Y; vertices[i * 8 + 2] = p.Z;
                vertices[i * 8 + 3] = normals[i].X; vertices[i * 8 + 4] = normals[i].Y; vertices[i * 8 + 5] = normals[i].Z;
                if (uv.Length > i) { vertices[i * 8 + 6] = uv[i].X; vertices[i * 8 + 7] = uv[i].Y; }
            }
            prepared.Add(new(source, vertices, positions.Length > 0 ? (meshMin + meshMax) / 2 : Vector3.Zero));
        }
        return new(prepared, any ? (min + max) / 2 : Vector3.Zero, any ? Math.Max((max - min).Length / 2, .001f) : 1);
    }

    public void SetScene(PreparedScene scene)
    {
        pending = scene;
        selectedMesh = null;
        hidden.Clear();
        dirty = true;
        // Translated programs belong to the previous document, even when no new archive is loaded.
        nativePending = new Dictionary<BfresMesh, HeavenTool.Shaders.TranslatedShader>();
        nativeDirty = true;
        SetNativeShaderWarnings([]);
        FrameScene();
    }

    public void SetVisible(BfresMesh mesh, bool visible)
    {
        if (visible) hidden.Remove(mesh); else hidden.Add(mesh);
        shadowDirty = true;
        Invalidate();
    }

    [System.ComponentModel.DefaultValue(false)]
    public bool Wireframe
    {
        get => wireframe;
        set { wireframe = value; Invalidate(); }
    }

    public bool ShowGround
    {
        get => showGround;
        set { if (showGround != value) { showGround = value; Invalidate(); } }
    }

    public void FrameScene()
    {
        center = pending.Center;
        radius = pending.Radius;
        FrameBounds(radius, radius * 3);
        Invalidate();
    }

    private void FrameBounds(float boundsRadius, float minimumDistance)
    {
        // A scene can be opened before the host has measured its viewport.
        pendingFrame = (boundsRadius, minimumDistance);
        distance = minimumDistance;
    }

    private void ApplyPendingFrame()
    {
        if (pendingFrame is not { } frame) return;
        float tangent = MathF.Tan(MathHelper.PiOver4 / 2) * Math.Min(1, width / (float)height);
        float fit = orthographic ? frame.Radius / tangent : frame.Radius / MathF.Sin(MathF.Atan(tangent));
        distance = Math.Max(frame.MinimumDistance, fit * 1.1f);
        pendingFrame = null;
    }

    /// <summary>Renders into a host framebuffer, in physical pixels. Never swaps the host's buffers.</summary>
    public void Render(int pixelWidth, int pixelHeight, int framebuffer, float scale = 1)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0) return;
        width = pixelWidth;
        height = pixelHeight;
        ApplyPendingFrame();
        renderScale = scale;
        try
        {
            GL.Disable(EnableCap.ScissorTest);
            GL.Disable(EnableCap.StencilTest);
            GL.ColorMask(true, true, true, true);
            EnsureOutputTarget();
            if (program == 0) InitializeRenderer();
            if (dirty) UploadScene();
            if (nativeDirty) UploadNativeScene();
            UpdateMaterials();
            UpdateNativeLighting();
            RenderShadowMap();
            BeginLightingFrame();
            GL.Viewport(0, 0, width, height);
            GL.Disable(EnableCap.FramebufferSrgb);
            GL.ClearColor(0, 0, 0, 0);
            GL.DepthMask(true);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.PolygonMode(TriangleFace.FrontAndBack, wireframe ? PolygonMode.Line : PolygonMode.Fill);
            Vector3 direction = new(MathF.Cos(pitch) * MathF.Sin(yaw), MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Cos(yaw));
            cameraPosition = center + direction * distance;
            var view = Matrix4.LookAt(cameraPosition, center, CameraUp());
            float aspect = width / (float)height;
            float near = Math.Max(radius * .001f, distance - radius * 1.5f), far = distance + radius * 4;
            if (showGround)
            {
                // The model's tight clipping range can cut the ground into horizontal bands.
                // Cover the grid's visible radius while retaining a useful depth precision.
                near = Math.Min(near, Math.Max(radius * .001f, distance * .01f));
                float groundRadius = distance * GroundFadeDistance;
                far = Math.Max(far, distance + MathF.Sqrt(center.Y * center.Y + groundRadius * groundRadius));
            }
            float halfHeight = distance * MathF.Tan(MathHelper.PiOver4 / 2);
            var projection = orthographic
                ? Matrix4.CreateOrthographicOffCenter(-halfHeight * aspect, halfHeight * aspect, -halfHeight, halfHeight, near, far)
                : Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect, near, far);
            var matrix = view * projection;
            UpdateNativeContext(view, projection, near, far);
            SetFrameUniforms(matrix);
            transparent.Clear();
            NativeDrawCount = 0; FallbackDrawCount = 0;
            foreach (var mesh in meshes)
            {
                if (hidden.Contains(mesh.Source)) continue;
                if (mesh.Material.Profile.Translucent) transparent.Add(mesh);
                else DrawSceneMesh(mesh);
            }
            transparent.Sort(transparentOrder);
            foreach (var mesh in transparent) DrawSceneMesh(mesh);
            // Checked once per frame: querying after every draw stalls the driver.
            if (NativeDrawCount > 0 && GL.GetError() is var error && error != ErrorCode.NoError)
                throw new InvalidOperationException($"Native shader draw failed: {error}.");
            EndLightingFrame();
            if (showGround) DrawGround(matrix);
            DrawBones(matrix);
            GL.DepthMask(true);
            for (int i = 0; i < 5; i++) GL.BindSampler(i, 0);
            GL.BindVertexArray(0);
            // Picking reads the completed scene depth while the host's context is current.
            if (pendingBonePick is { } point)
            {
                pendingBonePick = null;
                PickBone(point);
            }
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, outputFramebuffer);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, framebuffer);
            GL.BlitFramebuffer(0, 0, width, height, 0, 0, width, height,
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
        }
        finally { GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer); }
        DrawStatusChanged?.Invoke();
    }

    private void InitializeRenderer()
    {
        const string vertex = """
            #version 330 core
            layout(location=0) in vec3 position;
            layout(location=1) in vec3 normal;
            layout(location=2) in vec2 texCoord;
            uniform mat4 viewProjection;
            out vec3 surfaceNormal;
            out vec2 uv;
            out vec3 worldPosition;
            void main() { gl_Position = viewProjection * vec4(position,1.0); surfaceNormal = normal; uv = texCoord; worldPosition = position; }
            """;
        program = CreateProgram(vertex, MaterialFragmentShader);
        matrixUniform = GL.GetUniformLocation(program, "viewProjection");
        GL.UseProgram(program);
        InitializeMaterialUniforms();
    }

    private static int CreateProgram(string vertex, string fragment)
    {
        int vs = 0, fs = 0, id = 0;
        try
        {
            vs = Compile(ShaderType.VertexShader, vertex); fs = Compile(ShaderType.FragmentShader, fragment);
            id = GL.CreateProgram(); GL.AttachShader(id, vs); GL.AttachShader(id, fs); GL.LinkProgram(id);
            GL.GetProgram(id, GetProgramParameterName.LinkStatus, out int linked);
            if (linked == 0) throw new InvalidOperationException(GL.GetProgramInfoLog(id));
            return id;
        }
        catch { if (id != 0) GL.DeleteProgram(id); throw; }
        finally { if (vs != 0) GL.DeleteShader(vs); if (fs != 0) GL.DeleteShader(fs); }
    }

    private static int Compile(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source); GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int compiled);
        if (compiled != 0) return shader;
        string log = GL.GetShaderInfoLog(shader);
        GL.DeleteShader(shader);
        throw new InvalidOperationException(log);
    }

    private unsafe void UploadScene()
    {
        ReleaseMeshes();
        foreach (var prepared in pending.Meshes)
        {
            var source = prepared.Source;
            var vertices = prepared.Vertices;
            var indices = source.Indices.Span;
            var material = UploadMaterial(source.Material);
            int vao = GL.GenVertexArray(), vbo = GL.GenBuffer(), ebo = GL.GenBuffer();
            meshes.Add(new(source, vao, vbo, ebo, indices.Length, material, prepared.Center));
            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
            fixed (uint* pointer = indices)
                GL.BufferData(BufferTarget.ElementArrayBuffer, indices.Length * sizeof(uint), (IntPtr)pointer, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0); GL.EnableVertexAttribArray(1); GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 32, 0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 32, 12);
            GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 32, 24);
        }
        dirty = false;
    }

    private unsafe int UploadTexture(BntxTexture texture)
    {
        if (textures.TryGetValue(texture, out int existing)) return existing;
        int channel = (int)(texture.Format >> 8), type = (int)(texture.Format & 255);
        bool srgb = type == 6;
        InternalFormat format = channel switch
        {
            0x1a => srgb ? InternalFormat.CompressedSrgbAlphaS3tcDxt1Ext : InternalFormat.CompressedRgbaS3tcDxt1Ext,
            0x1b => srgb ? InternalFormat.CompressedSrgbAlphaS3tcDxt3Ext : InternalFormat.CompressedRgbaS3tcDxt3Ext,
            0x1c => srgb ? InternalFormat.CompressedSrgbAlphaS3tcDxt5Ext : InternalFormat.CompressedRgbaS3tcDxt5Ext,
            0x1d => type == 2 ? InternalFormat.CompressedSignedRedRgtc1 : InternalFormat.CompressedRedRgtc1,
            0x1e => type == 2 ? InternalFormat.CompressedSignedRgRgtc2 : InternalFormat.CompressedRgRgtc2,
            0x20 => srgb ? InternalFormat.CompressedSrgbAlphaBptcUnorm : InternalFormat.CompressedRgbaBptcUnorm,
            _ => 0
        };
        if (channel >= 0x1a && format == 0) return 0;
        int id = GL.GenTexture();
        textures.Add(texture, id);
        GL.BindTexture(TextureTarget.Texture2D, id);
        var pixel = channel == 0x02 ? PixelFormat.Red : channel == 0x09 ? PixelFormat.Rg : PixelFormat.Rgba;
        var storage = channel == 0x02 ? PixelInternalFormat.R8 : channel == 0x09 ? PixelInternalFormat.Rg8 : srgb ? PixelInternalFormat.Srgb8Alpha8 : PixelInternalFormat.Rgba8;
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        for (int level = 0; level < texture.Mips.Count; level++)
        {
            int width = Math.Max(1, texture.Width >> level), height = Math.Max(1, texture.Height >> level);
            var mip = texture.Mips[level];
            fixed (byte* bytes = mip.Span)
            {
                if (channel >= 0x1a) GL.CompressedTexImage2D(TextureTarget.Texture2D, level, format, width, height, 0, mip.Length, (IntPtr)bytes);
                else GL.TexImage2D(TextureTarget.Texture2D, level, storage, width, height, 0, pixel, PixelType.UnsignedByte, (IntPtr)bytes);
            }
        }
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, texture.Mips.Count - 1);
        if (GL.GetError() != ErrorCode.NoError)
        {
            // Optional GPU compression support varies by driver. Keep geometry usable.
            GL.DeleteTexture(id); textures[texture] = 0; return 0;
        }
        static int Swizzle(byte source) => source switch { 0 => 0, 1 => 1, 2 => (int)All.Red, 3 => (int)All.Green, 4 => (int)All.Blue, 5 => (int)All.Alpha, _ => (int)All.Red };
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleR, Swizzle(texture.Channels[0]));
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleG, Swizzle(texture.Channels[1]));
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleB, Swizzle(texture.Channels[2]));
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleA, Swizzle(texture.Channels[3]));
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        return id;
    }

    private void ReleaseMeshes()
    {
        ReleaseNativeScene();
        foreach (var mesh in meshes)
        {
            GL.DeleteBuffer(mesh.VertexBuffer); GL.DeleteBuffer(mesh.IndexBuffer); GL.DeleteVertexArray(mesh.Vao);
        }
        meshes.Clear();
        transparent.Clear();
        foreach (var material in materials.Values) foreach (int sampler in material.Samplers) if (sampler != 0) GL.DeleteSampler(sampler);
        materials.Clear();
        materialVisibility.Clear();
        changedMaterials.Clear();
        foreach (int texture in textures.Values) if (texture != 0) GL.DeleteTexture(texture);
        textures.Clear();
    }

    public void Orbit(float dx, float dy)
    {
        yaw -= dx * .008f;
        pitch += dy * .008f;
        Invalidate();
    }

    public void Pan(float dx, float dy)
    {
        var right = CameraRight();
        var up = CameraUp();
        center += (-right * dx + up * dy) * distance * .0015f;
        Invalidate();
    }

    private Vector3 CameraUp()
    {
        var up = new Vector3(-MathF.Sin(pitch) * MathF.Sin(yaw), MathF.Cos(pitch), -MathF.Sin(pitch) * MathF.Cos(yaw));
        var right = new Vector3(MathF.Cos(yaw), 0, -MathF.Sin(yaw));
        return up * MathF.Cos(roll) + right * MathF.Sin(roll);
    }

    private Vector3 CameraRight()
    {
        var up = new Vector3(-MathF.Sin(pitch) * MathF.Sin(yaw), MathF.Cos(pitch), -MathF.Sin(pitch) * MathF.Cos(yaw));
        var right = new Vector3(MathF.Cos(yaw), 0, -MathF.Sin(yaw));
        return right * MathF.Cos(roll) - up * MathF.Sin(roll);
    }

    public void SetView(float yawRadians, float pitchRadians)
    {
        yaw = yawRadians;
        pitch = pitchRadians;
        roll = 0;
        orthographic = true;
        Invalidate();
    }

    public void OrbitStep(float horizontalRadians, float verticalRadians)
    {
        yaw += horizontalRadians;
        pitch += verticalRadians;
        Invalidate();
    }

    public void RollStep(float radians) { roll += radians; Invalidate(); }
    public void OppositeView() { yaw += MathF.PI; pitch = -pitch; Invalidate(); }
    public void ToggleProjection() { orthographic = !orthographic; Invalidate(); }
    public void SelectMesh(BfresMesh? mesh) => selectedMesh = mesh;

    private void FrameSelectedMesh()
    {
        if (selectedMesh == null || selectedMesh.Positions.IsEmpty) { FrameScene(); return; }
        Vector3 min = new(float.MaxValue), max = new(float.MinValue);
        foreach (var point in selectedMesh.Positions.Span)
        {
            var p = new Vector3(point.X, point.Y, point.Z);
            min = Vector3.ComponentMin(min, p);
            max = Vector3.ComponentMax(max, p);
        }
        center = (min + max) * .5f;
        FrameBounds((max - min).Length * .5f, Math.Max((max - min).Length * 1.5f, radius * .02f));
        Invalidate();
    }

    public void Zoom(float steps)
    {
        if (width > 0 && height > 0) ApplyPendingFrame();
        pendingFrame = null;
        distance = Math.Clamp(distance * MathF.Pow(.85f, steps), radius * .02f, radius * 100);
        Invalidate();
    }

    public void RequestBonePick(float x, float y)
    {
        pendingBonePick = new(x, y);
        Invalidate();
    }

    private void EnsureOutputTarget()
    {
        if (outputFramebuffer != 0 && outputWidth == width && outputHeight == height) return;
        ReleaseOutputTarget();
        outputWidth = width;
        outputHeight = height;
        outputFramebuffer = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, outputFramebuffer);
        outputColor = GL.GenRenderbuffer();
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, outputColor);
        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.Rgba8, width, height);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, outputColor);
        outputDepth = GL.GenRenderbuffer();
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, outputDepth);
        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, width, height);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, outputDepth);
        if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            throw new InvalidOperationException("The 3D preview framebuffer is incomplete.");
    }

    private int outputWidth, outputHeight;

    private void ReleaseOutputTarget()
    {
        if (outputFramebuffer != 0) GL.DeleteFramebuffer(outputFramebuffer);
        if (outputColor != 0) GL.DeleteRenderbuffer(outputColor);
        if (outputDepth != 0) GL.DeleteRenderbuffer(outputDepth);
        outputFramebuffer = outputColor = outputDepth = 0;
    }

    /// <summary>Releases GPU objects in the current context; keeps the scene for reattachment.</summary>
    public void ReleaseGraphics()
    {
        ReleaseMeshes();
        ReleaseLighting();
        ReleaseBones();
        ReleaseGround();
        ReleaseOutputTarget();
        if (program != 0) GL.DeleteProgram(program);
        program = 0;
        maxAnisotropy = 0;
        dirty = true;
    }
}

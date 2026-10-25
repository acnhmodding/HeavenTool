using HeavenTool.IO.FileFormats.BFRES;
using OpenTK.GLControl;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace HeavenTool.Forms.BFRES;

internal sealed partial class BfresViewport : GLControl
{
    internal sealed record PreparedMesh(BfresMesh Source, float[] Vertices, Vector3 Center);
    internal sealed record PreparedScene(IReadOnlyList<PreparedMesh> Meshes, Vector3 Center, float Radius);
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
    private Point previous;
    private bool wireframe;
    public event Action<Exception>? RenderFailed;
    private bool failed;

    public BfresViewport() : base(new GLControlSettings
    {
        APIVersion = new Version(4, 4), Profile = ContextProfile.Core,
        Flags = ContextFlags.ForwardCompatible, NumberOfSamples = Samples, SrgbCapable = true
    })
    {
        Dock = DockStyle.Fill;
        TabStop = true;
        BackColor = Color.FromArgb(28, 30, 35);
        transparentOrder = CompareTransparent;
    }

    public static PreparedScene PrepareScene(IReadOnlyList<BfresMesh> scene, CancellationToken cancellation)
    {
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
        hidden.Clear();
        dirty = true;
        FrameScene();
        Invalidate();
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

    public void FrameScene()
    {
        center = pending.Center;
        radius = pending.Radius;
        distance = radius * 3;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (DesignMode || failed || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        try
        {
            MakeCurrent();
            if (program == 0) InitializeRenderer();
            if (dirty) UploadScene();
            if (nativeDirty) UploadNativeScene();
            UpdateMaterials();
            UpdateNativeLighting();
            RenderShadowMap();
            BeginLightingFrame();
            GL.Viewport(0, 0, ClientSize.Width, ClientSize.Height);
            GL.Disable(EnableCap.FramebufferSrgb);
            if (lighting != null) GL.ClearColor(0, 0, 0, 0);
            else GL.ClearColor(.11f, .12f, .14f, 1);
            GL.DepthMask(true);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.CullFace);
            GL.PolygonMode(TriangleFace.FrontAndBack, wireframe ? PolygonMode.Line : PolygonMode.Fill);
            Vector3 direction = new(MathF.Cos(pitch) * MathF.Sin(yaw), MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Cos(yaw));
            cameraPosition = center + direction * distance;
            var view = Matrix4.LookAt(center + direction * distance, center, Vector3.UnitY);
            var projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4,
                ClientSize.Width / (float)ClientSize.Height, Math.Max(radius * .001f, distance - radius * 1.5f), distance + radius * 4);
            var matrix = view * projection;
            UpdateNativeContext(view, projection, Math.Max(radius * .001f, distance - radius * 1.5f), distance + radius * 4);
            SetFrameUniforms(matrix);
            transparent.Clear();
            NativeDrawCount = 0; FallbackDrawCount = 0; UnlitDrawCount = 0;
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
            DrawBones(matrix);
            GL.DepthMask(true);
            for (int i = 0; i < 5; i++) GL.BindSampler(i, 0);
            GL.BindVertexArray(0);
            SwapBuffers();
            DrawStatusChanged?.Invoke();
        }
        catch (Exception ex)
        {
            failed = true;
            RenderFailed?.Invoke(ex);
        }
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

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus(); previous = e.Location; Capture = true;
        boneMouseDown = e.Location;
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e); Capture = false;
        if (e.Button == MouseButtons.Left && Math.Abs(e.X - boneMouseDown.X) + Math.Abs(e.Y - boneMouseDown.Y) <= 4) PickBone(e.Location);
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        float dx = e.X - previous.X, dy = e.Y - previous.Y;
        if (e.Button == MouseButtons.Left)
        {
            yaw -= dx * .008f; pitch = Math.Clamp(pitch + dy * .008f, -1.5f, 1.5f); Invalidate();
        }
        else if (e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle)
        {
            var right = new Vector3(MathF.Cos(yaw), 0, -MathF.Sin(yaw));
            var up = new Vector3(-MathF.Sin(pitch) * MathF.Sin(yaw), MathF.Cos(pitch), -MathF.Sin(pitch) * MathF.Cos(yaw));
            center += (-right * dx + up * dy) * distance * .0015f; Invalidate();
        }
        previous = e.Location;
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        distance = Math.Clamp(distance * MathF.Pow(.85f, e.Delta / 120f), radius * .02f, radius * 100);
        Invalidate();
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.F && !e.Handled) { FrameSelection(); e.Handled = true; }
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (program != 0 || meshes.Count > 0 || boneProgram != 0)
        {
            MakeCurrent(); ReleaseMeshes(); ReleaseLighting(); ReleaseBones();
            if (program != 0) GL.DeleteProgram(program);
            program = 0;
        }
        dirty = true;
        base.OnHandleDestroyed(e);
    }
}

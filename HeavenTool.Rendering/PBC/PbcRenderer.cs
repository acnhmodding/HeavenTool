using HeavenTool.Rendering.BFRES;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace HeavenTool.Rendering.PBC;

/// <summary>
/// Draws a <see cref="PbcMesh"/> with simple directional shading so slopes and cliffs read clearly.
/// All methods are called on the host UI thread; only Render and ReleaseGraphics use OpenGL.
/// </summary>
public sealed class PbcRenderer : IViewportRenderer
{
    private const int Samples = 4;
    private PbcMesh mesh = PbcMesh.Empty;
    private float[] highlight = [], hover = [];
    private bool meshDirty = true, highlightDirty = true, hoverDirty = true, showGrid = true, orthographic;
    private Vector3 center;
    private float radius = 1, distance = 3, yaw = .6f, pitch = .7f, roll;
    private (float Radius, float MinimumDistance)? pendingFrame;
    private int width, height;
    private int surfaceProgram, surfaceMatrix, lineProgram, lineMatrix, lineColor;
    private int surfaceVao, surfaceBuffer, surfaceCount, gridVao, gridBuffer, gridCount, highlightVao, highlightBuffer, highlightCount, hoverVao, hoverBuffer, hoverCount;
    private int sampleFramebuffer, sampleColor, sampleDepth, resolveFramebuffer, resolveColor, targetWidth, targetHeight;
    private int surfaceOpacityUniform, compositeProgram, compositeVao;
    private Matrix4 viewProjection = Matrix4.Identity;
    private BfresRenderer? model;
    private readonly List<BfresRenderer> retiredModels = [];
    private bool showModel = true;
    private float surfaceOpacity = .5f;
    public event Action? Invalidated;
    /// <summary>Raised when the model fails to draw; the model is dropped and the PBC keeps drawing.</summary>
    public event Action<Exception>? ModelFailed;

    /// <summary>
    /// The model drawn under the PBC, in its own coordinates. <see cref="ModelOffset"/> is where the PBC's origin lies in them.
    /// </summary>
    public BfresRenderer? Model
    {
        get => model;
        set
        {
            if (model == value) return;
            if (model != null)
            {
                model.Invalidated -= Invalidate;
                // Its GPU objects can only go while the host's context is current.
                retiredModels.Add(model);
            }
            model = value;
            if (model != null) model.Invalidated += Invalidate;
            Invalidate();
        }
    }

    public Vector3 ModelOffset { get; set { field = value; Invalidate(); } }

    public bool ShowModel
    {
        get => showModel;
        set { if (showModel != value) { showModel = value; Invalidate(); } }
    }

    /// <summary>How opaque the PBC surface is over a shown model, from 0 to 1.</summary>
    public float SurfaceOpacity
    {
        get => surfaceOpacity;
        set { value = Math.Clamp(value, 0, 1); if (surfaceOpacity != value) { surfaceOpacity = value; Invalidate(); } }
    }

    private bool DrawsModel => model != null && showModel;

    private void Invalidate() => Invalidated?.Invoke();

    public bool ShowGrid
    {
        get => showGrid;
        set { if (showGrid != value) { showGrid = value; Invalidate(); } }
    }

    public void SetMesh(PbcMesh value, bool keepCamera)
    {
        mesh = value;
        meshDirty = true;
        if (keepCamera && !value.IsEmpty) Invalidate();
        else FrameScene();
    }

    /// <summary>Line-list positions drawn on top of everything, for the selected triangle.</summary>
    public void SetHighlight(float[] lines)
    {
        highlight = lines;
        highlightDirty = true;
        Invalidate();
    }

    /// <summary>Line-list positions for the brush under the cursor, drawn over the surface.</summary>
    public void SetHover(float[] lines)
    {
        if (lines.Length == 0 && hover.Length == 0) return;
        hover = lines;
        hoverDirty = true;
        Invalidate();
    }

    public void FrameScene()
    {
        if (mesh.IsEmpty)
        {
            center = Vector3.Zero;
            radius = 1;
        }
        else
        {
            center = (mesh.Min + mesh.Max) * .5f;
            radius = Math.Max(1, (mesh.Max - mesh.Min).Length * .5f);
        }
        FrameBounds(radius, radius * 1.2f);
        Invalidate();
    }

    public void FrameSelection()
    {
        if (highlight.Length < 3) { FrameScene(); return; }
        Vector3 min = new(float.MaxValue), max = new(float.MinValue);
        for (int i = 0; i + 2 < highlight.Length; i += 3)
        {
            var p = new Vector3(highlight[i], highlight[i + 1], highlight[i + 2]);
            min = Vector3.ComponentMin(min, p);
            max = Vector3.ComponentMax(max, p);
        }
        center = (min + max) * .5f;
        // Show a few tiles around the triangle rather than filling the view with it.
        float size = Math.Max((max - min).Length, PbcGeometry.TileSize) * 4;
        FrameBounds(size * .5f, size);
        Invalidate();
    }

    private void FrameBounds(float boundsRadius, float minimumDistance)
    {
        // A file can be opened before the host has measured its viewport.
        pendingFrame = (boundsRadius, minimumDistance);
        distance = minimumDistance;
    }

    private void ApplyPendingFrame()
    {
        if (pendingFrame is not { } frame) return;
        float tangent = MathF.Tan(MathHelper.PiOver4 / 2) * Math.Min(1, width / (float)height);
        float fit = orthographic ? frame.Radius / tangent : frame.Radius / MathF.Sin(MathF.Atan(tangent));
        distance = Math.Max(frame.MinimumDistance, fit * 1.05f);
        pendingFrame = null;
    }

    public void Orbit(float dx, float dy)
    {
        yaw -= dx * .008f;
        pitch += dy * .008f;
        Invalidate();
    }

    public void Pan(float dx, float dy)
    {
        var (right, up) = CameraAxes();
        center += (-right * dx + up * dy) * distance * .0015f;
        Invalidate();
    }

    public void Zoom(float steps)
    {
        if (width > 0 && height > 0) ApplyPendingFrame();
        pendingFrame = null;
        distance = Math.Clamp(distance * MathF.Pow(.85f, steps), radius * .005f, radius * 100);
        Invalidate();
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

    private (Vector3 Right, Vector3 Up) CameraAxes()
    {
        var up = new Vector3(-MathF.Sin(pitch) * MathF.Sin(yaw), MathF.Cos(pitch), -MathF.Sin(pitch) * MathF.Cos(yaw));
        var right = new Vector3(MathF.Cos(yaw), 0, -MathF.Sin(yaw));
        return (right * MathF.Cos(roll) - up * MathF.Sin(roll), up * MathF.Cos(roll) + right * MathF.Sin(roll));
    }

    /// <summary>The world-space ray under a point of the last rendered frame, in physical pixels.</summary>
    public bool TryGetRay(float x, float y, out Vector3 origin, out Vector3 direction)
    {
        origin = direction = default;
        if (width <= 0 || height <= 0) return false;
        Matrix4 inverse;
        try { inverse = Matrix4.Invert(viewProjection); }
        catch (InvalidOperationException) { return false; }
        float ndcX = 2 * x / width - 1, ndcY = 1 - 2 * y / height;
        var near = new Vector4(ndcX, ndcY, -1, 1) * inverse;
        var far = new Vector4(ndcX, ndcY, 1, 1) * inverse;
        if (near.W == 0 || far.W == 0) return false;
        origin = near.Xyz / near.W;
        direction = (far.Xyz / far.W - origin).Normalized();
        return true;
    }

    public void Render(int pixelWidth, int pixelHeight, int framebuffer, float scale = 1)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0) return;
        width = pixelWidth;
        height = pixelHeight;
        ApplyPendingFrame();
        try
        {
            GL.Disable(EnableCap.ScissorTest);
            GL.Disable(EnableCap.StencilTest);
            GL.ColorMask(true, true, true, true);
            if (surfaceProgram == 0) Initialize();
            EnsureTargets();
            if (meshDirty) UploadMesh();
            if (highlightDirty) UploadHighlight();
            if (hoverDirty) UploadHover();
            ReleaseRetiredModels();

            Vector3 direction = new(MathF.Cos(pitch) * MathF.Sin(yaw), MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Cos(yaw));
            var eye = center + direction * distance;
            var view = Matrix4.LookAt(eye, center, CameraAxes().Up);
            float aspect = width / (float)height;
            float near = Math.Max(radius * .0005f, distance - radius * 2), far = distance + radius * 3;
            float halfHeight = distance * MathF.Tan(MathHelper.PiOver4 / 2);
            var projection = orthographic
                ? Matrix4.CreateOrthographicOffCenter(-halfHeight * aspect, halfHeight * aspect, -halfHeight, halfHeight, near, far)
                : Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, aspect, near, far);
            viewProjection = view * projection;

            bool overlay = DrawsModel && RenderModel(pixelWidth, pixelHeight, framebuffer, scale);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, sampleFramebuffer);
            GL.Viewport(0, 0, width, height);
            GL.Disable(EnableCap.FramebufferSrgb);
            // Over a model the layer starts transparent and holds premultiplied color, to be laid over the model's frame.
            if (overlay) GL.ClearColor(0, 0, 0, 0);
            else GL.ClearColor(0x1c / 255f, 0x1e / 255f, 0x23 / 255f, 1);
            GL.DepthMask(true);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.Blend);
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);

            float opacity = overlay ? surfaceOpacity : 1;
            if (surfaceCount > 0)
            {
                // Push the surface back so its own edges draw cleanly on top.
                GL.Enable(EnableCap.PolygonOffsetFill);
                GL.PolygonOffset(1, 1);
                GL.UseProgram(surfaceProgram);
                GL.UniformMatrix4(surfaceMatrix, false, ref viewProjection);
                GL.BindVertexArray(surfaceVao);
                if (opacity < 1)
                {
                    // Depth first, so only the nearest surface blends where cliffs overlap.
                    GL.ColorMask(false, false, false, false);
                    GL.Uniform1(surfaceOpacityUniform, 1f);
                    GL.DrawArrays(PrimitiveType.Triangles, 0, surfaceCount);
                    GL.ColorMask(true, true, true, true);
                    GL.Enable(EnableCap.Blend);
                    GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
                }
                GL.Uniform1(surfaceOpacityUniform, opacity);
                if (opacity > 0) GL.DrawArrays(PrimitiveType.Triangles, 0, surfaceCount);
                GL.Disable(EnableCap.Blend);
                GL.Disable(EnableCap.PolygonOffsetFill);
            }

            GL.UseProgram(lineProgram);
            GL.UniformMatrix4(lineMatrix, false, ref viewProjection);
            GL.Enable(EnableCap.Blend);
            // Alpha adds up the same way as color, which keeps the layer premultiplied.
            GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha, BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
            if (showGrid && gridCount > 0)
            {
                GL.DepthMask(false);
                GL.Uniform4(lineColor, 1f, 1f, 1f, .22f);
                GL.BindVertexArray(gridVao);
                GL.DrawArrays(PrimitiveType.Lines, 0, gridCount);
                GL.DepthMask(true);
            }
            if (hoverCount > 0)
            {
                GL.DepthMask(false);
                GL.Uniform4(lineColor, 1f, 1f, 1f, .85f);
                GL.BindVertexArray(hoverVao);
                GL.DrawArrays(PrimitiveType.Lines, 0, hoverCount);
                GL.DepthMask(true);
            }
            if (highlightCount > 0)
            {
                // The selection stays visible behind hills.
                GL.Disable(EnableCap.DepthTest);
                GL.Uniform4(lineColor, 1f, .85f, .2f, 1f);
                GL.BindVertexArray(highlightVao);
                GL.DrawArrays(PrimitiveType.Lines, 0, highlightCount);
                GL.Enable(EnableCap.DepthTest);
            }
            GL.Disable(EnableCap.Blend);
            GL.BindVertexArray(0);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, sampleFramebuffer);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, resolveFramebuffer);
            GL.BlitFramebuffer(0, 0, width, height, 0, 0, width, height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
            if (overlay) Composite(framebuffer);
            else
            {
                GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, resolveFramebuffer);
                GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, framebuffer);
                GL.BlitFramebuffer(0, 0, width, height, 0, 0, width, height, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
            }
        }
        finally { GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer); }
    }

    /// <summary>Draws the model into the host framebuffer from this view's camera. False when it failed and was dropped.</summary>
    private bool RenderModel(int pixelWidth, int pixelHeight, int framebuffer, float scale)
    {
        var shown = model!;
        try
        {
            // Same orbit as the PBC, moved into the model's coordinates.
            shown.SetCamera(center + ModelOffset, distance, yaw, pitch, roll, orthographic);
            shown.Render(pixelWidth, pixelHeight, framebuffer, scale);
            return true;
        }
        catch (Exception error)
        {
            Model = null;
            ReleaseRetiredModels();
            ModelFailed?.Invoke(error);
            return false;
        }
        finally
        {
            // The model leaves its own state behind; this view sets what it relies on.
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
            GL.ColorMask(true, true, true, true);
            GL.DepthMask(true);
            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.CullFace);
            GL.Disable(EnableCap.ScissorTest);
            GL.Disable(EnableCap.StencilTest);
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }
    }

    /// <summary>Lays the resolved, premultiplied PBC layer over the model already in the host framebuffer.</summary>
    private void Composite(int framebuffer)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
        GL.Viewport(0, 0, width, height);
        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.FramebufferSrgb);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha);
        GL.UseProgram(compositeProgram);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindSampler(0, 0);
        GL.BindTexture(TextureTarget.Texture2D, resolveColor);
        GL.BindVertexArray(compositeVao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GL.BindVertexArray(0);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        GL.Disable(EnableCap.Blend);
        GL.Enable(EnableCap.DepthTest);
    }

    private void ReleaseRetiredModels()
    {
        foreach (var retired in retiredModels) retired.ReleaseGraphics();
        retiredModels.Clear();
    }

    private void Initialize()
    {
        surfaceProgram = CreateProgram("""
            #version 330 core
            layout(location=0) in vec3 position;
            layout(location=1) in vec3 normal;
            layout(location=2) in vec3 color;
            uniform mat4 viewProjection;
            out vec3 surfaceNormal;
            out vec3 surfaceColor;
            void main() { gl_Position = viewProjection * vec4(position, 1.0); surfaceNormal = normal; surfaceColor = color; }
            """, """
            #version 330 core
            in vec3 surfaceNormal;
            in vec3 surfaceColor;
            uniform float opacity;
            out vec4 fragColor;
            void main()
            {
                vec3 n = normalize(surfaceNormal);
                // A fixed key light from above and one side, plus a fill, so slope direction shows as brightness.
                float key = abs(dot(n, normalize(vec3(0.45, 1.0, 0.3))));
                float fill = abs(dot(n, normalize(vec3(-0.6, 0.4, -0.5))));
                // Premultiplied, for laying the surface over a model.
                fragColor = vec4(surfaceColor * (0.35 + 0.55 * key + 0.15 * fill) * opacity, opacity);
            }
            """);
        surfaceMatrix = GL.GetUniformLocation(surfaceProgram, "viewProjection");
        surfaceOpacityUniform = GL.GetUniformLocation(surfaceProgram, "opacity");
        GL.UseProgram(surfaceProgram);
        GL.Uniform1(surfaceOpacityUniform, 1f);
        compositeProgram = CreateProgram("""
            #version 330 core
            out vec2 uv;
            void main()
            {
                // One triangle that covers the screen.
                vec2 corner = vec2((gl_VertexID << 1) & 2, gl_VertexID & 2);
                uv = corner;
                gl_Position = vec4(corner * 2.0 - 1.0, 0.0, 1.0);
            }
            """, """
            #version 330 core
            in vec2 uv;
            uniform sampler2D layer;
            out vec4 fragColor;
            void main() { fragColor = texture(layer, uv); }
            """);
        GL.UseProgram(compositeProgram);
        GL.Uniform1(GL.GetUniformLocation(compositeProgram, "layer"), 0);
        GL.UseProgram(0);
        compositeVao = GL.GenVertexArray();
        lineProgram = CreateProgram("""
            #version 330 core
            layout(location=0) in vec3 position;
            uniform mat4 viewProjection;
            void main() { gl_Position = viewProjection * vec4(position, 1.0); }
            """, """
            #version 330 core
            uniform vec4 color;
            out vec4 fragColor;
            void main() { fragColor = color; }
            """);
        lineMatrix = GL.GetUniformLocation(lineProgram, "viewProjection");
        lineColor = GL.GetUniformLocation(lineProgram, "color");
        (surfaceVao, surfaceBuffer) = CreateVertexArray(PbcMesh.VertexFloats, [3, 3, 3]);
        (gridVao, gridBuffer) = CreateVertexArray(3, [3]);
        (highlightVao, highlightBuffer) = CreateVertexArray(3, [3]);
        (hoverVao, hoverBuffer) = CreateVertexArray(3, [3]);
        meshDirty = highlightDirty = hoverDirty = true;
    }

    private static (int Vao, int Buffer) CreateVertexArray(int stride, int[] attributes)
    {
        int vao = GL.GenVertexArray(), buffer = GL.GenBuffer();
        GL.BindVertexArray(vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, buffer);
        int offset = 0;
        for (int i = 0; i < attributes.Length; i++)
        {
            GL.EnableVertexAttribArray(i);
            GL.VertexAttribPointer(i, attributes[i], VertexAttribPointerType.Float, false, stride * sizeof(float), offset * sizeof(float));
            offset += attributes[i];
        }
        GL.BindVertexArray(0);
        return (vao, buffer);
    }

    private void UploadMesh()
    {
        Upload(surfaceBuffer, mesh.Surface);
        surfaceCount = mesh.Surface.Length / PbcMesh.VertexFloats;
        Upload(gridBuffer, mesh.Grid);
        gridCount = mesh.Grid.Length / 3;
        meshDirty = false;
    }

    private void UploadHighlight()
    {
        Upload(highlightBuffer, highlight);
        highlightCount = highlight.Length / 3;
        highlightDirty = false;
    }

    private void UploadHover()
    {
        Upload(hoverBuffer, hover);
        hoverCount = hover.Length / 3;
        hoverDirty = false;
    }

    private static void Upload(int buffer, float[] data)
    {
        GL.BindBuffer(BufferTarget.ArrayBuffer, buffer);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Length * sizeof(float), data, BufferUsageHint.StaticDraw);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    private void EnsureTargets()
    {
        if (sampleFramebuffer != 0 && targetWidth == width && targetHeight == height) return;
        ReleaseTargets();
        targetWidth = width;
        targetHeight = height;
        sampleFramebuffer = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, sampleFramebuffer);
        sampleColor = GL.GenRenderbuffer();
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, sampleColor);
        GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, Samples, RenderbufferStorage.Rgba8, width, height);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, sampleColor);
        sampleDepth = GL.GenRenderbuffer();
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, sampleDepth);
        GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, Samples, RenderbufferStorage.DepthComponent24, width, height);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, sampleDepth);
        CheckComplete("PBC multisample");
        resolveFramebuffer = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, resolveFramebuffer);
        // A texture, so the layer can be laid over a model.
        resolveColor = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, resolveColor);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, resolveColor, 0);
        CheckComplete("PBC resolve");
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
    }

    private static void CheckComplete(string name)
    {
        if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            throw new InvalidOperationException($"{name} framebuffer is incomplete.");
    }

    private void ReleaseTargets()
    {
        if (sampleFramebuffer != 0) GL.DeleteFramebuffer(sampleFramebuffer);
        if (resolveFramebuffer != 0) GL.DeleteFramebuffer(resolveFramebuffer);
        if (sampleColor != 0) GL.DeleteRenderbuffer(sampleColor);
        if (sampleDepth != 0) GL.DeleteRenderbuffer(sampleDepth);
        if (resolveColor != 0) GL.DeleteTexture(resolveColor);
        sampleFramebuffer = resolveFramebuffer = sampleColor = sampleDepth = resolveColor = 0;
    }

    public void ReleaseGraphics()
    {
        ReleaseTargets();
        ReleaseRetiredModels();
        model?.ReleaseGraphics();
        foreach (int vao in (int[])[surfaceVao, gridVao, highlightVao, hoverVao, compositeVao]) if (vao != 0) GL.DeleteVertexArray(vao);
        foreach (int buffer in (int[])[surfaceBuffer, gridBuffer, highlightBuffer, hoverBuffer]) if (buffer != 0) GL.DeleteBuffer(buffer);
        if (surfaceProgram != 0) GL.DeleteProgram(surfaceProgram);
        if (lineProgram != 0) GL.DeleteProgram(lineProgram);
        if (compositeProgram != 0) GL.DeleteProgram(compositeProgram);
        surfaceVao = gridVao = highlightVao = hoverVao = compositeVao = surfaceBuffer = gridBuffer = highlightBuffer = hoverBuffer = 0;
        surfaceProgram = lineProgram = compositeProgram = 0;
        surfaceCount = gridCount = highlightCount = hoverCount = 0;
        meshDirty = highlightDirty = hoverDirty = true;
    }

    private static int CreateProgram(string vertex, string fragment)
    {
        int vs = 0, fs = 0, id = 0;
        try
        {
            vs = Compile(ShaderType.VertexShader, vertex);
            fs = Compile(ShaderType.FragmentShader, fragment);
            id = GL.CreateProgram();
            GL.AttachShader(id, vs);
            GL.AttachShader(id, fs);
            GL.LinkProgram(id);
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
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        GL.GetShader(shader, ShaderParameter.CompileStatus, out int compiled);
        if (compiled != 0) return shader;
        string log = GL.GetShaderInfoLog(shader);
        GL.DeleteShader(shader);
        throw new InvalidOperationException(log);
    }
}

using HeavenTool.IO.FileFormats.BFRES;
using HeavenTool.Shaders;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace HeavenTool.Rendering.BFRES;

// The game's screen-space ambient occlusion, drawn every frame at half the frame's size with its own programs: the opaque
// meshes' Z-only variants lay the camera depth down first, as the game's prepass does, gsys's render_buffer_depth turns a
// copy of it into view depth, agl's alchemy_ao_buffer darkens by it into ssaoMaps[1], and ssao_blur softens that down into
// ssaoMaps[0] and across back, blur_num times. The main pass then draws onto the prepass's depth.
public sealed partial class BfresRenderer
{
    private int ssaoViewDepth, ssaoViewDepthFramebuffer, ssaoWidth, ssaoHeight, ssaoVao, ssaoVertices, ssaoParameters;
    private readonly int[] ssaoMaps = new int[2], ssaoFramebuffers = new int[2];
    private readonly Dictionary<(SwitchProcessShaderProgram Source, int Variation), SsaoPass> ssaoPasses = [];
    // The c3 block the passes share, six rows of it.
    private readonly float[] ssaoRows = new float[24];
    // Whether this frame's depth was laid down by the prepass, which the main pass then draws onto.
    private bool depthPrepassed;

    private sealed record SsaoPass(int Program, int VertexConstants, int FragmentConstants);

    // Its size follows the frame's, so it is laid out when the frame is drawn.
    private int SsaoMap()
    {
        if (ssaoMaps[1] != 0) return ssaoMaps[1];
        for (int i = 0; i < 2; i++) { ssaoMaps[i] = GL.GenTexture(); ssaoFramebuffers[i] = GL.GenFramebuffer(); }
        ssaoViewDepth = GL.GenTexture();
        ssaoViewDepthFramebuffer = GL.GenFramebuffer();
        ssaoWidth = ssaoHeight = 0;
        return ssaoMaps[1];
    }

    // Draws after the frame's camera context is up to date and its target is cleared, and leaves that target bound with the
    // opaque depth in it.
    private void RenderSsao(Matrix4 projection, float near, float far)
    {
        depthPrepassed = false;
        if (!occlusion || ssaoMaps[1] == 0) return;
        int w = Math.Max(1, width / 2), h = Math.Max(1, height / 2);
        EnsureSsaoTargets(w, h);
        if (lighting.ScreenOcclusion is not { } ao)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, ssaoFramebuffers[1]);
            GL.ClearColor(1, 1, 1, 1);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.ClearColor(0, 0, 0, 0);
            // The shaders read a level down, which would keep the last occlusion drawn.
            GL.BindTexture(TextureTarget.Texture2D, ssaoMaps[1]);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            RestoreFrameTarget();
            return;
        }

        DrawDepthPrepass();
        CopyCameraDepth();
        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.Blend);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.FramebufferSrgb);
        GL.ColorMask(true, true, true, true);
        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        GL.Viewport(0, 0, w, h);
        var settings = ao.Settings;
        string perspective = orthographic ? "0" : "1";

        // View depth from the near plane, or a copy of the depth an orthographic camera already keeps linear.
        Rows(0, new Vector4(near, (far - near) / far, 0, 0));
        DrawSsaoPass(ao.Passes.Depth, new()
        {
            ["DRAW_NORMALIZED_VIEW_DEPTH"] = "1", ["COPY_DEPTH"] = "0", ["IS_ORTHO"] = orthographic ? "1" : "0",
            ["DEPTH_MAX"] = "0", ["DEPTH_WITH_MODIFY_OFFSET"] = "0",
        }, ssaoViewDepthFramebuffer, cameraDepthCopy);

        // [0].y turns a depth into view z; [1].zw take the pass's output back to the camera's distance; [1].xy and [2].xy a
        // texture coordinate to a view position, before its depth scales it for perspective; [3].zw a texel, also the
        // smallest radius worth sampling; [4] the reach, the bias and the strength; [5].x the largest reach, a fraction of
        // the frame, and [5].z how far over the bias one sample darkens fully.
        float px = 1 / projection.M11, py = 1 / projection.M22;
        Rows(0, new Vector4(0, 1, 0, 0), new(px, -py, near, orthographic ? far - near : 1), new(2 * px, -2 * py, 0, 0), new(0, 0, 1f / w, 1f / h),
            new(orthographic ? settings.Radius : settings.Radius * .5f * projection.M11, 0, settings.Bias, settings.Intensity),
            new(settings.MaxRadius, 0, settings.Density, 0));
        DrawSsaoPass(ao.Passes.Ao, new() { ["NUM_SAMPLES"] = settings.Samples.ToString(), ["IS_PERSPECTIVE"] = perspective },
            ssaoFramebuffers[1], ssaoViewDepth);

        // Three taps a texel and a third apart, down and then across.
        Rows(0, default, default, default, default, new(1f / w, 1f / h, 0, 0));
        for (int pass = 0; pass < settings.BlurCount; pass++)
        {
            DrawSsaoPass(ao.Passes.Blur, new() { ["BLUR_X"] = "0", ["BLUR_MODIFY"] = "0" }, ssaoFramebuffers[0], ssaoMaps[1]);
            DrawSsaoPass(ao.Passes.Blur, new() { ["BLUR_X"] = "1", ["BLUR_MODIFY"] = "0" }, ssaoFramebuffers[1], ssaoMaps[0]);
        }
        // The shaders read the map a level down, already softened.
        GL.BindTexture(TextureTarget.Texture2D, ssaoMaps[1]);
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        RestoreFrameTarget();
    }

    private void RestoreFrameTarget()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, hdrFramebuffer);
        GL.Viewport(0, 0, width, height);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthMask(true);
        GL.PolygonMode(TriangleFace.FrontAndBack, wireframe ? PolygonMode.Line : PolygonMode.Fill);
    }

    private void EnsureSsaoTargets(int w, int h)
    {
        if (ssaoWidth == w && ssaoHeight == h) return;
        ssaoWidth = w;
        ssaoHeight = h;
        GL.BindTexture(TextureTarget.Texture2D, ssaoViewDepth);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32f, w, h, 0, PixelFormat.Red, PixelType.Float, IntPtr.Zero);
        // Filtered: the AO places each sample at its exact texture coordinate, so a nearest texel's depth would put it off
        // a slanted surface by more than the bias, and darken flat floors.
        TextureParameters(TextureMinFilter.Linear);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, ssaoViewDepthFramebuffer);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, ssaoViewDepth, 0);
        CheckFramebufferComplete("SSAO view depth");
        for (int i = 0; i < 2; i++)
        {
            GL.BindTexture(TextureTarget.Texture2D, ssaoMaps[i]);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R8, w, h, 0, PixelFormat.Red, PixelType.UnsignedByte, IntPtr.Zero);
            TextureParameters(i == 1 ? TextureMinFilter.LinearMipmapLinear : TextureMinFilter.Linear);
            // The blur writes red alone; the shaders read the map from any channel.
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleRgba, new[] { (int)All.Red, (int)All.Red, (int)All.Red, (int)All.Red });
            if (i == 1) GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, ssaoFramebuffers[i]);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, ssaoMaps[i], 0);
            CheckFramebufferComplete("SSAO");
        }
    }

    // Whether a mesh's depth comes from the prepass: an opaque native mesh that writes depth and has a Z-only variant.
    private bool PrepassesDepth(GpuMesh mesh, out NativeMeshGpu? depth)
    {
        depth = null;
        var profile = mesh.Material.Profile;
        return !mesh.Source.Material.IsShadowOnly && !profile.Translucent && profile.DepthTest && profile.DepthWrite
            && nativeMeshes.ContainsKey(mesh.Source) && nativeDepthMeshes.TryGetValue(mesh.Source, out depth);
    }

    // With the material's own culling, unlike the world map's, so a room's walls facing the camera leave its inside seen.
    private void DrawDepthPrepass()
    {
        GL.ColorMask(false, false, false, false);
        GL.PolygonMode(TriangleFace.FrontAndBack, wireframe ? PolygonMode.Line : PolygonMode.Fill);
        foreach (var mesh in meshes)
        {
            if (!Shown(mesh.Source) || !PrepassesDepth(mesh, out var depth)) continue;
            ApplyRasterState(mesh.Material.Profile);
            GL.Disable(EnableCap.Blend);
            DrawNativeMesh(mesh, depth!);
        }
        GL.ColorMask(true, true, true, true);
        depthPrepassed = true;
    }

    // A "less" material would fail on the depth its own prepass laid down.
    private void MatchPrepassedDepth(GpuMesh mesh)
    {
        if (depthPrepassed && PrepassesDepth(mesh, out _) && mesh.Material.Profile.Source.Info("gsys_depth_test_func", "lequal") == "less")
            GL.DepthFunc(DepthFunction.Lequal);
    }

    private void Rows(int first, params Vector4[] rows)
    {
        if (first == 0) Array.Clear(ssaoRows);
        for (int i = 0; i < rows.Length; i++)
        {
            ssaoRows[(first + i) * 4] = rows[i].X;
            ssaoRows[(first + i) * 4 + 1] = rows[i].Y;
            ssaoRows[(first + i) * 4 + 2] = rows[i].Z;
            ssaoRows[(first + i) * 4 + 3] = rows[i].W;
        }
    }

    // One full-frame draw of a variation of an agl or gsys process program, sampling source.
    private void DrawSsaoPass(SwitchProcessShaderProgram source, Dictionary<string, string> choices, int framebuffer, int texture)
    {
        var pass = SsaoProgram(source, source.FindVariation(choices));
        if (ssaoParameters == 0) ssaoParameters = CreateBuffer(new byte[ParkUniforms.BufferSize]);
        GL.BindBuffer(BufferTarget.UniformBuffer, ssaoParameters);
        GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, ssaoRows.Length * sizeof(float), ssaoRows);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
        GL.UseProgram(pass.Program);
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, pass.VertexConstants);
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 1, ssaoParameters);
        GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 2, pass.FragmentConstants);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindSampler(0, 0);
        GL.BindTexture(TextureTarget.Texture2D, texture);
        GL.BindVertexArray(SsaoVao());
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
    }

    private SsaoPass SsaoProgram(SwitchProcessShaderProgram source, int variation)
    {
        if (ssaoPasses.TryGetValue((source, variation), out var pass)) return pass;
        var stages = source.ReadVariation(variation);
        var vertex = stages.Vertex ?? throw new InvalidDataException($"{source.Name} has no vertex stage.");
        var fragment = stages.Fragment ?? throw new InvalidDataException($"{source.Name} has no fragment stage.");
        int id = CreateProgram(NativeShaderTranslator.TranslateStage(vertex), NativeShaderTranslator.TranslateStage(fragment));
        // Each stage's embedded constants, and the c3 block both stages read.
        BindBlock(id, "vp_c1", 0);
        BindBlock(id, "vp_c3", 1);
        BindBlock(id, "fp_c1", 2);
        BindBlock(id, "fp_c3", 1);
        GL.UseProgram(id);
        int scale = GL.GetUniformLocation(id, "fp_renderScale");
        if (scale >= 0) GL.Uniform1(scale, 1f);
        foreach (string name in new[] { "vp_tex_tcb_8", "fp_tex_tcb_8" })
            if (GL.GetUniformLocation(id, name) is var location and >= 0) GL.Uniform1(location, 0);
        pass = new(id, CreateBuffer(ParkUniforms.Constants(vertex)), CreateBuffer(ParkUniforms.Constants(fragment)));
        ssaoPasses.Add((source, variation), pass);
        return pass;
    }

    // The game's full-frame triangle: positions halved, as its vertex programs double them, and texture coordinates.
    private int SsaoVao()
    {
        if (ssaoVao != 0) return ssaoVao;
        float[] vertices = [-.5f, -.5f, 0, 1, 0, 0, 0, 0, 1.5f, -.5f, 0, 1, 2, 0, 0, 0, -.5f, 1.5f, 0, 1, 0, 2, 0, 0];
        ssaoVao = GL.GenVertexArray();
        ssaoVertices = GL.GenBuffer();
        GL.BindVertexArray(ssaoVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, ssaoVertices);
        GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);
        for (int attribute = 0; attribute < 2; attribute++)
        {
            GL.EnableVertexAttribArray(attribute);
            GL.VertexAttribPointer(attribute, 4, VertexAttribPointerType.Float, false, 8 * sizeof(float), attribute * 4 * sizeof(float));
        }
        return ssaoVao;
    }

    private void ReleaseSsao()
    {
        foreach (int texture in ssaoMaps.Append(ssaoViewDepth)) if (texture != 0) GL.DeleteTexture(texture);
        foreach (int framebuffer in ssaoFramebuffers.Append(ssaoViewDepthFramebuffer)) if (framebuffer != 0) GL.DeleteFramebuffer(framebuffer);
        foreach (var pass in ssaoPasses.Values)
        {
            GL.DeleteProgram(pass.Program);
            GL.DeleteBuffer(pass.VertexConstants);
            GL.DeleteBuffer(pass.FragmentConstants);
        }
        if (ssaoParameters != 0) GL.DeleteBuffer(ssaoParameters);
        if (ssaoVertices != 0) GL.DeleteBuffer(ssaoVertices);
        if (ssaoVao != 0) GL.DeleteVertexArray(ssaoVao);
        ssaoPasses.Clear();
        Array.Clear(ssaoMaps);
        Array.Clear(ssaoFramebuffers);
        ssaoViewDepth = ssaoViewDepthFramebuffer = ssaoParameters = ssaoVertices = ssaoVao = 0;
        ssaoWidth = ssaoHeight = 0;
        depthPrepassed = false;
    }
}

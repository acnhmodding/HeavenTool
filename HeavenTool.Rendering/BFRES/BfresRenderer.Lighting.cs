using OpenTK.Graphics.OpenGL4;

namespace HeavenTool.Rendering.BFRES;

public sealed partial class BfresRenderer
{
    private const int Samples = 4;
    // Lit frames render into a multisampled HDR target, resolved into hdrColor before tone mapping.
    private int hdrFramebuffer, hdrSamples, hdrDepth, resolveFramebuffer, resolveDepth, hdrColor, toneProgram, toneVao, toneExposure;
    private int hdrWidth, hdrHeight;
    private float previewExposure = .06f;
    public void SetExposure(float value)
    {
        if (!float.IsFinite(value) || value < .00375f || value > .96f) throw new ArgumentOutOfRangeException(nameof(value));
        previewExposure = value; Invalidate();
    }
    private void BeginLightingFrame()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, outputFramebuffer);
        if (hdrFramebuffer == 0 || hdrWidth != width || hdrHeight != height)
        {
            ReleaseLightingTarget();
            hdrWidth = width; hdrHeight = height;
            GL.GetInteger(GetPName.MaxTextureSize, out int maxSize);
            if (hdrWidth > maxSize || hdrHeight > maxSize) throw new NotSupportedException("Viewport exceeds the GPU's HDR texture size limit.");
            int samples = Math.Min(Samples, GL.GetInteger(GetPName.MaxSamples));
            hdrFramebuffer = GL.GenFramebuffer(); GL.BindFramebuffer(FramebufferTarget.Framebuffer, hdrFramebuffer);
            hdrSamples = GL.GenRenderbuffer(); GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, hdrSamples);
            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, samples, RenderbufferStorage.Rgba16f, hdrWidth, hdrHeight);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, hdrSamples);
            hdrDepth = GL.GenRenderbuffer(); GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, hdrDepth);
            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, samples, RenderbufferStorage.DepthComponent24, hdrWidth, hdrHeight);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, hdrDepth);
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                throw new InvalidOperationException("HDR lighting framebuffer is incomplete.");
            resolveDepth = GL.GenRenderbuffer();
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, resolveDepth);
            GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, hdrWidth, hdrHeight);
            resolveFramebuffer = GL.GenFramebuffer(); GL.BindFramebuffer(FramebufferTarget.Framebuffer, resolveFramebuffer);
            hdrColor = GL.GenTexture(); GL.BindTexture(TextureTarget.Texture2D, hdrColor);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, hdrWidth, hdrHeight, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            TextureParameters(TextureMinFilter.Nearest);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, hdrColor, 0);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, resolveDepth);
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                throw new InvalidOperationException("HDR resolve framebuffer is incomplete.");
        }
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, hdrFramebuffer);
    }
    private void EndLightingFrame()
    {
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, hdrFramebuffer);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, resolveFramebuffer);
        GL.BlitFramebuffer(0, 0, hdrWidth, hdrHeight, 0, 0, hdrWidth, hdrHeight, ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, outputFramebuffer);
        GL.Disable(EnableCap.DepthTest); GL.Disable(EnableCap.Blend); GL.Disable(EnableCap.CullFace); GL.Disable(EnableCap.FramebufferSrgb);
        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        if (toneProgram == 0)
        {
            toneProgram = CreateProgram("""
                    #version 330 core
                    out vec2 uv;
                    void main() { uv=vec2((gl_VertexID<<1)&2,gl_VertexID&2); gl_Position=vec4(uv*2.0-1.0,0,1); }
                    """, """
                    #version 330 core
                    in vec2 uv; out vec4 color;
                    uniform sampler2D frame; uniform float exposure;
                    void main() {
                        vec4 source=texture(frame,uv);
                        vec3 x=max(source.rgb,vec3(0))*exposure;
                        vec3 mapped=clamp((x*(2.51*x+.03))/(x*(2.43*x+.59)+.14),0.0,1.0);
                        mapped=pow(mapped,vec3(1.0/2.2));
                        color=vec4(mix(vec3(.11,.12,.14),mapped,clamp(source.a,0.0,1.0)),1);
                    }
                    """);
            toneVao = GL.GenVertexArray(); toneExposure = GL.GetUniformLocation(toneProgram, "exposure");
            GL.UseProgram(toneProgram); GL.Uniform1(GL.GetUniformLocation(toneProgram, "frame"), 0);
        }
        GL.UseProgram(toneProgram);
        // Fixed preview exposure, shared by Day/Night. The game's post-processing is not reconstructed.
        GL.Uniform1(toneExposure, previewExposure);
        GL.ActiveTexture(TextureUnit.Texture0); GL.BindTexture(TextureTarget.Texture2D, hdrColor); GL.BindSampler(0, 0);
        GL.BindVertexArray(toneVao); GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
    }
    private void ReleaseLightingTarget()
    {
        if (hdrFramebuffer != 0) GL.DeleteFramebuffer(hdrFramebuffer);
        if (resolveFramebuffer != 0) GL.DeleteFramebuffer(resolveFramebuffer);
        if (hdrSamples != 0) GL.DeleteRenderbuffer(hdrSamples);
        if (hdrDepth != 0) GL.DeleteRenderbuffer(hdrDepth);
        if (resolveDepth != 0) GL.DeleteRenderbuffer(resolveDepth);
        if (hdrColor != 0) GL.DeleteTexture(hdrColor);
        hdrFramebuffer = resolveFramebuffer = hdrSamples = hdrDepth = resolveDepth = hdrColor = 0;
    }
    private void ReleaseLighting()
    {
        ReleaseLightingTarget();
        if (toneProgram != 0) GL.DeleteProgram(toneProgram);
        if (toneVao != 0) GL.DeleteVertexArray(toneVao);
        toneProgram = toneVao = 0;
    }
}

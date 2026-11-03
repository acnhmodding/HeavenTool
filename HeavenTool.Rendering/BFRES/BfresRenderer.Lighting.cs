using OpenTK.Graphics.OpenGL4;

namespace HeavenTool.Rendering.BFRES;

public sealed partial class BfresRenderer
{
    private const int Samples = 4;
    // Lit frames render into a multisampled HDR target, resolved into hdrColor before tone mapping.
    private int hdrFramebuffer, hdrSamples, hdrDepth, resolveFramebuffer, resolveDepth, hdrColor, toneProgram, toneVao, toneExposure, toneGraded;
    private int hdrWidth, hdrHeight;
    private float previewExposure = 1;
    public void SetExposure(float value)
    {
        if (!float.IsFinite(value) || value < .0625f || value > 16f) throw new ArgumentOutOfRangeException(nameof(value));
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
                    uniform sampler2D frame; uniform sampler3D grading; uniform float exposure; uniform bool graded;
                    void main() {
                        vec4 source=texture(frame,uv);
                        vec3 x=max(source.rgb,vec3(0))*exposure;
                        // Park_Proc ToneMapping, cExponentialLuminance: exponential on luminance, blended toward
                        // per-channel 1-exp(-x) as the pixel brightens. mSaturateBias 0 leaves saturation as is.
                        float luminance=dot(x,vec3(.298912,.586611,.114478)), k=1.0-exp(-luminance);
                        vec3 byLuminance=luminance>0.0?x*(k/luminance):x;
                        vec3 mapped=clamp(mix(byLuminance,1.0-exp(-x),k*k),0.0,1.0);
                        // HDR_COLOR_CORRECTION: the grading volume at texel centers.
                        if(graded) mapped=texture(grading,mapped*.875+.0625).rgb;
                        // ToneMapping writes linear color; the game's sRGB target applies the piecewise encode on store.
                        mapped=mix(mapped*12.92,1.055*pow(mapped,vec3(1.0/2.4))-.055,step(.0031308,mapped));
                        color=vec4(mix(vec3(.11,.12,.14),mapped,clamp(source.a,0.0,1.0)),1);
                    }
                    """);
            toneVao = GL.GenVertexArray(); toneExposure = GL.GetUniformLocation(toneProgram, "exposure"); toneGraded = GL.GetUniformLocation(toneProgram, "graded");
            GL.UseProgram(toneProgram); GL.Uniform1(GL.GetUniformLocation(toneProgram, "frame"), 0); GL.Uniform1(GL.GetUniformLocation(toneProgram, "grading"), 1);
        }
        bool graded = PrepareGrading();
        GL.UseProgram(toneProgram);
        GL.Uniform1(toneGraded, graded ? 1 : 0);
        GL.ActiveTexture(TextureUnit.Texture1); GL.BindTexture(TextureTarget.Texture3D, graded ? gradingTexture : 0); GL.BindSampler(1, 0);
        // The game exposes by exp(mExpose); ToneMapping.byml ships mExpose 0, so scenes at game scale use 1.
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
        ReleaseGrading();
        ReleaseProbe();
        if (toneProgram != 0) GL.DeleteProgram(toneProgram);
        if (toneVao != 0) GL.DeleteVertexArray(toneVao);
        toneProgram = toneVao = 0;
    }
}

using OpenTK.Graphics.OpenGL4;

namespace HeavenTool.Rendering.BFRES;

public sealed partial class BfresRenderer
{
    // agl::pfx::Bloom's chain: bloom_mask at half size, bloom_reduce halving into the four composed levels, and
    // bloom_gaussian's two directions on each; ToneMapping then adds the weighted levels to the HDR color.
    private const int BloomLevels = 4;
    private readonly int[] bloomLevels = new int[BloomLevels], bloomScratch = new int[BloomLevels];
    private int bloomMaskTexture, bloomFramebuffer, bloomMaskProgram, bloomReduceProgram, bloomBlurProgram, bloomVao;
    private int bloomWidth, bloomHeight;
    private int bloomMaskThreshold, bloomMaskLimit, bloomMaskIntensity, bloomBlurStep;

    private const string BloomVertex = """
        #version 330 core
        out vec2 uv;
        void main() { uv=vec2((gl_VertexID<<1)&2,gl_VertexID&2); gl_Position=vec4(uv*2.0-1.0,0,1); }
        """;

    /// <summary>Draws the bloom levels from hdrColor; false when the lighting has no bloom.</summary>
    private bool RenderBloom()
    {
        if (lighting.Bloom is not { } bloom) return false;
        PrepareBloom();
        int w = Math.Max(1, hdrWidth / 2), h = Math.Max(1, hdrHeight / 2);
        GL.BindVertexArray(bloomVao);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, bloomFramebuffer);

        // bloom_mask, with BLM_LUMINANCE_CLAMP when clamped_luminance is enabled: each full-size texel keeps
        // clamp((L - threshhold·a) / range, 0, 1) of itself, scaled down to clamped_luminance; four make a texel here.
        GL.UseProgram(bloomMaskProgram);
        GL.Uniform2(bloomMaskThreshold, bloom.Threshold, bloom.Range > 0 ? 1 / bloom.Range : 0);
        GL.Uniform1(bloomMaskLimit, bloom.ClampedLuminance ?? 0);
        GL.Uniform1(bloomMaskIntensity, bloom.Intensity);
        Target(bloomMaskTexture, w, h, hdrColor);

        // bloom_reduce's single bilinear tap per halving, then bloom_gaussian across and down each level.
        int source = bloomMaskTexture;
        for (int level = 0; level < BloomLevels; level++)
        {
            w = Math.Max(1, w / 2); h = Math.Max(1, h / 2);
            GL.UseProgram(bloomReduceProgram);
            Target(bloomLevels[level], w, h, source);
            GL.UseProgram(bloomBlurProgram);
            GL.Uniform2(bloomBlurStep, 1f / w, 0f);
            Target(bloomScratch[level], w, h, bloomLevels[level]);
            GL.Uniform2(bloomBlurStep, 0f, 1f / h);
            Target(bloomLevels[level], w, h, bloomScratch[level]);
            source = bloomLevels[level];
        }
        GL.ActiveTexture(TextureUnit.Texture0);
        return true;

        void Target(int texture, int targetWidth, int targetHeight, int input)
        {
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture, 0);
            GL.Viewport(0, 0, targetWidth, targetHeight);
            GL.ActiveTexture(TextureUnit.Texture0); GL.BindTexture(TextureTarget.Texture2D, input); GL.BindSampler(0, 0);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }
    }

    private void PrepareBloom()
    {
        if (bloomMaskProgram == 0)
        {
            bloomMaskProgram = CreateProgram(BloomVertex, """
                #version 330 core
                in vec2 uv; out vec4 color;
                uniform sampler2D frame; uniform vec2 threshold; uniform float limit, intensity;
                vec3 masked(ivec2 at) {
                    vec4 s=texelFetch(frame,clamp(at,ivec2(0),textureSize(frame,0)-1),0);
                    vec3 c=max(s.rgb,vec3(0));
                    // ToneMapping's luminance weights; threshhold_balance (1, 1, 1) leaves them as is.
                    float luminance=dot(c,vec3(.298912,.586611,.114478));
                    float w=clamp((luminance-threshold.x*clamp(s.a,0.0,1.0))*threshold.y,0.0,1.0);
                    if(limit>0.0&&luminance>limit) w*=limit/luminance;
                    return c*w*intensity;
                }
                void main() {
                    ivec2 at=ivec2(gl_FragCoord.xy)*2;
                    color=vec4((masked(at)+masked(at+ivec2(1,0))+masked(at+ivec2(0,1))+masked(at+ivec2(1,1)))*.25,0);
                }
                """);
            bloomMaskThreshold = GL.GetUniformLocation(bloomMaskProgram, "threshold");
            bloomMaskLimit = GL.GetUniformLocation(bloomMaskProgram, "limit");
            bloomMaskIntensity = GL.GetUniformLocation(bloomMaskProgram, "intensity");
            bloomReduceProgram = CreateProgram(BloomVertex, """
                #version 330 core
                in vec2 uv; out vec4 color; uniform sampler2D frame;
                void main() { color=texture(frame,uv); }
                """);
            // bloom_gaussian: nine taps folded into five bilinear ones, the 0.31621623 weight at 1.3846 texels.
            bloomBlurProgram = CreateProgram(BloomVertex, """
                #version 330 core
                in vec2 uv; out vec4 color; uniform sampler2D frame; uniform vec2 step;
                void main() {
                    vec2 near=step*1.3846154, far=step*3.2307692;
                    color=texture(frame,uv)*.22702703
                        +(texture(frame,uv-near)+texture(frame,uv+near))*.31621623
                        +(texture(frame,uv-far)+texture(frame,uv+far))*.07027027;
                }
                """);
            bloomBlurStep = GL.GetUniformLocation(bloomBlurProgram, "step");
            bloomVao = GL.GenVertexArray();
            bloomFramebuffer = GL.GenFramebuffer();
        }
        if (bloomMaskTexture != 0 && bloomWidth == hdrWidth && bloomHeight == hdrHeight) return;
        ReleaseBloomTargets();
        bloomWidth = hdrWidth; bloomHeight = hdrHeight;
        int w = Math.Max(1, hdrWidth / 2), h = Math.Max(1, hdrHeight / 2);
        bloomMaskTexture = Level(w, h);
        for (int level = 0; level < BloomLevels; level++)
        {
            w = Math.Max(1, w / 2); h = Math.Max(1, h / 2);
            bloomLevels[level] = Level(w, h);
            bloomScratch[level] = Level(w, h);
        }

        static int Level(int w, int h)
        {
            int texture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba16f, w, h, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            TextureParameters(TextureMinFilter.Linear);
            return texture;
        }
    }

    // Binds the composed levels from texture unit 2 on, for the tone program's bloom sum.
    private void BindBloomLevels(bool bloomed)
    {
        for (int level = 0; level < BloomLevels; level++)
        {
            GL.ActiveTexture(TextureUnit.Texture2 + level);
            GL.BindTexture(TextureTarget.Texture2D, bloomed ? bloomLevels[level] : 0);
            GL.BindSampler(2 + level, 0);
        }
        GL.ActiveTexture(TextureUnit.Texture0);
    }

    private void ReleaseBloomTargets()
    {
        if (bloomMaskTexture != 0) GL.DeleteTexture(bloomMaskTexture);
        for (int level = 0; level < BloomLevels; level++)
        {
            if (bloomLevels[level] != 0) GL.DeleteTexture(bloomLevels[level]);
            if (bloomScratch[level] != 0) GL.DeleteTexture(bloomScratch[level]);
            bloomLevels[level] = bloomScratch[level] = 0;
        }
        bloomMaskTexture = 0;
    }

    private void ReleaseBloom()
    {
        ReleaseBloomTargets();
        foreach (int program in new[] { bloomMaskProgram, bloomReduceProgram, bloomBlurProgram }) if (program != 0) GL.DeleteProgram(program);
        if (bloomVao != 0) GL.DeleteVertexArray(bloomVao);
        if (bloomFramebuffer != 0) GL.DeleteFramebuffer(bloomFramebuffer);
        bloomMaskProgram = bloomReduceProgram = bloomBlurProgram = bloomVao = bloomFramebuffer = 0;
    }
}

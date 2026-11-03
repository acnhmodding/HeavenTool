using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace HeavenTool.Rendering.BFRES;

public sealed partial class BfresRenderer
{
    private const string NormalizedDepthFragmentShader = """
        #version 330 core
        in vec2 uv;
        out float normalizedDepth;
        uniform sampler2D cameraDepth;
        uniform mat4 unprojection;
        uniform vec2 nearFar;
        void main() {
            float depth = texelFetch(cameraDepth, ivec2(gl_FragCoord.xy), 0).r;
            if (depth >= 1.0) { normalizedDepth = 1.0; return; }
            vec4 view = unprojection * vec4(uv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
            float distance = -view.z / view.w;
            normalizedDepth = clamp((distance - nearFar.x) / (nearFar.y - nearFar.x), 0.0, 1.0);
        }
        """;

    private int normalizedLinearDepth, cameraDepthCopy, cameraDepthFramebuffer, normalizedDepthFramebuffer;
    private int normalizedDepthProgram, normalizedDepthVao, normalizedDepthWidth, normalizedDepthHeight, cameraDepthWidth, cameraDepthHeight;

    private int NormalizedLinearDepthMap()
    {
        if (normalizedLinearDepth != 0) return normalizedLinearDepth;
        normalizedLinearDepth = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, normalizedLinearDepth);
        // Until the first opaque pass is complete, the background is at the far plane.
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32f, 1, 1, 0, PixelFormat.Red, PixelType.Float, new float[] { 1 });
        NearestTextureParameters();
        return normalizedLinearDepth;
    }

    // Snapshot opaque camera depth before any translucent material can write into it. A separate sampled image avoids
    // attachment feedback and includes the real color pass's culling, alpha discard and fallback geometry.
    private void RenderNormalizedLinearDepth(Matrix4 projection, float near, float far)
    {
        if (normalizedLinearDepth == 0) return;
        CopyCameraDepth();
        EnsureNormalizedDepthTargets();
        EnsureNormalizedDepthProgram();

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, normalizedDepthFramebuffer);
        GL.Viewport(0, 0, width, height);
        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.Blend);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.FramebufferSrgb);
        GL.ColorMask(true, true, true, true);
        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);

        GL.UseProgram(normalizedDepthProgram);
        var inverse = projection.Inverted();
        GL.UniformMatrix4(GL.GetUniformLocation(normalizedDepthProgram, "unprojection"), false, ref inverse);
        GL.Uniform2(GL.GetUniformLocation(normalizedDepthProgram, "nearFar"), new Vector2(near, far));
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindSampler(0, 0);
        GL.BindTexture(TextureTarget.Texture2D, cameraDepthCopy);
        GL.BindVertexArray(normalizedDepthVao);
        GL.DrawArrays(PrimitiveType.Triangles, 0, 3);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, hdrFramebuffer);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthMask(true);
        GL.PolygonMode(TriangleFace.FrontAndBack, wireframe ? PolygonMode.Line : PolygonMode.Fill);
    }

    // A single-sampled copy of the frame's depth so far, which the SSAO passes also read.
    private void CopyCameraDepth()
    {
        EnsureCameraDepthCopy();
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, hdrFramebuffer);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, cameraDepthFramebuffer);
        GL.BlitFramebuffer(0, 0, width, height, 0, 0, width, height, ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
    }

    private void EnsureCameraDepthCopy()
    {
        if (cameraDepthCopy == 0)
        {
            cameraDepthCopy = GL.GenTexture();
            cameraDepthFramebuffer = GL.GenFramebuffer();
        }
        if (cameraDepthWidth == width && cameraDepthHeight == height) return;

        GL.BindTexture(TextureTarget.Texture2D, cameraDepthCopy);
        // Depth blits must match the multisampled HDR target's depth format.
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent24, width, height, 0, PixelFormat.DepthComponent, PixelType.UnsignedInt, IntPtr.Zero);
        NearestTextureParameters();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, cameraDepthFramebuffer);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, cameraDepthCopy, 0);
        GL.DrawBuffer(DrawBufferMode.None);
        GL.ReadBuffer(ReadBufferMode.None);
        CheckFramebufferComplete("Opaque camera depth");
        cameraDepthWidth = width;
        cameraDepthHeight = height;
    }

    private void EnsureNormalizedDepthTargets()
    {
        if (normalizedDepthFramebuffer == 0) normalizedDepthFramebuffer = GL.GenFramebuffer();
        if (normalizedDepthWidth == width && normalizedDepthHeight == height) return;

        // Resize the existing texture object: native sampler bindings keep its identity across viewport changes.
        GL.BindTexture(TextureTarget.Texture2D, normalizedLinearDepth);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32f, width, height, 0, PixelFormat.Red, PixelType.Float, IntPtr.Zero);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, normalizedDepthFramebuffer);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, normalizedLinearDepth, 0);
        CheckFramebufferComplete("Normalized linear depth");

        normalizedDepthWidth = width;
        normalizedDepthHeight = height;
    }

    private void EnsureNormalizedDepthProgram()
    {
        if (normalizedDepthProgram != 0) return;
        normalizedDepthProgram = CreateProgram(FullScreenVertexShader, NormalizedDepthFragmentShader);
        normalizedDepthVao = GL.GenVertexArray();
        GL.UseProgram(normalizedDepthProgram);
        GL.Uniform1(GL.GetUniformLocation(normalizedDepthProgram, "cameraDepth"), 0);
    }

    private static void NearestTextureParameters()
    {
        TextureParameters(TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
    }

    private void ReleaseNormalizedLinearDepth()
    {
        if (normalizedLinearDepth != 0) GL.DeleteTexture(normalizedLinearDepth);
        if (cameraDepthCopy != 0) GL.DeleteTexture(cameraDepthCopy);
        if (cameraDepthFramebuffer != 0) GL.DeleteFramebuffer(cameraDepthFramebuffer);
        if (normalizedDepthFramebuffer != 0) GL.DeleteFramebuffer(normalizedDepthFramebuffer);
        if (normalizedDepthProgram != 0) GL.DeleteProgram(normalizedDepthProgram);
        if (normalizedDepthVao != 0) GL.DeleteVertexArray(normalizedDepthVao);

        normalizedLinearDepth = cameraDepthCopy = 0;
        cameraDepthFramebuffer = normalizedDepthFramebuffer = 0;
        normalizedDepthProgram = normalizedDepthVao = 0;
        normalizedDepthWidth = normalizedDepthHeight = cameraDepthWidth = cameraDepthHeight = 0;
    }
}

using OpenTK.Graphics.OpenGL4;

namespace HeavenTool.Rendering.BFRES;

public sealed partial class BfresRenderer
{
    // gsys_color_buffer: the game's UpdateIndirect(Water) copies the scene color right before Model(OpaWater), Model(XluWater)
    // and Model(Xlu). The captured copy draws into an R11G11B10 float target of half the viewport's size, taking one bilinear
    // sample of the full-size color at each pixel's middle.
    private int sceneColor, sceneColorFramebuffer, sceneColorWidth, sceneColorHeight;

    private int SceneColorMap()
    {
        if (sceneColor != 0) return sceneColor;
        sceneColor = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, sceneColor);
        // Until the first copy, nothing has been drawn.
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R11fG11fB10f, 1, 1, 0, PixelFormat.Rgb, PixelType.Float, new float[3]);
        TextureParameters(TextureMinFilter.Linear);
        return sceneColor;
    }

    private void CopySceneColor()
    {
        if (sceneColor == 0) return;
        if (sceneColorFramebuffer == 0) sceneColorFramebuffer = GL.GenFramebuffer();
        int copyWidth = Math.Max(1, hdrWidth / 2), copyHeight = Math.Max(1, hdrHeight / 2);
        if (sceneColorWidth != copyWidth || sceneColorHeight != copyHeight)
        {
            // Resize the existing texture object: native sampler bindings keep its identity across viewport changes.
            GL.BindTexture(TextureTarget.Texture2D, sceneColor);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R11fG11fB10f, copyWidth, copyHeight, 0, PixelFormat.Rgb, PixelType.Float, IntPtr.Zero);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, sceneColorFramebuffer);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, sceneColor, 0);
            CheckFramebufferComplete("Scene color copy");
            sceneColorWidth = copyWidth;
            sceneColorHeight = copyHeight;
        }
        GL.Disable(EnableCap.FramebufferSrgb);
        // A multisampled target can't be scaled while it's resolved, so resolve it into hdrColor first; the end of the
        // frame resolves the finished scene over it again.
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, hdrFramebuffer);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, resolveFramebuffer);
        GL.BlitFramebuffer(0, 0, hdrWidth, hdrHeight, 0, 0, hdrWidth, hdrHeight, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, resolveFramebuffer);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, sceneColorFramebuffer);
        GL.BlitFramebuffer(0, 0, hdrWidth, hdrHeight, 0, 0, copyWidth, copyHeight, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, hdrFramebuffer);
    }

    private void ReleaseSceneColor()
    {
        if (sceneColor != 0) GL.DeleteTexture(sceneColor);
        if (sceneColorFramebuffer != 0) GL.DeleteFramebuffer(sceneColorFramebuffer);
        sceneColor = sceneColorFramebuffer = sceneColorWidth = sceneColorHeight = 0;
    }
}

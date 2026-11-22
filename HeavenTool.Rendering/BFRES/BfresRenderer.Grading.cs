using HeavenTool.Shaders;
using OpenTK.Graphics.OpenGL4;
using System.Buffers.Binary;

namespace HeavenTool.Rendering.BFRES;

public sealed partial class BfresRenderer
{
    // agl::pfx::ColorCorrection's volume: 8³, sampled at mapped*(1-1/N)+.5/N.
    private const int GradingSize = 8;
    private int gradingProgram, gradingTexture;
    private AcnhColorGrading? gradingSource;

    // Bakes the grading volume with the game's own color_correction_map program, which writes one slice per render target.
    private bool PrepareGrading()
    {
        var grading = lighting.ColorGrading;
        if (grading == gradingSource) return gradingTexture != 0;
        if (gradingTexture != 0) GL.DeleteTexture(gradingTexture);
        gradingTexture = 0;
        gradingSource = grading;
        if (grading == null) return false;

        var fragment = grading.Map.ReadVariation().Fragment ?? throw new InvalidDataException("color_correction_map has no fragment stage.");
        GL.GetInteger(GetPName.MaxDrawBuffers, out int maxDrawBuffers);
        if (maxDrawBuffers < GradingSize) throw new NotSupportedException("Color grading needs eight render targets.");
        if (gradingProgram == 0)
        {
            gradingProgram = CreateProgram("""
                #version 440 core
                void main() { gl_Position=vec4(vec2((gl_VertexID<<1)&2,gl_VertexID&2)*2.0-1.0,0,1); }
                """, NativeShaderTranslator.TranslateStage(fragment));
            GL.UseProgram(gradingProgram);
            int scale = GL.GetUniformLocation(gradingProgram, "fp_renderScale");
            if (scale >= 0) GL.Uniform1(scale, 1f);
        }

        // fp_c3: [0].z the first slice, [1] the volume size and its texel step.
        var slices = new byte[ParkUniforms.BufferSize];
        BinaryPrimitives.WriteSingleLittleEndian(slices.AsSpan(16), GradingSize);
        BinaryPrimitives.WriteSingleLittleEndian(slices.AsSpan(20), 1f / (GradingSize - 1));
        var filters = new byte[ParkUniforms.BufferSize];
        grading.Filters.CopyTo(filters, 0);
        int[] buffers = [CreateBuffer(ParkUniforms.Constants(fragment)), CreateBuffer(slices), CreateBuffer(filters)];
        int framebuffer = GL.GenFramebuffer();
        int[] viewport = new int[4];
        GL.GetInteger(GetPName.Viewport, viewport);
        try
        {
            gradingTexture = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture3D, gradingTexture);
            GL.TexImage3D(TextureTarget.Texture3D, 0, PixelInternalFormat.Rgba16f, GradingSize, GradingSize, GradingSize, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            foreach (var wrap in new[] { TextureParameterName.TextureWrapS, TextureParameterName.TextureWrapT, TextureParameterName.TextureWrapR })
                GL.TexParameter(TextureTarget.Texture3D, wrap, (int)TextureWrapMode.ClampToEdge);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
            var targets = new DrawBuffersEnum[GradingSize];
            for (int slice = 0; slice < GradingSize; slice++)
            {
                GL.FramebufferTextureLayer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0 + slice, gradingTexture, 0, slice);
                targets[slice] = DrawBuffersEnum.ColorAttachment0 + slice;
            }
            GL.DrawBuffers(GradingSize, targets);
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                throw new InvalidOperationException("Color-grading framebuffer is incomplete.");

            GL.Viewport(0, 0, GradingSize, GradingSize);
            GL.UseProgram(gradingProgram);
            for (int binding = 0; binding < buffers.Length; binding++)
                GL.BindBufferBase(BufferRangeTarget.UniformBuffer, binding, buffers[binding]);
            GL.BindVertexArray(toneVao);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }
        catch
        {
            if (gradingTexture != 0) GL.DeleteTexture(gradingTexture);
            gradingTexture = 0;
            throw;
        }
        finally
        {
            GL.Viewport(viewport[0], viewport[1], viewport[2], viewport[3]);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, outputFramebuffer);
            GL.DeleteFramebuffer(framebuffer);
            foreach (int buffer in buffers) GL.DeleteBuffer(buffer);
        }
        return true;
    }

    private void ReleaseGrading()
    {
        if (gradingProgram != 0) GL.DeleteProgram(gradingProgram);
        if (gradingTexture != 0) GL.DeleteTexture(gradingTexture);
        gradingProgram = gradingTexture = 0;
        gradingSource = null;
    }
}

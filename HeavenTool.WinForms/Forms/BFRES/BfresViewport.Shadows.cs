using HeavenTool.Shaders;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;

namespace HeavenTool.Forms.BFRES;

internal sealed partial class BfresViewport
{
    private const int ShadowSize = 2048;
    // The light is fixed to the scene, so the map is redrawn only when the scene, lighting or visibility changes.
    private int shadowMap, shadowFramebuffer, shadowContext;
    private bool shadowDirty = true;
    private Matrix4 shadowMatrix = Matrix4.Identity;

    private int ShadowMap()
    {
        if (shadowMap != 0) return shadowMap;
        shadowMap = GL.GenTexture(); GL.BindTexture(TextureTarget.Texture2D, shadowMap);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32f, ShadowSize, ShadowSize, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        // Outside the map counts as lit.
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, [1f, 1, 1, 1]);
        // The game shaders count a passing comparison as lit.
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int)TextureCompareMode.CompareRefToTexture);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, (int)DepthFunction.Lequal);
        shadowFramebuffer = GL.GenFramebuffer(); GL.BindFramebuffer(FramebufferTarget.Framebuffer, shadowFramebuffer);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, shadowMap, 0);
        GL.DrawBuffer(DrawBufferMode.None); GL.ReadBuffer(ReadBufferMode.None);
        if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
            throw new InvalidOperationException("Shadow map framebuffer is incomplete.");
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        shadowContext = CreateBuffer(new byte[ParkUniforms.BufferSize]);
        return shadowMap;
    }

    private void RenderShadowMap()
    {
        if (!shadowDirty || lighting == null || shadowMap == 0) return;
        shadowDirty = false;
        // Fit an orthographic light frustum around the scene's bounding sphere.
        var direction = new Vector3(lighting.DirectDirection.X, lighting.DirectDirection.Y, lighting.DirectDirection.Z).Normalized();
        float r = pending.Radius;
        var up = MathF.Abs(direction.Y) > .99f ? Vector3.UnitZ : Vector3.UnitY;
        var view = Matrix4.LookAt(pending.Center - direction * r * 2, pending.Center, up);
        var projection = Matrix4.CreateOrthographicOffCenter(-r, r, -r, r, r, r * 3);
        // Maps clip space into the [0, 1] texture and depth range the game shaders sample with.
        shadowMatrix = view * projection * Matrix4.CreateScale(.5f) * Matrix4.CreateTranslation(.5f, .5f, .5f);
        byte[] bytes = ParkUniforms.Context(Numerics(view), Numerics(projection), r, r * 3, 1, Numerics(shadowMatrix), 1f / ShadowSize);
        GL.BindBuffer(BufferTarget.UniformBuffer, shadowContext);
        GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, bytes.Length, bytes);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, shadowFramebuffer);
        GL.Viewport(0, 0, ShadowSize, ShadowSize);
        GL.ColorMask(false, false, false, false);
        GL.DepthMask(true); GL.Clear(ClearBufferMask.DepthBufferBit);
        GL.Enable(EnableCap.DepthTest); GL.DepthFunc(DepthFunction.Lequal);
        GL.Disable(EnableCap.CullFace); GL.Disable(EnableCap.Blend);
        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        GL.Enable(EnableCap.PolygonOffsetFill); GL.PolygonOffset(2, 4);
        foreach (var mesh in meshes)
        {
            if (hidden.Contains(mesh.Source) || mesh.Material.Profile.Translucent || !nativeMeshes.TryGetValue(mesh.Source, out var native)) continue;
            DrawNativeMesh(mesh, native, true);
        }
        GL.Disable(EnableCap.PolygonOffsetFill);
        GL.ColorMask(true, true, true, true);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private void ReleaseShadows()
    {
        if (shadowMap != 0) GL.DeleteTexture(shadowMap);
        if (shadowFramebuffer != 0) GL.DeleteFramebuffer(shadowFramebuffer);
        if (shadowContext != 0) GL.DeleteBuffer(shadowContext);
        shadowMap = shadowFramebuffer = shadowContext = 0;
        shadowDirty = true;
    }
}

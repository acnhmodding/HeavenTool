using HeavenTool.Shaders;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Runtime.InteropServices;

namespace HeavenTool.Rendering.BFRES;

// The game's ambient occlusion, which its floor and wall shaders add together: a world map of the scene's heights seen from
// above, and a screen-space one of what the camera sees.
public sealed partial class BfresRenderer
{
    private const int WorldAoSize = 512;
    // How far the heights spread when blurred, in world units: the reach of the darkening around a model.
    private const float WorldAoSpread = 2.5f;
    private bool occlusion;
    // The world map is drawn once per scene, like the shadow map: heights from above into worldAoDepth, then blurred across
    // into worldAoMaps[0] and down into worldAoMaps[1], which the shaders sample.
    private int worldAoDepth, worldAoDepthFramebuffer, worldAoContext;
    private readonly int[] worldAoMaps = new int[2], worldAoFramebuffers = new int[2];
    private bool worldAoDirty = true;
    // gsys_user0 rows [0] and [1], which take a world position to the world map's texture coordinates.
    private System.Numerics.Vector4 worldAoU, worldAoV;
    private int blurProgram, blurVao;

    /// <summary>
    /// Whether the native shaders get the game's ambient occlusion, which darkens what is under or beside something else, such
    /// as a room's floor under its furniture and along its walls.
    /// </summary>
    public bool AmbientOcclusion
    {
        get => occlusion;
        set
        {
            if (occlusion == value) return;
            occlusion = value;
            // Meshes pick their textures when they load.
            nativeDirty = nativePending.Count > 0;
            worldAoDirty = lightingDirty = true;
            Invalidate();
        }
    }

    // The studio blocks, with the world map's rows when it is on.
    private byte[] StudioBlock(string name)
    {
        byte[] bytes = ParkUniforms.StudioBlock(name, lighting);
        if (occlusion && name == "gsys_user0")
        {
            MemoryMarshal.Write(bytes.AsSpan(0), in worldAoU);
            MemoryMarshal.Write(bytes.AsSpan(16), in worldAoV);
        }
        return bytes;
    }

    private static int CreateTarget(PixelInternalFormat format, int size, out int framebuffer)
    {
        int texture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, texture);
        bool depth = format == PixelInternalFormat.DepthComponent32f;
        GL.TexImage2D(TextureTarget.Texture2D, 0, format, size, size, 0, depth ? PixelFormat.DepthComponent : PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
        TextureParameters(TextureMinFilter.Linear);
        framebuffer = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, depth ? FramebufferAttachment.DepthAttachment : FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, texture, 0);
        if (depth) { GL.DrawBuffer(DrawBufferMode.None); GL.ReadBuffer(ReadBufferMode.None); }
        return texture;
    }

    private int WorldAoMap()
    {
        if (worldAoMaps[1] != 0) return worldAoMaps[1];
        worldAoDepth = CreateTarget(PixelInternalFormat.DepthComponent32f, WorldAoSize, out worldAoDepthFramebuffer);
        // Every channel holds the height, as the shaders read it from green.
        for (int i = 0; i < 2; i++) worldAoMaps[i] = CreateTarget(PixelInternalFormat.Rgba16f, WorldAoSize, out worldAoFramebuffers[i]);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, outputFramebuffer);
        worldAoContext = CreateBuffer(new byte[ParkUniforms.BufferSize]);
        return worldAoMaps[1];
    }

    private void RenderWorldAo()
    {
        if (!occlusion || !worldAoDirty || worldAoMaps[1] == 0) return;
        worldAoDirty = false;
        // Straight down onto a square around the scene's bounding sphere.
        float r = pending.Radius;
        var view = Matrix4.LookAt(pending.Center + Vector3.UnitY * r * 2, pending.Center, -Vector3.UnitZ);
        var projection = Matrix4.CreateOrthographicOffCenter(-r, r, -r, r, r, r * 3);
        var map = view * projection * Matrix4.CreateScale(.5f) * Matrix4.CreateTranslation(.5f, .5f, .5f);
        worldAoU = new(map.M11, map.M21, map.M31, map.M41);
        worldAoV = new(map.M12, map.M22, map.M32, map.M42);
        lightingDirty = true;
        byte[] bytes = ParkUniforms.Context(Numerics(view), Numerics(projection), r, r * 3, new(WorldAoSize), Numerics(shadowMatrix), 1f / ShadowSize);
        GL.BindBuffer(BufferTarget.UniformBuffer, worldAoContext);
        GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, bytes.Length, bytes);
        DrawDepth(worldAoDepthFramebuffer, WorldAoSize, WorldAoSize, worldAoContext);
        // The view looks straight down, so a map depth is a height alone.
        Blur(worldAoDepth, worldAoMaps[0], worldAoFramebuffers, WorldAoSize, WorldAoSize, WorldAoSpread / (2 * r), new(1 / map.M23, -map.M43 / map.M23));
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, outputFramebuffer);
    }

    // World-AO depth excludes authored shadow-only geometry and uses each material's Z-only variant.
    private void DrawDepth(int framebuffer, int w, int h, int context)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffer);
        GL.Viewport(0, 0, w, h);
        GL.ColorMask(false, false, false, false);
        GL.DepthMask(true);
        GL.Clear(ClearBufferMask.DepthBufferBit);
        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.Blend);
        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        foreach (var mesh in meshes)
        {
            if (!Shown(mesh.Source) || mesh.Source.Material.IsShadowOnly || mesh.Material.Profile.Translucent
                || !nativeDepthMeshes.TryGetValue(mesh.Source, out var native)) continue;
            DrawNativeMesh(mesh, native, context == nativeContext ? 0 : context);
        }
        GL.ColorMask(true, true, true, true);
    }

    private const string FullScreenVertexShader = """
        #version 330 core
        out vec2 uv;
        void main() { uv=vec2((gl_VertexID<<1)&2,gl_VertexID&2); gl_Position=vec4(uv*2.0-1.0,0,1); }
        """;

    private int BlurVao() => blurVao != 0 ? blurVao : blurVao = GL.GenVertexArray();

    // A Gaussian blur of source across into middle, drawn by framebuffers[0], then down into framebuffers[1]; spread is its
    // deviation in texture coordinates across. depthToHeight, when given, turns the source's depths into heights first.
    private void Blur(int source, int middle, int[] framebuffers, int w, int h, float spread, Vector2? depthToHeight)
    {
        if (blurProgram == 0)
        {
            blurProgram = CreateProgram(FullScreenVertexShader, """
                #version 330 core
                in vec2 uv; out vec4 color;
                // tap: one tap's offset, an eighth of the deviation.
                uniform sampler2D source; uniform vec2 tap, depthToHeight; uniform bool fromDepth;
                void main() {
                    float sum=0.0, weights=0.0;
                    for (int i=-24; i<=24; i++) {
                        float weight=exp(-float(i*i)/128.0), value=texture(source,uv+tap*float(i)).r;
                        sum+=weight*(fromDepth ? value*depthToHeight.x+depthToHeight.y : value); weights+=weight;
                    }
                    color=vec4(sum/weights);
                }
                """);
            GL.UseProgram(blurProgram);
            GL.Uniform1(GL.GetUniformLocation(blurProgram, "source"), 0);
        }
        GL.Viewport(0, 0, w, h);
        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.Blend);
        GL.Disable(EnableCap.FramebufferSrgb);
        GL.UseProgram(blurProgram);
        GL.Uniform2(GL.GetUniformLocation(blurProgram, "depthToHeight"), depthToHeight ?? default);
        GL.BindVertexArray(BlurVao());
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindSampler(0, 0);
        float aspect = w / (float)h;
        for (int pass = 0; pass < 2; pass++)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, framebuffers[pass]);
            GL.Uniform2(GL.GetUniformLocation(blurProgram, "tap"), pass == 0 ? new Vector2(spread / 8, 0) : new Vector2(0, spread * aspect / 8));
            GL.Uniform1(GL.GetUniformLocation(blurProgram, "fromDepth"), pass == 0 && depthToHeight != null ? 1 : 0);
            GL.BindTexture(TextureTarget.Texture2D, pass == 0 ? source : middle);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
        }
    }

    private void ReleaseOcclusion()
    {
        ReleaseSsao();
        foreach (int texture in worldAoMaps.Append(worldAoDepth)) if (texture != 0) GL.DeleteTexture(texture);
        foreach (int framebuffer in worldAoFramebuffers.Append(worldAoDepthFramebuffer)) if (framebuffer != 0) GL.DeleteFramebuffer(framebuffer);
        if (worldAoContext != 0) GL.DeleteBuffer(worldAoContext);
        if (blurProgram != 0) GL.DeleteProgram(blurProgram);
        if (blurVao != 0) GL.DeleteVertexArray(blurVao);
        Array.Clear(worldAoMaps);
        Array.Clear(worldAoFramebuffers);
        worldAoDepth = worldAoDepthFramebuffer = worldAoContext = blurProgram = blurVao = 0;
        worldAoDirty = true;
    }
}

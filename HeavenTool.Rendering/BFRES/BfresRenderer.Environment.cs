using HeavenTool.IO.AcnhEnvironment;
using HeavenTool.Shaders;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System.Buffers.Binary;

namespace HeavenTool.Rendering.BFRES;

public sealed partial class BfresRenderer
{
    private const int ProbeFaceSize = 128;
    // The probe's cube, the panorama gsys_cube_map reads in its place, and their programs: DrawEnvMap, BlurLow, Blur, BlurHigh.
    private int probeCube, probeDepth, probeFramebuffer, probeContext, probeTexture;
    private int[] probePrograms = [];
    private AcnhPanoramaEnvironment? probeSource;
    private bool probeDirty = true, probeActive;

    // Captures the room the way the game's CubeMapMgr does and filters it with the Park_Proc chain of 71015224b0.
    private void RenderEnvironmentProbe()
    {
        if (!probeDirty) return;
        probeDirty = false;
        var probe = lighting.EnvironmentProbe;
        probeActive = probe != null && meshes.Any(mesh => !hidden.Contains(mesh.Source) && nativeCubeMeshes.ContainsKey(mesh.Source));
        if (!probeActive) return;
        var panorama = probe!.Panorama;
        if (panorama != probeSource) CreateProbeTargets(panorama);

        int[] viewport = new int[4];
        GL.GetInteger(GetPName.Viewport, viewport);
        try
        {
            CaptureProbeCube(probe);
            FilterProbePanorama(panorama);
        }
        finally
        {
            GL.Viewport(viewport[0], viewport[1], viewport[2], viewport[3]);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, outputFramebuffer);
        }
    }

    private void CreateProbeTargets(AcnhPanoramaEnvironment panorama)
    {
        ReleaseProbe();
        probeSource = panorama;
        probePrograms = [.. new[] { panorama.DrawEnvMap, panorama.BlurLow, panorama.Blur, panorama.BlurHigh }.Select(program =>
        {
            var fragment = program.ReadVariation().Fragment ?? throw new InvalidDataException($"{program.Name} has no fragment stage.");
            int id = CreateProgram("""
                #version 440 core
                void main() { gl_Position=vec4(vec2((gl_VertexID<<1)&2,gl_VertexID&2)*2.0-1.0,0,1); }
                """, NativeShaderTranslator.TranslateStage(fragment));
            GL.UseProgram(id);
            int scale = GL.GetUniformLocation(id, "fp_renderScale");
            if (scale >= 0) GL.Uniform1(scale, 1f);
            return id;
        })];

        probeCube = GL.GenTexture();
        GL.BindTexture(TextureTarget.TextureCubeMap, probeCube);
        for (int face = 0; face < 6; face++)
            GL.TexImage2D(TextureTarget.TextureCubeMapPositiveX + face, 0, PixelInternalFormat.Rgba16f, ProbeFaceSize, ProbeFaceSize, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMaxLevel, 0);
        foreach (var wrap in new[] { TextureParameterName.TextureWrapS, TextureParameterName.TextureWrapT, TextureParameterName.TextureWrapR })
            GL.TexParameter(TextureTarget.TextureCubeMap, wrap, (int)TextureWrapMode.ClampToEdge);
        probeDepth = GL.GenRenderbuffer();
        GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, probeDepth);
        GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, RenderbufferStorage.DepthComponent24, ProbeFaceSize, ProbeFaceSize);

        probeTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, probeTexture);
        for (int level = 0; level < panorama.Levels; level++)
            GL.TexImage2D(TextureTarget.Texture2D, level, PixelInternalFormat.Rgba16f, Math.Max(1, panorama.Width >> level), Math.Max(1, panorama.Height >> level),
                0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, panorama.Levels - 1);
        TextureParameters(TextureMinFilter.LinearMipmapLinear);
        probeFramebuffer = GL.GenFramebuffer();
        probeContext = CreateBuffer(new byte[ParkUniforms.BufferSize]);
    }

    private void CaptureProbeCube(AcnhEnvironmentProbe probe)
    {
        // DrawEnvMap reads the cube at (x, y, -z), so each face holds the world mirrored in z. The mirror turns the winding round.
        (Vector3 Forward, Vector3 Up)[] faces = [(Vector3.UnitX, -Vector3.UnitY), (-Vector3.UnitX, -Vector3.UnitY), (Vector3.UnitY, Vector3.UnitZ),
            (-Vector3.UnitY, -Vector3.UnitZ), (Vector3.UnitZ, -Vector3.UnitY), (-Vector3.UnitZ, -Vector3.UnitY)];
        var position = new Vector3(probe.Position.X, probe.Position.Y, probe.Position.Z);
        var projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver2, 1, probe.Near, probe.Far);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, probeFramebuffer);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, probeDepth);
        GL.Viewport(0, 0, ProbeFaceSize, ProbeFaceSize);
        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        GL.FrontFace(FrontFaceDirection.Cw);
        try
        {
            for (int face = 0; face < 6; face++)
            {
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.TextureCubeMapPositiveX + face, probeCube, 0);
                if (face == 0 && GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                    throw new InvalidOperationException("Environment probe framebuffer is incomplete.");
                var view = Matrix4.CreateTranslation(-position) * Matrix4.CreateScale(1, 1, -1) * Matrix4.LookAt(Vector3.Zero, faces[face].Forward, faces[face].Up);
                byte[] bytes = ParkUniforms.Context(Numerics(view), Numerics(projection), probe.Near, probe.Far, new(ProbeFaceSize),
                    Numerics(shadowMatrix), 1f / ShadowSize);
                GL.BindBuffer(BufferTarget.UniformBuffer, probeContext);
                GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, bytes.Length, bytes);
                GL.ClearColor(0, 0, 0, 0);
                GL.DepthMask(true);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                // 710398f890: Model(CubeMap/Opa+AlphaMask), then Model(CubeMap/Xlu).
                foreach (bool translucent in new[] { false, true })
                    foreach (var mesh in meshes)
                    {
                        if (mesh.Material.Profile.Translucent != translucent || hidden.Contains(mesh.Source)
                            || !nativeCubeMeshes.TryGetValue(mesh.Source, out var native)) continue;
                        ApplyRasterState(mesh.Material.Profile);
                        DrawNativeMesh(mesh, native, probeContext);
                    }
            }
        }
        finally { GL.FrontFace(FrontFaceDirection.Ccw); }
    }

    private void FilterProbePanorama(AcnhPanoramaEnvironment panorama)
    {
        GL.Disable(EnableCap.DepthTest); GL.Disable(EnableCap.Blend); GL.Disable(EnableCap.CullFace); GL.Disable(EnableCap.FramebufferSrgb);
        GL.DepthMask(false);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, probeFramebuffer);
        GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, 0);
        GL.BindVertexArray(toneVao != 0 ? toneVao : toneVao = GL.GenVertexArray());
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindSampler(0, 0);
        var sizes = new byte[ParkUniforms.BufferSize];
        int sizeBuffer = CreateBuffer(sizes);
        var constants = probePrograms.Skip(1).Select((_, i) => CreateBuffer(ParkUniforms.Constants(
            (i switch { 0 => panorama.BlurLow, 1 => panorama.Blur, _ => panorama.BlurHigh }).ReadVariation().Fragment!))).ToArray();
        try
        {
            // fp_c3[0]: the source and target sizes; DrawEnvMap's source is the panorama itself.
            void Pass(int program, int constantBuffer, int target, int source, int sourceTexture, TextureTarget sourceTarget)
            {
                float Size(int full, int level) => Math.Max(1, full >> level);
                BinaryPrimitives.WriteSingleLittleEndian(sizes.AsSpan(0), Size(panorama.Width, source));
                BinaryPrimitives.WriteSingleLittleEndian(sizes.AsSpan(4), Size(panorama.Height, source));
                BinaryPrimitives.WriteSingleLittleEndian(sizes.AsSpan(8), Size(panorama.Width, target));
                BinaryPrimitives.WriteSingleLittleEndian(sizes.AsSpan(12), Size(panorama.Height, target));
                GL.BindBuffer(BufferTarget.UniformBuffer, sizeBuffer);
                GL.BufferSubData(BufferTarget.UniformBuffer, IntPtr.Zero, 16, sizes);
                GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, probeTexture, target);
                GL.Viewport(0, 0, (int)Size(panorama.Width, target), (int)Size(panorama.Height, target));
                GL.UseProgram(program);
                if (constantBuffer != 0) GL.BindBufferBase(BufferRangeTarget.UniformBuffer, 0, constantBuffer);
                GL.BindBufferBase(BufferRangeTarget.UniformBuffer, constantBuffer != 0 ? 1 : 0, sizeBuffer);
                GL.BindTexture(sourceTarget, sourceTexture);
                // 7101523750 clamps the source's LOD to its one level; limiting the texture to it also keeps the target level out of reach.
                if (sourceTarget == TextureTarget.Texture2D)
                {
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, source);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, source);
                }
                GL.DrawArrays(PrimitiveType.Triangles, 0, 3);
            }

            Pass(probePrograms[0], 0, 0, 0, probeCube, TextureTarget.TextureCubeMap);
            int levels = panorama.Levels;
            // Modes 0, 1 and 2 pick DrawEnvBlurLow, DrawEnvBlur and DrawEnvBlurHi; the upward pass reads the level below.
            for (int level = 1; level < levels; level++)
            {
                int mode = Math.Min(level - 1, 2);
                Pass(probePrograms[1 + mode], constants[mode], level, level - 1, probeTexture, TextureTarget.Texture2D);
            }
            for (int level = 2; level < levels - 1; level++)
                Pass(probePrograms[2], constants[1], level, level + 1, probeTexture, TextureTarget.Texture2D);
        }
        finally
        {
            GL.BindTexture(TextureTarget.Texture2D, probeTexture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, panorama.Levels - 1);
            GL.DeleteBuffer(sizeBuffer);
            foreach (int buffer in constants) GL.DeleteBuffer(buffer);
            GL.DepthMask(true);
        }
    }

    // The panorama gsys_cube_map reads: the probe's in the main pass, and the studio's while capturing the probe itself.
    private int EnvironmentTexture(int bound, int passContext) =>
        probeActive && passContext == 0 && bound == environmentTexture && environmentTexture != 0 ? probeTexture : bound;

    private void ReleaseProbe()
    {
        foreach (int id in probePrograms) GL.DeleteProgram(id);
        if (probeCube != 0) GL.DeleteTexture(probeCube);
        if (probeTexture != 0) GL.DeleteTexture(probeTexture);
        if (probeDepth != 0) GL.DeleteRenderbuffer(probeDepth);
        if (probeFramebuffer != 0) GL.DeleteFramebuffer(probeFramebuffer);
        if (probeContext != 0) GL.DeleteBuffer(probeContext);
        probeCube = probeTexture = probeDepth = probeFramebuffer = probeContext = 0;
        probePrograms = [];
        probeSource = null;
        probeActive = false;
        probeDirty = true;
    }
}

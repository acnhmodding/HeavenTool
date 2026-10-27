using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace HeavenTool.Rendering.BFRES;

public sealed partial class BfresRenderer
{
    private const float GroundFadeDistance = 1.8f;
    private int groundProgram, groundVao, groundVbo, groundMatrix, groundCenter, groundFade;

    // The BFRES scene is Y-up: the ground plane is X/Z at the world origin.
    private void DrawGround(Matrix4 matrix)
    {
        if (groundProgram == 0)
        {
            groundProgram = CreateProgram("""
                #version 330 core
                layout(location=0) in vec3 position;
                layout(location=1) in vec3 tint;
                uniform mat4 viewProjection;
                out vec3 vertexColor;
                out vec2 worldPosition;
                void main() { gl_Position=viewProjection*vec4(position,1); vertexColor=tint; worldPosition=position.xz; }
                """, """
                #version 330 core
                in vec3 vertexColor;
                in vec2 worldPosition;
                uniform vec2 gridCenter;
                uniform float fadeRadius;
                out vec4 color;
                void main() {
                    float fade=1.0-smoothstep(fadeRadius*0.35,fadeRadius,length(worldPosition-gridCenter));
                    color=vec4(vertexColor,0.65*fade);
                }
                """);
            groundMatrix = GL.GetUniformLocation(groundProgram, "viewProjection");
            groundCenter = GL.GetUniformLocation(groundProgram, "gridCenter");
            groundFade = GL.GetUniformLocation(groundProgram, "fadeRadius");
            groundVao = GL.GenVertexArray();
            groundVbo = GL.GenBuffer();
            GL.BindVertexArray(groundVao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, groundVbo);
            GL.EnableVertexAttribArray(0); GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 24, 0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 24, 12);
        }

        // Keep the lines useful at different zoom levels and anchor them to world coordinates.
        float spacing = MathF.Pow(10, MathF.Floor(MathF.Log10(Math.Max(distance / 8, .000001f))));
        double gridX = Math.Floor(center.X / spacing), gridZ = Math.Floor(center.Z / spacing);
        const int extent = 128;
        float minX = (float)((gridX - extent) * spacing), maxX = (float)((gridX + extent) * spacing);
        float minZ = (float)((gridZ - extent) * spacing), maxZ = (float)((gridZ + extent) * spacing);
        var lines = new List<float>((extent * 4 + 2) * 12);
        void Line(float x1, float z1, float x2, float z2, float r, float g, float b)
        {
            lines.AddRange([x1, 0, z1, r, g, b, x2, 0, z2, r, g, b]);
        }
        for (int i = -extent; i <= extent; i++)
        {
            double x = gridX + i, z = gridZ + i;
            if (x != 0)
            {
                float tint = x % 10 == 0 ? .38f : .25f;
                Line((float)(x * spacing), minZ, (float)(x * spacing), maxZ, tint, tint, tint);
            }
            if (z != 0)
            {
                float tint = z % 10 == 0 ? .38f : .25f;
                Line(minX, (float)(z * spacing), maxX, (float)(z * spacing), tint, tint, tint);
            }
        }
        if (minZ <= 0 && maxZ >= 0) Line(minX, 0, maxX, 0, .75f, .18f, .18f);
        if (minX <= 0 && maxX >= 0) Line(0, minZ, 0, maxZ, .2f, .4f, .8f);

        // The scene was tone-mapped into the output target; copy its resolved depth so the
        // ground is hidden by the model instead of being drawn over it.
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, resolveFramebuffer);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, outputFramebuffer);
        GL.BlitFramebuffer(0, 0, width, height, 0, 0, width, height, ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, outputFramebuffer);
        GL.Disable(EnableCap.FramebufferSrgb); GL.Disable(EnableCap.CullFace);
        GL.Enable(EnableCap.DepthTest); GL.DepthFunc(DepthFunction.Lequal); GL.DepthMask(false);
        GL.Enable(EnableCap.Blend); GL.BlendEquation(BlendEquationMode.FuncAdd);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        GL.UseProgram(groundProgram); GL.UniformMatrix4(groundMatrix, false, ref matrix);
        GL.Uniform2(groundCenter, center.X, center.Z);
        GL.Uniform1(groundFade, Math.Max(distance * GroundFadeDistance, spacing * 8));
        GL.BindVertexArray(groundVao); GL.BindBuffer(BufferTarget.ArrayBuffer, groundVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, lines.Count * sizeof(float), lines.ToArray(), BufferUsageHint.DynamicDraw);
        GL.LineWidth(1); GL.DrawArrays(PrimitiveType.Lines, 0, lines.Count / 6);
        GL.DepthMask(true); GL.Disable(EnableCap.Blend);
    }

    private void ReleaseGround()
    {
        if (groundVbo != 0) GL.DeleteBuffer(groundVbo);
        if (groundVao != 0) GL.DeleteVertexArray(groundVao);
        if (groundProgram != 0) GL.DeleteProgram(groundProgram);
        groundVbo = groundVao = groundProgram = 0;
    }
}

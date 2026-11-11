using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using NVector3 = System.Numerics.Vector3;

namespace HeavenTool.Rendering.BFRES;

/// <summary>A point drawn over the scene as a small three-axis cross.</summary>
public readonly record struct BfresMarker(NVector3 Position, NVector3 Color);

public sealed partial class BfresRenderer
{
    private IReadOnlyList<BfresMarker> markers = [];
    private bool markerDirty = true;
    private int markerProgram, markerVao, markerVbo, markerMatrix, markerLineCount;

    /// <summary>Points to draw on top of the scene, in its space; empty for none.</summary>
    public void SetMarkers(IReadOnlyList<BfresMarker> value)
    {
        markers = value;
        markerDirty = true;
        Invalidate();
    }

    private void UploadMarkers()
    {
        var lines = new List<float>();
        float size = pending.Radius * .03f;
        foreach (var marker in markers)
        {
            var p = new Vector3(marker.Position.X, marker.Position.Y, marker.Position.Z);
            var c = new Vector3(marker.Color.X, marker.Color.Y, marker.Color.Z);
            foreach (var axis in (Vector3[])[Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ])
            {
                var a = p - axis * size; var b = p + axis * size;
                lines.AddRange([a.X, a.Y, a.Z, c.X, c.Y, c.Z, b.X, b.Y, b.Z, c.X, c.Y, c.Z]);
            }
        }
        markerLineCount = lines.Count / 6;
        foreach (var marker in markers)
            lines.AddRange([marker.Position.X, marker.Position.Y, marker.Position.Z, marker.Color.X, marker.Color.Y, marker.Color.Z]);
        GL.BindVertexArray(markerVao); GL.BindBuffer(BufferTarget.ArrayBuffer, markerVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, lines.Count * sizeof(float), lines.ToArray(), BufferUsageHint.DynamicDraw);
        markerDirty = false;
    }

    private void DrawMarkers(Matrix4 matrix)
    {
        if (markers.Count == 0) return;
        if (markerProgram == 0)
        {
            markerProgram = CreateProgram("""
                    #version 330 core
                    layout(location=0) in vec3 position; layout(location=1) in vec3 tint;
                    uniform mat4 viewProjection; out vec3 vertexColor;
                    void main() { gl_Position=viewProjection*vec4(position,1); vertexColor=tint; }
                    """, """
                    #version 330 core
                    in vec3 vertexColor; out vec4 color;
                    void main() { color=vec4(vertexColor,1); }
                    """);
            markerMatrix = GL.GetUniformLocation(markerProgram, "viewProjection");
            markerVao = GL.GenVertexArray(); markerVbo = GL.GenBuffer();
            GL.BindVertexArray(markerVao); GL.BindBuffer(BufferTarget.ArrayBuffer, markerVbo);
            GL.EnableVertexAttribArray(0); GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 24, 0);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 24, 12);
        }
        if (markerDirty) UploadMarkers();

        GL.Disable(EnableCap.FramebufferSrgb); GL.Disable(EnableCap.Blend); GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);
        GL.DepthMask(false); GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        GL.UseProgram(markerProgram); GL.UniformMatrix4(markerMatrix, false, ref matrix);
        GL.BindVertexArray(markerVao);
        GL.LineWidth(1); GL.DrawArrays(PrimitiveType.Lines, 0, markerLineCount);
        GL.PointSize(7 * renderScale); GL.DrawArrays(PrimitiveType.Points, markerLineCount, markers.Count); GL.PointSize(1);
        GL.DepthMask(true);
    }

    private void ReleaseMarkers()
    {
        if (markerVbo != 0) GL.DeleteBuffer(markerVbo); if (markerVao != 0) GL.DeleteVertexArray(markerVao);
        if (markerProgram != 0) GL.DeleteProgram(markerProgram);
        markerVbo = markerVao = markerProgram = 0; markerDirty = true;
    }
}

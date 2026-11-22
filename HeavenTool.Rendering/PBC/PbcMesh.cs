using HeavenTool.IO.FileFormats.PBC;
using OpenTK.Mathematics;

namespace HeavenTool.Rendering.PBC;

/// <summary>Surface geometry for a PBC: one triangle per valid file triangle, plus walls where neighbors' edges differ.</summary>
public sealed class PbcMesh
{
    /// <summary>Position, normal and color (RGB 0..1) per vertex; three vertices per triangle.</summary>
    public const int VertexFloats = 9;

    public float[] Surface { get; }
    /// <summary>Line-list positions along every surface triangle's edges.</summary>
    public float[] Grid { get; }
    public Vector3 Min { get; }
    public Vector3 Max { get; }
    public bool IsEmpty => Surface.Length == 0;

    private PbcMesh(float[] surface, float[] grid, Vector3 min, Vector3 max)
    {
        Surface = surface;
        Grid = grid;
        Min = min;
        Max = max;
    }

    public static readonly PbcMesh Empty = new([], [], Vector3.Zero, Vector3.Zero);

    private static readonly TriangleDirection[] Directions = Enum.GetValues<TriangleDirection>();

    public static Vector3 Position(int tileX, int tileZ, (float X, float Z) corner, float height) =>
        new((tileX + corner.X) * PbcGeometry.TileSize, height, (tileZ + corner.Z) * PbcGeometry.TileSize);

    /// <summary>The triangle's world corners in file order (center, corner 1, corner 2).</summary>
    public static (Vector3 Center, Vector3 Corner1, Vector3 Corner2) Corners(PBCFileReader file, int tileX, int tileZ, TriangleDirection triangle)
    {
        var heights = file[tileZ, tileX].HeightMap.Triangles[(int)triangle];
        var (center, corner1, corner2) = PbcGeometry.Corners(triangle);
        return (Position(tileX, tileZ, center, heights.CenterHeight),
            Position(tileX, tileZ, corner1, heights.Corner1Height),
            Position(tileX, tileZ, corner2, heights.Corner2Height));
    }

    public static PbcMesh Build(PBCFileReader file)
    {
        if (PbcGeometry.HeightRange(file) == null) return Empty;
        var surface = new List<float>(file.Width * file.Height * 4 * 3 * VertexFloats);
        var grid = new List<float>(file.Width * file.Height * 4 * 6 * 3);
        Vector3 min = new(float.MaxValue), max = new(float.MinValue);

        static Vector3 Color(TileType material) => Unpack(PbcGeometry.MaterialColor(material));

        for (int z = 0; z < file.Height; z++)
            for (int x = 0; x < file.Width; x++)
            {
                var tile = file[z, x];
                foreach (var direction in Directions)
                {
                    var heights = tile.HeightMap.Triangles[(int)direction];
                    if (!PbcGeometry.IsValid(heights)) continue;
                    var material = tile.Materials[(int)direction];
                    var (a, b, c) = Corners(file, x, z, direction);
                    var normal = Vector3.Cross(c - a, b - a);
                    normal = normal.LengthSquared > 0 ? normal.Normalized() : Vector3.UnitY;
                    if (normal.Y < 0) normal = -normal;
                    AddVertex(surface, a, normal, Color(material));
                    AddVertex(surface, b, normal, Color(material));
                    AddVertex(surface, c, normal, Color(material));
                    AddLine(grid, a, b);
                    AddLine(grid, b, c);
                    AddLine(grid, c, a);
                    min = Vector3.ComponentMin(min, Vector3.ComponentMin(a, Vector3.ComponentMin(b, c)));
                    max = Vector3.ComponentMax(max, Vector3.ComponentMax(a, Vector3.ComponentMax(b, c)));
                }

                // Edges shared inside the tile: each triangle's corner 2 side meets the next triangle's corner 1 side.
                for (int i = 0; i < 4; i++)
                {
                    var first = (TriangleDirection)i;
                    var second = (TriangleDirection)((i + 1) % 4);
                    // File order runs top, left, bottom, right, so the next triangle turns counterclockwise on screen.
                    var (fc, _, f2) = Corners(file, x, z, first);
                    var (sc, s1, _) = Corners(file, x, z, second);
                    AddWall(surface, tile, first, tile, second, fc, f2, sc, s1, Color);
                }
                // Edges shared with the tile above (its bottom) and to the left (its right).
                if (z > 0)
                {
                    var (_, t1, t2) = Corners(file, x, z, TriangleDirection.Top);
                    var (_, b1, b2) = Corners(file, x, z - 1, TriangleDirection.Bottom);
                    AddWall(surface, tile, TriangleDirection.Top, file[z - 1, x], TriangleDirection.Bottom, t2, t1, b1, b2, Color);
                }
                if (x > 0)
                {
                    var (_, l1, l2) = Corners(file, x, z, TriangleDirection.Left);
                    var (_, r1, r2) = Corners(file, x - 1, z, TriangleDirection.Right);
                    AddWall(surface, tile, TriangleDirection.Left, file[z, x - 1], TriangleDirection.Right, l1, l2, r2, r1, Color);
                }
            }
        return new PbcMesh([.. surface], [.. grid], min, max);
    }

    /// <summary>
    /// Closes the gap between two triangles' shared edge when their heights differ there.
    /// <paramref name="a0"/>/<paramref name="a1"/> and <paramref name="b0"/>/<paramref name="b1"/> are the same two map points on each side.
    /// </summary>
    private static void AddWall(List<float> surface, PBCFileReader.Tile tileA, TriangleDirection triangleA, PBCFileReader.Tile tileB, TriangleDirection triangleB,
        Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, Func<TileType, Vector3> color)
    {
        if (!PbcGeometry.IsValid(tileA.HeightMap.Triangles[(int)triangleA]) || !PbcGeometry.IsValid(tileB.HeightMap.Triangles[(int)triangleB])) return;
        if (a0.Y == b0.Y && a1.Y == b1.Y) return;
        // The higher side owns the wall's color.
        bool aHigher = a0.Y + a1.Y >= b0.Y + b1.Y;
        var material = aHigher ? tileA.Materials[(int)triangleA] : tileB.Materials[(int)triangleB];
        var edge = a1 - a0;
        var normal = new Vector3(edge.Z, 0, -edge.X);
        normal = normal.LengthSquared > 0 ? normal.Normalized() : Vector3.UnitX;
        AddVertex(surface, a0, normal, color(material));
        AddVertex(surface, a1, normal, color(material));
        AddVertex(surface, b1, normal, color(material));
        AddVertex(surface, a0, normal, color(material));
        AddVertex(surface, b1, normal, color(material));
        AddVertex(surface, b0, normal, color(material));
    }

    private static void AddVertex(List<float> target, Vector3 position, Vector3 normal, Vector3 color)
    {
        target.Add(position.X); target.Add(position.Y); target.Add(position.Z);
        target.Add(normal.X); target.Add(normal.Y); target.Add(normal.Z);
        target.Add(color.X); target.Add(color.Y); target.Add(color.Z);
    }

    private static void AddLine(List<float> target, Vector3 from, Vector3 to)
    {
        target.Add(from.X); target.Add(from.Y); target.Add(from.Z);
        target.Add(to.X); target.Add(to.Y); target.Add(to.Z);
    }

    private static Vector3 Unpack(uint argb) => new((argb >> 16 & 0xFF) / 255f, (argb >> 8 & 0xFF) / 255f, (argb & 0xFF) / 255f);

    /// <summary>Line-list positions outlining one triangle, or empty when it has no valid heights.</summary>
    public static float[] Outline(PBCFileReader file, int tileX, int tileZ, TriangleDirection triangle) =>
        Outline(file, [(tileX, tileZ, triangle)]);

    /// <summary>Line-list positions outlining every listed triangle that has valid heights.</summary>
    public static float[] Outline(PBCFileReader file, IEnumerable<(int TileX, int TileZ, TriangleDirection Triangle)> triangles)
    {
        var lines = new List<float>();
        foreach (var (tileX, tileZ, triangle) in triangles)
        {
            if (tileX < 0 || tileZ < 0 || tileX >= file.Width || tileZ >= file.Height) continue;
            if (!PbcGeometry.IsValid(file[tileZ, tileX].HeightMap.Triangles[(int)triangle])) continue;
            var (a, b, c) = Corners(file, tileX, tileZ, triangle);
            AddLine(lines, a, b);
            AddLine(lines, b, c);
            AddLine(lines, c, a);
        }
        return [.. lines];
    }

    /// <summary>The nearest surface triangle hit by a ray, ignoring walls, and the distance along the ray to the hit.</summary>
    public static (int TileX, int TileZ, TriangleDirection Triangle, float Distance)? Pick(PBCFileReader file, Vector3 origin, Vector3 direction)
    {
        (int, int, TriangleDirection, float)? best = null;
        float nearest = float.MaxValue;
        for (int z = 0; z < file.Height; z++)
            for (int x = 0; x < file.Width; x++)
                foreach (var triangle in Directions)
                {
                    if (!PbcGeometry.IsValid(file[z, x].HeightMap.Triangles[(int)triangle])) continue;
                    var (a, b, c) = Corners(file, x, z, triangle);
                    if (Intersect(origin, direction, a, b, c) is { } t && t < nearest)
                    {
                        nearest = t;
                        best = (x, z, triangle, t);
                    }
                }
        return best;
    }

    // Möller–Trumbore, two-sided.
    private static float? Intersect(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c)
    {
        var e1 = b - a;
        var e2 = c - a;
        var p = Vector3.Cross(direction, e2);
        float det = Vector3.Dot(e1, p);
        if (MathF.Abs(det) < 1e-8f) return null;
        float inverse = 1 / det;
        var s = origin - a;
        float u = Vector3.Dot(s, p) * inverse;
        if (u < 0 || u > 1) return null;
        var q = Vector3.Cross(s, e1);
        float v = Vector3.Dot(direction, q) * inverse;
        if (v < 0 || u + v > 1) return null;
        float t = Vector3.Dot(e2, q) * inverse;
        return t > 0 ? t : null;
    }
}

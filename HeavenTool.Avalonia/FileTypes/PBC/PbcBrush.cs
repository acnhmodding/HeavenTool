using HeavenTool.IO.FileFormats.PBC;
using HeavenTool.Rendering.PBC;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace HeavenTool.Avalonia.FileTypes.PBC;

/// <summary>Which triangles the paint and fill tools reach. Void triangles have no surface in the 3D view and are skipped.</summary>
public static class PbcBrush
{
    private static readonly TriangleDirection[] Directions = Enum.GetValues<TriangleDirection>();

    private static bool IsSolid(PBCFileReader file, int tileX, int tileZ, TriangleDirection triangle) =>
        tileX >= 0 && tileZ >= 0 && tileX < file.Width && tileZ < file.Height
        && PbcGeometry.IsValid(file[tileZ, tileX].HeightMap.Triangles[(int)triangle]);

    /// <summary>
    /// The paint brush under a hit: the hit triangle alone at size 1, otherwise also every triangle whose center lies,
    /// seen from above, within (size - 1) half tiles of the hit point.
    /// </summary>
    public static List<TilePosition> Footprint(PBCFileReader file, TilePosition hit, Vector3 point, int size)
    {
        var result = new List<TilePosition> { hit };
        if (size <= 1) return result;
        float radius = (size - 1) * PbcGeometry.TileSize / 2;
        int minX = Math.Max(0, (int)MathF.Floor((point.X - radius) / PbcGeometry.TileSize));
        int maxX = Math.Min(file.Width - 1, (int)MathF.Floor((point.X + radius) / PbcGeometry.TileSize));
        int minZ = Math.Max(0, (int)MathF.Floor((point.Z - radius) / PbcGeometry.TileSize));
        int maxZ = Math.Min(file.Height - 1, (int)MathF.Floor((point.Z + radius) / PbcGeometry.TileSize));
        for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
                foreach (var triangle in Directions)
                {
                    var position = new TilePosition(x, z, triangle);
                    if (position == hit || !IsSolid(file, x, z, triangle)) continue;
                    var (center, corner1, corner2) = PbcGeometry.Corners(triangle);
                    float centerX = (x + (center.X + corner1.X + corner2.X) / 3) * PbcGeometry.TileSize;
                    float centerZ = (z + (center.Z + corner1.Z + corner2.Z) / 3) * PbcGeometry.TileSize;
                    float dx = centerX - point.X, dz = centerZ - point.Z;
                    if (dx * dx + dz * dz <= radius * radius) result.Add(position);
                }
        return result;
    }

    /// <summary>
    /// The heights a height brush moves: every triangle height at the hit triangle's corner nearest the hit point and,
    /// from size 2 up, every one whose map point lies, seen from above, within (size - 1) half tiles of the hit point.
    /// Triangles meeting at a map point all move together, so a brush never opens a step of its own.
    /// </summary>
    public static List<HeightPoint> Points(PBCFileReader file, TilePosition hit, Vector3 point, int size)
    {
        var (center, corner1, corner2) = PbcGeometry.Corners(hit.Triangle);
        (float X, float Z) nearest = default;
        float best = float.MaxValue;
        foreach (var (X, Z) in (ReadOnlySpan<(float X, float Z)>)[center, corner1, corner2])
        {
            float x = (hit.TileX + X) * PbcGeometry.TileSize, z = (hit.TileZ + Z) * PbcGeometry.TileSize;
            float distance = (x - point.X) * (x - point.X) + (z - point.Z) * (z - point.Z);
            if (distance < best) { best = distance; nearest = (x, z); }
        }
        float radius = Math.Max(0, size - 1) * PbcGeometry.TileSize / 2;
        // One tile of margin covers the nearest corner, which can sit on the next tile's edge.
        int minX = Math.Max(0, (int)MathF.Floor((Math.Min(point.X, nearest.X) - radius) / PbcGeometry.TileSize) - 1);
        int maxX = Math.Min(file.Width - 1, (int)MathF.Floor((Math.Max(point.X, nearest.X) + radius) / PbcGeometry.TileSize) + 1);
        int minZ = Math.Max(0, (int)MathF.Floor((Math.Min(point.Z, nearest.Z) - radius) / PbcGeometry.TileSize) - 1);
        int maxZ = Math.Min(file.Height - 1, (int)MathF.Floor((Math.Max(point.Z, nearest.Z) + radius) / PbcGeometry.TileSize) + 1);
        var result = new List<HeightPoint>();
        for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
                foreach (var triangle in Directions)
                {
                    if (!IsSolid(file, x, z, triangle)) continue;
                    var (Center, Corner1, Corner2) = PbcGeometry.Corners(triangle);
                    var list = (ReadOnlySpan<(float X, float Z)>)[Center, Corner1, Corner2];
                    for (int i = 0; i < 3; i++)
                    {
                        float worldX = (x + list[i].X) * PbcGeometry.TileSize, worldZ = (z + list[i].Z) * PbcGeometry.TileSize;
                        float dx = worldX - point.X, dz = worldZ - point.Z;
                        bool inside = worldX == nearest.X && worldZ == nearest.Z || radius > 0 && dx * dx + dz * dz <= radius * radius;
                        if (inside) result.Add(new HeightPoint(new TilePosition(x, z, triangle), i));
                    }
                }
        return result;
    }

    /// <summary>
    /// The heights a height brush moves in face mode: all three heights of every triangle the paint brush would reach.
    /// Neighbors outside the brush keep their heights, so the edited faces can step away from them.
    /// </summary>
    public static List<HeightPoint> FacePoints(PBCFileReader file, TilePosition hit, Vector3 point, int size)
    {
        var result = new List<HeightPoint>();
        foreach (var position in Footprint(file, hit, point, size))
            for (int corner = 0; corner < 3; corner++) result.Add(new HeightPoint(position, corner));
        return result;
    }

    /// <summary>The height of the hit triangle's corner nearest the hit point, which the flatten tool levels to.</summary>
    public static float NearestHeight(PBCFileReader file, TilePosition hit, Vector3 point)
    {
        var (a, b, c) = PbcMesh.Corners(file, hit.TileX, hit.TileZ, hit.Triangle);
        static float Distance(Vector3 corner, Vector3 point) => (corner.Xz - point.Xz).LengthSquared;
        var nearest = a;
        if (Distance(b, point) < Distance(nearest, point)) nearest = b;
        if (Distance(c, point) < Distance(nearest, point)) nearest = c;
        return nearest.Y;
    }

    /// <summary>
    /// Every triangle reachable from <paramref name="start"/> through edges it shares, at the same heights on both sides,
    /// with triangles of the same material. A step or cliff between two triangles stops the fill.
    /// </summary>
    public static List<TilePosition> Fill(PBCFileReader file, TilePosition start)
    {
        var result = new List<TilePosition>();
        if (!IsSolid(file, start.TileX, start.TileZ, start.Triangle)) return result;
        var material = Material(file, start);
        var visited = new HashSet<TilePosition> { start };
        var queue = new Queue<TilePosition>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            result.Add(current);
            foreach (var next in Neighbors(current))
            {
                if (visited.Contains(next) || !IsSolid(file, next.TileX, next.TileZ, next.Triangle)) continue;
                if (Material(file, next) != material || !SharesEdge(file, current, next)) continue;
                visited.Add(next);
                queue.Enqueue(next);
            }
        }
        return result;
    }

    private static TileType Material(PBCFileReader file, TilePosition at) => file[at.TileZ, at.TileX].Materials[(int)at.Triangle];

    // File order runs top, left, bottom, right, so each triangle touches the ones before and after it in that order.
    private static IEnumerable<TilePosition> Neighbors(TilePosition at)
    {
        yield return at with { Triangle = (TriangleDirection)(((int)at.Triangle + 1) % 4) };
        yield return at with { Triangle = (TriangleDirection)(((int)at.Triangle + 3) % 4) };
        yield return at.Triangle switch
        {
            TriangleDirection.Top => new TilePosition(at.TileX, at.TileZ - 1, TriangleDirection.Bottom),
            TriangleDirection.Bottom => new TilePosition(at.TileX, at.TileZ + 1, TriangleDirection.Top),
            TriangleDirection.Left => new TilePosition(at.TileX - 1, at.TileZ, TriangleDirection.Right),
            _ => new TilePosition(at.TileX + 1, at.TileZ, TriangleDirection.Left)
        };
    }

    // Neighbors meet on two map points; the surface continues when both points have the same height on each side.
    private static bool SharesEdge(PBCFileReader file, TilePosition a, TilePosition b)
    {
        var (a0, a1, a2) = PbcMesh.Corners(file, a.TileX, a.TileZ, a.Triangle);
        var (b0, b1, b2) = PbcMesh.Corners(file, b.TileX, b.TileZ, b.Triangle);
        int shared = 0;
        foreach (var p in (ReadOnlySpan<Vector3>)[a0, a1, a2])
            foreach (var q in (ReadOnlySpan<Vector3>)[b0, b1, b2])
            {
                if (p.X != q.X || p.Z != q.Z) continue;
                if (p.Y != q.Y) return false;
                shared++;
            }
        return shared >= 2;
    }
}

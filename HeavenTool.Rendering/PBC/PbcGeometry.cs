using HeavenTool.IO.FileFormats.PBC;
using System.ComponentModel;
using System.Reflection;

namespace HeavenTool.Rendering.PBC;

/// <summary>PBC layout, colors and names shared by the editor and the 3D view.</summary>
public static class PbcGeometry
{
    public const float VoidHeight = -10000000;

    /// <summary>World units per tile: the game indexes cells with floor(position / 5).</summary>
    public const float TileSize = 5;

    public static bool IsValidHeight(float height) => float.IsFinite(height) && height != VoidHeight;

    public static bool IsValid(PBCFileReader.TriangleHeights heights) =>
        IsValidHeight(heights.CenterHeight) && IsValidHeight(heights.Corner1Height) && IsValidHeight(heights.Corner2Height);

    /// <summary>
    /// The triangle's corners in tile space (0..1, x right and z down), in file order: center, corner 1, corner 2.
    /// The game's cliff checks pair top.corner2 with left.corner1 at (0,0), left.corner2 with bottom.corner1 at (0,1),
    /// bottom.corner2 with right.corner1 at (1,1), and right.corner2 with top.corner1 at (1,0).
    /// </summary>
    public static ((float X, float Z) Center, (float X, float Z) Corner1, (float X, float Z) Corner2) Corners(TriangleDirection triangle) => triangle switch
    {
        TriangleDirection.Top => ((.5f, .5f), (1, 0), (0, 0)),
        TriangleDirection.Left => ((.5f, .5f), (0, 0), (0, 1)),
        TriangleDirection.Bottom => ((.5f, .5f), (0, 1), (1, 1)),
        TriangleDirection.Right => ((.5f, .5f), (1, 1), (1, 0)),
        _ => throw new ArgumentOutOfRangeException(nameof(triangle))
    };

    public static string CornerName(TriangleDirection triangle, int corner) => (triangle, corner) switch
    {
        (TriangleDirection.Top, 1) or (TriangleDirection.Right, 2) => "top right",
        (TriangleDirection.Top, 2) or (TriangleDirection.Left, 1) => "top left",
        (TriangleDirection.Left, 2) or (TriangleDirection.Bottom, 1) => "bottom left",
        _ => "bottom right"
    };

    /// <summary>Normalized tile coordinates; ties follow the game's x/z diagonal tests.</summary>
    public static TriangleDirection GetTriangleDirection(float x, float z) =>
        z >= x
            ? (x + z >= 1 ? TriangleDirection.Bottom : TriangleDirection.Left)
            : (x + z >= 1 ? TriangleDirection.Right : TriangleDirection.Top);

    private static readonly Dictionary<byte, uint> Colors = new()
    {
        { 0, 0xFF467840 }, { 1, 0xFF80D7C3 }, { 3, 0xFFC0C0C0 }, { 4, 0xFFF0E6AA },
        { 5, 0xFF80D7C3 }, { 6, 0xFFFF8080 }, { 7, 0xFF000000 }, { 8, 0xFF202020 },
        { 9, 0xFFFF0000 }, { 10, 0xFF303030 }, { 12, 0xFF80D7C3 }, { 15, 0xFF80D7C3 },
        { 22, 0xFFC0FF62 }, { 23, 0xFFC09B62 }, { 28, 0xFFFF0000 }, { 29, 0xFFE8DEA2 },
        { 41, 0xFF767A84 }, { 42, 0xFF808593 }, { 43, 0xFF00FFFF }, { 44, 0xFF3E7038 },
        { 45, 0xFF767A84 }, { 46, 0xFF78CFBB }, { 47, 0xFF808000 }, { 49, 0xFFBE6262 },
        { 51, 0xFF209820 }, { 70, 0xFF6D717C }, { 149, 0xFFB3CFFC }, { 150, 0xFF3D77D4 }
    };

    /// <summary>The editor's display color for a material, as 0xAARRGGBB.</summary>
    public static uint MaterialColor(TileType material) => Colors.TryGetValue((byte)material, out var color) ? color : 0xFF800080;

    public static string MaterialName(TileType material) =>
        typeof(TileType).GetField(material.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? material.ToString();

    public static (float Min, float Max)? HeightRange(PBCFileReader file)
    {
        float min = float.MaxValue, max = float.MinValue;
        foreach (var tile in file.Tiles)
            foreach (var heights in tile.HeightMap.Triangles)
                foreach (var value in (ReadOnlySpan<float>)[heights.CenterHeight, heights.Corner1Height, heights.Corner2Height])
                {
                    if (!IsValidHeight(value)) continue;
                    min = Math.Min(min, value);
                    max = Math.Max(max, value);
                }
        return min <= max ? (min, max) : null;
    }
}

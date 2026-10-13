using HeavenTool.IO.FileFormats.PBC;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;

namespace HeavenTool.Forms.PBC;

public static class PBCImageUtilities
{
    public const float VoidHeight = -10000000;

    public static bool IsValidHeight(float height) => float.IsFinite(height) && height != VoidHeight;

    public static PointF[] GetTrianglePoints(RectangleF bounds, TriangleDirection triangle)
    {
        var center = new PointF(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        var topLeft = new PointF(bounds.Left, bounds.Top);
        var topRight = new PointF(bounds.Right, bounds.Top);
        var bottomLeft = new PointF(bounds.Left, bounds.Bottom);
        var bottomRight = new PointF(bounds.Right, bounds.Bottom);
        return triangle switch
        {
            TriangleDirection.Top => [center, topRight, topLeft],
            TriangleDirection.Left => [center, topLeft, bottomLeft],
            TriangleDirection.Bottom => [center, bottomLeft, bottomRight],
            TriangleDirection.Right => [center, bottomRight, topRight],
            _ => throw new ArgumentOutOfRangeException(nameof(triangle))
        };
    }

    // Normalized cell coordinates; ties follow the game's x/z diagonal tests.
    public static TriangleDirection GetTriangleDirection(float x, float z) =>
        z >= x
            ? (x + z >= 1 ? TriangleDirection.Bottom : TriangleDirection.Left)
            : (x + z >= 1 ? TriangleDirection.Right : TriangleDirection.Top);

    private readonly static Dictionary<uint, Color> Colors = new()
    {
        { 0, Color.FromArgb(255, 70, 120, 64) },
        { 1, Color.FromArgb(255, 128, 215, 195) },
        { 3, Color.FromArgb(255, 192, 192, 192) },
        { 4, Color.FromArgb(255, 240, 230, 170) },
        { 5, Color.FromArgb(255, 128, 215, 195) },
        { 6, Color.FromArgb(255, 255, 128, 128) },
        { 7, Color.FromArgb(255, 0, 0, 0) },
        { 8, Color.FromArgb(255, 32, 32, 32) },
        { 9, Color.FromArgb(255, 255, 0, 0) },
        { 10, Color.FromArgb(255, 48, 48, 48) },
        { 12, Color.FromArgb(255, 128, 215, 195) },
        { 15, Color.FromArgb(255, 128, 215, 195) },
        { 22, Color.FromArgb(255, 192, 255, 98) },
        { 23, Color.FromArgb(255, 192, 155, 98) },
        { 28, Color.FromArgb(255, 255, 0, 0) },
        { 29, Color.FromArgb(255, 232, 222, 162) },
        { 41, Color.FromArgb(255, 118, 122, 132) },
        { 42, Color.FromArgb(255, 128, 133, 147) },
        { 43, Color.Cyan },
        { 44, Color.FromArgb(255, 62, 112, 56) },
        { 45, Color.FromArgb(255, 118, 122, 132) },
        { 46, Color.FromArgb(255, 120, 207, 187) },
        { 47, Color.FromArgb(255, 128, 128, 0) },
        { 49, Color.FromArgb(255, 190, 98, 98) },
        { 51, Color.FromArgb(255, 32, 152, 32) },
        { 70, Color.FromArgb(255, 109, 113, 124)},
        { 149, Color.FromArgb(255, 179, 207, 252)},
        { 150, Color.FromArgb(255, 61 , 119, 212)}
    };

    public static readonly Color HEIGHT_DARKER = Color.FromArgb(30, 30, 30);
    public static readonly Color HEIGHT_BRIGHTER = Color.FromArgb(150, 150, 150);

    public static Color GetColor(TileType tileType) => GetColor((byte)tileType);

    public static string GetMaterialName(TileType material) => typeof(TileType).GetField(material.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? material.ToString();

    public static Color GetColor(byte i)
    {
        if (Colors.TryGetValue(i, out var value))
            return value;

        return Color.FromArgb(255, 128, 0, 128);
    }

    public static Color GetHeightColor(float current, float min, float max)
    {
        if (!IsValidHeight(current)) return Color.Black;
        if (min == max) return HEIGHT_DARKER;

        var t = (float)Math.Clamp(((double)current - min) / ((double)max - min), 0, 1);
        return Color.FromArgb(
            (int)(HEIGHT_DARKER.R + t * (HEIGHT_BRIGHTER.R - HEIGHT_DARKER.R)),
            (int)(HEIGHT_DARKER.G + t * (HEIGHT_BRIGHTER.G - HEIGHT_DARKER.G)),
            (int)(HEIGHT_DARKER.B + t * (HEIGHT_BRIGHTER.B - HEIGHT_DARKER.B))
        );
    }
}
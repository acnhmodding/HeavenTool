using HeavenTool.IO.FileFormats.PBC;
using System.Collections.Generic;
using System.Drawing;

namespace HeavenTool.Forms.PBC;

public static class PBCImageUtilities
{
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

    //public static Image GenerateImage(this PBCFileReader pbc, ViewType viewType, int scale, bool grid, bool type)
    //{
    //    var bm = new Bitmap(pbc.Width * scale * 2, pbc.Height * scale * 2);
    //    var gr = Graphics.FromImage(bm);

    //    var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center};
    //    var f = new Font(FontFamily.GenericMonospace, scale / 2, FontStyle.Regular, GraphicsUnit.Pixel);

    //    int heightId = viewType switch
    //    {
    //        ViewType.HeightMap1 => 0,
    //        ViewType.HeightMap2 => 1,
    //        ViewType.HeightMap3 => 2,
    //        _ => -1
    //    };

    //    float? minHeight = null;
    //    float? maxHeight = null;

    //    if (heightId != -1)
    //    {
    //        for (var h = 0; h < pbc.Height; h++)
    //            for (var w = 0; w < pbc.Width; w++)
    //            {
    //                var tileHeight = pbc.Tiles[h, w].HeightMap;
    //                if (tileHeight != null)
    //                {
    //                    foreach (var heightTile in tileHeight.Quadrants)
    //                    {
    //                        if (heightTile.Val2 == -10000000) continue;

    //                        if (minHeight == null || heightTile.Val2 < minHeight)
    //                            minHeight = heightTile.Val2;

    //                        if (maxHeight == null || heightTile.Val2 > maxHeight)
    //                            maxHeight = heightTile.Val2;
    //                    }
    //                }
    //            }
    //    }

    //    for (var h = 0; h < pbc.Height; h++)
    //    {
    //        for (var w = 0; w < pbc.Width; w++)
    //        {
    //            var tile = pbc.Tiles[h, w];

    //            var tileBitmap = new Bitmap(scale * 2, scale * 2);
    //            var tileGraphic = Graphics.FromImage(tileBitmap);

    //            //var tileHeight = tile.GetHeightMap(heightId);
    //            var tileHeight = tile.HeightMap;

    //            for (var y = 0; y < 2; y++)
    //            {
    //                for (var x = 0; x < 2; x++)
    //                {
    //                    var v = tile.Type[y, x];

    //                    if (tileHeight != null && minHeight.HasValue && maxHeight.HasValue)
    //                    {
    //                        //var heightInfo = tileHeight[y, x];
    //                        var heightInfo = tileHeight.Quadrants[y * x].Val2;
    //                        var c = GetHeightColor(heightInfo, minHeight.Value, maxHeight.Value);
    //                        tileGraphic.FillRectangle(new SolidBrush(c), new Rectangle(y * scale, x * scale, scale, scale));

    //                        if (heightInfo > -10000000)
    //                            tileGraphic.DrawString($"{heightInfo}", f, new SolidBrush(ContrastColor(c)), new Rectangle(y * scale, x * scale, scale, scale), sf);
    //                    }
    //                    else
    //                    {
    //                        var c = GetColor(v);
    //                        tileGraphic.FillRectangle(new SolidBrush(c), new Rectangle(y * scale, x * scale, scale, scale));
    //                         tileGraphic.DrawString($"{v}", f, new SolidBrush(ContrastColor(c)), new Rectangle(y * scale, x * scale, scale, scale), sf);
                            
    //                    }
    //                }
    //            }


    //            gr.DrawImage(tileBitmap, w * scale * 2, h * scale * 2);

    //            if (grid)
    //                gr.DrawRectangle(new Pen(Color.Black), new Rectangle(w * scale * 2, h * scale * 2, scale * 2, scale * 2));
                
    //        }
    //    }

    //    return bm;
    //}


    private static Color ContrastColor(Color color)
    {
        var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
        var d = luminance > 0.5 ? 0 : 255;
        return Color.FromArgb(d, d, d);
    }

    public static Color GetColor(TileType tileType) => GetColor((byte) tileType);

    public static Color GetColor(byte i)
    {
        if (Colors.TryGetValue(i, out Color value))
            return value;

        return Color.FromArgb(255, 128, 0, 128);
    }

    public static Color GetHeightColor(float current, float min, float max)
    {
        if (current == -10000000) return Color.Black;
        if (min == max) return HEIGHT_DARKER;

        float t = (current - min) / (max - min);
        return Color.FromArgb(
            (int)(HEIGHT_DARKER.R + t * (HEIGHT_BRIGHTER.R - HEIGHT_DARKER.R)),
            (int)(HEIGHT_DARKER.G + t * (HEIGHT_BRIGHTER.G - HEIGHT_DARKER.G)),
            (int)(HEIGHT_DARKER.B + t * (HEIGHT_BRIGHTER.B - HEIGHT_DARKER.B))
        );
    }
}

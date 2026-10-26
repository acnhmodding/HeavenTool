using HeavenTool.Forms.PBC.Tools;
using HeavenTool.IO.FileFormats.PBC;
using HeavenTool.IO.UndoSystem;
using HeavenTool.Utility;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace HeavenTool.Forms.PBC;

public partial class TileEditor : Control
{
    public class EditorTools(TileEditor parent)
    {
        public CollisionBrushTool CollisionBrush { get; } = new CollisionBrushTool(parent);
        public InspectorTool InspectorTool { get; } = new InspectorTool(parent);
    }

    // FindForm() resolves to a docking wrapper, so the owner supplies its history.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public UndoManager UndoManager { get; set; } = new();

    public EditorTools Tools { get; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TileEditorTool? ActiveTool { get; set; }

    private PBCFileReader? _pbcFile;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public PBCFileReader? PBCFile
    {
        get { return _pbcFile; }
        set
        {
            if (_pbcFile != value)
            {
                Zoom = 10;
                offset = default;
                lastMousePos = null;

            }

            _pbcFile = value;
            UpdateHeight();
        }
    }

    [DefaultValue(10)]
    public int Zoom { get; set; }

    [DefaultValue(true)]
    public bool DisplayGrid { get; set; }

    [DefaultValue(true)]
    public bool ShowType { get; set; }

    public TileType? TileBrush = TileType.Custom1;

    [DefaultValue(ViewType.HeightMap)]
    public ViewType CurrentView { get; set; } = ViewType.HeightMap;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float? MinHeight { get; private set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float? MaxHeight { get; private set; }

    public void UpdateHeight()
    {
        MinHeight = null;
        MaxHeight = null;
        if (PBCFile == null)
        {
            return;
        }

        for (var h = 0; h < PBCFile.Height; h++)
        {
            for (var w = 0; w < PBCFile.Width; w++)
            {
                var tileHeight = PBCFile.Tiles[h, w].HeightMap;
                if (tileHeight != null)
                {
                    foreach (var heightTile in tileHeight.Triangles)
                    {
                        var values = new[] { heightTile.CenterHeight, heightTile.Corner1Height, heightTile.Corner2Height };
                        foreach (var val in values)
                        {
                            if (!PBCImageUtilities.IsValidHeight(val)) continue;
                            if (MinHeight == null || val < MinHeight)
                                MinHeight = val;
                            if (MaxHeight == null || val > MaxHeight)
                                MaxHeight = val;
                        }
                    }
                }
            }
        }
    }

    private Point offset;
    private Point? lastMousePos;

    public delegate void ZoomEventHandler(int zoom);
    public event ZoomEventHandler? ZoomChanged;

    public delegate void TriangleSelectedEventHandler(PBCFileReader.TriangleHeights triangle);
    public event TriangleSelectedEventHandler? TriangleSelected;

    public TileEditor()
    {
        DoubleBuffered = true;

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);

        BackColor = Color.White;

        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;

        Tools = new EditorTools(this);
        ActiveTool = Tools.CollisionBrush;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(BackColor);
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;

        if (PBCFile == null) return;

        using var pen = new Pen(Color.FromArgb(50, 255, 255, 255), 1);
        using var bigGridPen = new Pen(Color.FromArgb(30, 255, 255, 255), 2);

        using var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center };
        using var f = new Font(FontFamily.GenericMonospace, Math.Max(1, Zoom / 2f), FontStyle.Regular, GraphicsUnit.Pixel);

        for (var h = 0; h < PBCFile.Height; h++)
        {
            for (var w = 0; w < PBCFile.Width; w++)
            {
                var tile = PBCFile.Tiles[h, w];
                var tileHeight = tile.HeightMap;
                var bounds = new RectangleF(w * 2 * Zoom + offset.X, h * 2 * Zoom + offset.Y, 2 * Zoom, 2 * Zoom);
                foreach (var triangle in Enum.GetValues<TriangleDirection>())
                {
                    var points = PBCImageUtilities.GetTrianglePoints(bounds, triangle);
                    string? label = null;

                    if (CurrentView == ViewType.HeightMap)
                    {
                        var heightInfo = GetHeightInfo(tileHeight, triangle);
                        if (PBCImageUtilities.IsValidHeight(heightInfo) && MinHeight.HasValue && MaxHeight.HasValue)
                        {
                            var heights = tileHeight.Triangles[(int)triangle];
                            var colors = new[]
                            {
                                PBCImageUtilities.GetHeightColor(heights.CenterHeight, MinHeight.Value, MaxHeight.Value),
                                PBCImageUtilities.GetHeightColor(heights.Corner1Height, MinHeight.Value, MaxHeight.Value),
                                PBCImageUtilities.GetHeightColor(heights.Corner2Height, MinHeight.Value, MaxHeight.Value)
                            };
                            using var brush = new PathGradientBrush(points)
                            {
                                CenterPoint = new PointF((points[0].X + points[1].X + points[2].X) / 3, (points[0].Y + points[1].Y + points[2].Y) / 3),
                                CenterColor = Color.FromArgb((colors[0].R + colors[1].R + colors[2].R) / 3, (colors[0].G + colors[1].G + colors[2].G) / 3, (colors[0].B + colors[1].B + colors[2].B) / 3),
                                SurroundColors = colors
                            };
                            e.Graphics.FillPolygon(brush, points);
                        }
                        else
                        {
                            var color = MinHeight.HasValue && MaxHeight.HasValue
                                ? PBCImageUtilities.GetHeightColor(heightInfo, MinHeight.Value, MaxHeight.Value)
                                : Color.Black;
                            using var brush = new SolidBrush(color);
                            e.Graphics.FillPolygon(brush, points);
                        }
                        if (PBCImageUtilities.IsValidHeight(heightInfo)) label = heightInfo.ToString("0.##");
                    }
                    else
                    {
                        using var brush = new SolidBrush(PBCImageUtilities.GetColor(tile.Materials[(int)triangle]));
                        e.Graphics.FillPolygon(brush, points);
                        label = ((int)tile.Materials[(int)triangle]).ToString();
                    }

                    if (ShowType && label != null)
                    {
                        var center = new PointF((points[0].X + points[1].X + points[2].X) / 3, (points[0].Y + points[1].Y + points[2].Y) / 3);
                        e.Graphics.DrawString(label, f, Brushes.White, center, sf);
                    }

                    if (DisplayGrid)
                        e.Graphics.DrawPolygon(pen, points);
                }

                if (DisplayGrid)
                    e.Graphics.DrawRectangle(bigGridPen, w * 2 * Zoom + offset.X, h * 2 * Zoom + offset.Y, 2 * Zoom, 2 * Zoom);

            }
        }
    }

    internal float GetHeightInfo(PBCFileReader.HeightMap tileHeight, TriangleDirection triangle)
    {
        var tile = tileHeight.Triangles[(int)triangle];

        return PBCImageUtilities.IsValidHeight(tile.CenterHeight) && PBCImageUtilities.IsValidHeight(tile.Corner1Height) && PBCImageUtilities.IsValidHeight(tile.Corner2Height)
                ? (float)(((double)tile.CenterHeight + tile.Corner1Height + tile.Corner2Height) / 3)
                : PBCImageUtilities.VoidHeight;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        Focus();

        ActiveTool?.OnMouseDown(e);

        lastMousePos = e.Location;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        ActiveTool?.OnMouseUp(e);
        lastMousePos = null;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        ActiveTool?.OnMouseMove(e);

        if (e.Button == MouseButtons.Left && ModifierKeys.HasFlag(Keys.Control) && lastMousePos.HasValue)
        {
            var dx = e.X - lastMousePos.Value.X;
            var dy = e.Y - lastMousePos.Value.Y;

            offset.X += dx;
            offset.Y += dy;
            lastMousePos = e.Location;
            Invalidate();
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Zoom = Math.Max(1, Zoom + (e.Delta > 0 ? 1 : -1));
        ZoomChanged?.Invoke(Zoom);
        Invalidate();
    }

    public bool TryGetTilePosition(Point mouse, out TilePosition pos)
    {
        pos = default;

        if (PBCFile == null)
            return false;

        var localX = mouse.X - offset.X;
        var localY = mouse.Y - offset.Y;
        if (localX < 0 || localY < 0 || Zoom <= 0) return false;
        var tileSize = 2 * Zoom;
        var tileX = localX / tileSize;
        var tileY = localY / tileSize;
        var triangle = PBCImageUtilities.GetTriangleDirection((localX % tileSize) / (float)tileSize, (localY % tileSize) / (float)tileSize);

        if (tileX < 0 || tileX >= PBCFile.Width ||
            tileY < 0 || tileY >= PBCFile.Height)
            return false;

        pos = new TilePosition
        {
            TileX = tileX,
            TileY = tileY,
            Triangle = triangle
        };

        return true;
    }

    public void SelectTriangle(PBCFileReader.TriangleHeights triangle) => TriangleSelected?.Invoke(triangle);

}

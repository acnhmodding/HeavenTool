using HeavenTool.Forms.PBC.Tools;
using HeavenTool.IO.FileFormats.PBC;
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

    public UndoManager _localUndoManager = new();
    public UndoManager UndoManager
    {
        get
        {
            if (FindForm() is PBCEditor pbcEditor)
                return pbcEditor.UndoManager;

            return _localUndoManager;
        }
    }

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

    [DefaultValue(LayerView.Layer1)]
    public LayerView LayerView { get; set; } = LayerView.Layer1;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float? MinHeight { get; private set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float? MaxHeight { get; private set; }

    /// <summary>
    /// Updates the minimum and maximum height values based on the current tile data in the associated <see cref="PBCFile"/>.
    /// </summary>
    private void UpdateHeight()
    {
        if (PBCFile == null)
        {
            MinHeight = null;
            MaxHeight = null;
            return;
        }

        for (var h = 0; h < PBCFile.Height; h++)
        {
            for (var w = 0; w < PBCFile.Width; w++)
            {
                var tileHeight = PBCFile.Tiles[h, w].HeightMap;
                if (tileHeight != null)
                {
                    foreach (var heightTile in tileHeight.Quadrants)
                    {
                        var val = LayerView switch
                        {
                            LayerView.Layer0 => heightTile.Layer0,
                            LayerView.Layer1 => heightTile.Layer1,
                            LayerView.Layer2 => heightTile.Layer2,

                            _ => heightTile.Layer1
                        };

                        // Ignore void height
                        if (val == -10000000) continue;

                        if (MinHeight == null || val < MinHeight)
                            MinHeight = val;

                        if (MaxHeight == null || val > MaxHeight)
                            MaxHeight = val;
                    }
                }
            }
        }
    }

    private Point offset;
    private Point? lastMousePos;

    public delegate void ZoomEventHandler(int zoom);
    public event ZoomEventHandler? ZoomChanged;

    public delegate void HeightMapQuadrantSelected(PBCFileReader.Quadrant quadrant);
    public event HeightMapQuadrantSelected? QuadrantSelected;

    public TileEditor()
    {
        DoubleBuffered = true;

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);

        BackColor = Color.White;

        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;

        // Create our tools handler and set the default active tool
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

        var sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center };
        var f = new Font(FontFamily.GenericMonospace, Zoom / 2, FontStyle.Regular, GraphicsUnit.Pixel);

        for (int h = 0; h < PBCFile.Height; h++)
        {
            for (int w = 0; w < PBCFile.Width; w++)
            {
                var tile = PBCFile.Tiles[h, w];
                var tileHeight = tile.HeightMap;
                for (int subY = 0; subY < 2; subY++)   
                {
                    int globalY = h * 2 + subY;
                    for (int subX = 0; subX < 2; subX++)
                    {
                        int globalX = w * 2 + subX;

                        // Render HeightMap
                        if (CurrentView == ViewType.HeightMap && MinHeight.HasValue && MaxHeight.HasValue)
                        {
                            var heightInfo = GetHeightInfo(tileHeight, subY, subX);
                            var c = PBCImageUtilities.GetHeightColor(heightInfo, MinHeight.Value, MaxHeight.Value);
                            using var brush = new SolidBrush(c);
                            e.Graphics.FillRectangle(brush, globalX * Zoom + offset.X, globalY * Zoom + offset.Y, Zoom, Zoom);

                            // Render text if ShowType is true and it's not void
                            if (heightInfo != -10000000 && ShowType)
                                e.Graphics.DrawString(heightInfo.ToString(), f, Brushes.White, new Rectangle(globalX * Zoom + offset.X, globalY * Zoom + offset.Y, Zoom, Zoom));
                        }
                        // Render Tile Color
                        else
                        {
                            using var brush = new SolidBrush(PBCImageUtilities.GetColor(tile.Type[subY, subX]));
                            e.Graphics.FillRectangle(brush, globalX * Zoom + offset.X, globalY * Zoom + offset.Y, Zoom, Zoom);

                            if (ShowType)
                                e.Graphics.DrawString(((int) tile.Type[subY, subX]).ToString(), f, Brushes.White, new Rectangle(globalX * Zoom + offset.X, globalY * Zoom + offset.Y, Zoom, Zoom));
                        }

                        if (DisplayGrid)
                            e.Graphics.DrawRectangle(pen, globalX * Zoom + offset.X, globalY * Zoom + offset.Y, Zoom, Zoom);
                    }
                }

                if (DisplayGrid)
                    e.Graphics.DrawRectangle(bigGridPen, w * 2 * Zoom + offset.X, h * 2 * Zoom + offset.Y, 2 * Zoom, 2 * Zoom);

            }
        }
    }

    internal float GetHeightInfo(PBCFileReader.HeightMap tileHeight, int subY, int subX)
    {
        var tile = tileHeight.Quadrants[subY, subX];

        return LayerView switch
        {
            LayerView.Layer0 => tile.Layer0,
            LayerView.Layer1 => tile.Layer1,
            LayerView.Layer2 => tile.Layer2,

            _ => tile.Layer1
        };
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        Focus();

        ActiveTool?.OnMouseDown(e);

        //lastMousePos = e.Location;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        ActiveTool?.OnMouseUp(e);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float? HighlightedHeight { get; set; }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        ActiveTool?.OnMouseMove(e);

        if (e.Button == MouseButtons.Left && ModifierKeys.HasFlag(Keys.Control) && lastMousePos.HasValue)
        {
            int dx = e.X - lastMousePos.Value.X;
            int dy = e.Y - lastMousePos.Value.Y;

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

        int clickedX = (mouse.X - offset.X) / Zoom;
        int clickedY = (mouse.Y - offset.Y) / Zoom;

        int tileX = clickedX / 2;
        int tileY = clickedY / 2;
        int subX = clickedX % 2;
        int subY = clickedY % 2;

        if (tileX < 0 || tileX >= PBCFile.Width ||
            tileY < 0 || tileY >= PBCFile.Height ||
            subX < 0 || subY < 0)
            return false;

        pos = new TilePosition
        {
            TileX = tileX,
            TileY = tileY,
            SubX = subX,
            SubY = subY
        };

        return true;
    }

    public void ChangeLayerView(LayerView view)
    {
        if (LayerView != view)
        {
            LayerView = view;
            UpdateHeight();
            Invalidate();
        }
    }

    public void SelectQuadrant(PBCFileReader.Quadrant quadrant) => QuadrantSelected?.Invoke(quadrant);
    
}

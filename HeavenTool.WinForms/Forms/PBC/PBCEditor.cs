using HeavenTool.IO.FileFormats.PBC;
using HeavenTool.Utility;
using HeavenTool.Utility.UndoSystem;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace HeavenTool.Forms.PBC;

public partial class PBCEditor : Form
{
    public PBCFileReader CurrentPBC;

    private readonly Action<byte[]>? saveFunction;
    public UndoManager UndoManager { get; } = new();

    public PBCEditor(byte[] fileContent, string fileName, Action<byte[]> saveFunction)
    {
        InitializeComponent();
        this.saveFunction = saveFunction;

        pbcPreview.ZoomChanged += ZoomChanged;
        pbcPreview.MouseMove += (_, _) => UpdateStatusLabel();
        pbcPreview.QuadrantSelected += QuadrantSelected;

        Text = $"PBC Editor: {fileName}";
        CurrentPBC = new PBCFileReader(fileContent);
        UpdateStatusLabel();

        gridToolStripMenuItem.Checked = pbcPreview.DisplayGrid;
        viewIDToolStripMenuItem.Checked = pbcPreview.ShowType;

        propertyGrid.PropertyValueChanged += PropertyGrid_PropertyValueChanged;

        ReloadPBCImage();

        var colors = Enum.GetValues<TileType>();
        foreach (TileType color in colors)
            colorList.Items.Add(color);

        pbcPreview.TileBrush = TileType.Null;
        colorList.SelectedIndex = colorList.Items.IndexOf(TileType.Null);
    }

    private void PropertyGrid_PropertyValueChanged(object? s, PropertyValueChangedEventArgs e)
    {
        if (s is not PropertyGrid grid || grid.SelectedObject == null || e.ChangedItem == null || e.ChangedItem.PropertyDescriptor == null)
            return;

        var target = grid.SelectedObject;
        var property = e.ChangedItem.PropertyDescriptor;

        var oldValue = e.OldValue;
        var newValue = property.GetValue(target);

        var command = new PropertyChangeUndoCommand(target, property, oldValue, newValue);

        UndoManager.Execute(command, true);
        Invalidate();
    }

    private void QuadrantSelected(PBCFileReader.Quadrant quadrant)
    {
        propertyGrid.SelectedObject = quadrant;
    }

    private void UpdateStatusLabel()
    {
        if (pbcPreview == null || CurrentPBC == null)
        {
            statusLabel.Text = "No PBC loaded.";
            return;
        }

        var statusText = $"Width: {CurrentPBC.Width * 2} | Height: {CurrentPBC.Height * 2} | Offset: (X {CurrentPBC.OffsetX}, Y {CurrentPBC.OffsetY}) ";

        if (pbcPreview.CurrentView == ViewType.Collision && pbcPreview.TileBrush != null)
            statusText += $"| Brush: {pbcPreview.TileBrush}";

        statusText += pbcPreview.HighlightedHeight != null ? $"| Highlithed Height: {pbcPreview.HighlightedHeight}" : "";

        statusLabel.Text = statusText;
    }

    private void ZoomChanged(int zoom)
    {
        currentZoomMenu.Text = $"Zoom: {zoom}x";
    }

    public void ReloadPBCImage()
    {
        pbcPreview.PBCFile = CurrentPBC;
        pbcPreview.Invalidate();

        heightMapToolStripMenuItem.Checked = pbcPreview.CurrentView == ViewType.HeightMap;
        collisionMapToolStripMenuItem.Checked = pbcPreview.CurrentView == ViewType.Collision;

        propertyGrid.SelectedObject = CurrentPBC;
    }

    private void ZoomPlusButton_Click(object sender, EventArgs e)
    {
        pbcPreview.Zoom++;

        ZoomChanged(pbcPreview.Zoom);
        ReloadPBCImage();
    }

    private void ZoomMinusButton_Click(object sender, EventArgs e)
    {
        if (pbcPreview.Zoom > 1)
            pbcPreview.Zoom--;

        ZoomChanged(pbcPreview.Zoom);
        ReloadPBCImage();
    }

    private void ViewIDToolStripMenuItem_Click(object sender, EventArgs e)
    {
        pbcPreview.ShowType = !pbcPreview.ShowType;
        viewIDToolStripMenuItem.Checked = pbcPreview.ShowType;

        ReloadPBCImage();
    }

    private void GridToolStripMenuItem_Click(object sender, EventArgs e)
    {
        pbcPreview.DisplayGrid = !pbcPreview.DisplayGrid;
        gridToolStripMenuItem.Checked = pbcPreview.DisplayGrid;

        ReloadPBCImage();
    }

    private void CollisionMapToolStripMenuItem_Click(object sender, EventArgs e)
    {
        pbcPreview.CurrentView = ViewType.Collision;
        pbcPreview.ActiveTool = pbcPreview.Tools.CollisionBrush;
        propertyGrid.SelectedObject = CurrentPBC;
        colorList.Enabled = true;
        ReloadPBCImage();
    }

    private void HeightMapToolStripMenuItem_Click(object sender, EventArgs e)
    {
        pbcPreview.CurrentView = ViewType.HeightMap;
        pbcPreview.ActiveTool = pbcPreview.Tools.InspectorTool;
        colorList.Enabled = false;
        ReloadPBCImage();
    }

    private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (CurrentPBC != null)
            saveFunction?.Invoke(CurrentPBC.SaveAsBytes());
    }

    private void SaveButton_Click(object sender, EventArgs e)
    {
        if (CurrentPBC != null)
            saveFunction?.Invoke(CurrentPBC.SaveAsBytes());
    }

    private void ColorList_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (colorList.SelectedItem != null && colorList.SelectedItem is TileType tileType)
            pbcPreview.TileBrush = tileType;

        UpdateStatusLabel();
    }

    private void ColorList_DrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index == -1) return;

        var tileType = (TileType)colorList.Items[e.Index];
        byte tileNumber = (byte)tileType;

        e.DrawBackground();
        var rect = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 2, 12, e.Bounds.Height - 4);
        using (SolidBrush brush = new(PBCImageUtilities.GetColor(tileType)))
        {
            e.Graphics.FillRectangle(brush, rect);
        }

        var ft = new StringFormat()
        {
            LineAlignment = StringAlignment.Center,
            Alignment = StringAlignment.Far
        };

        if (e.Font != null)
        {
            e.Graphics.DrawString(tileType.ToString(), e.Font, Brushes.White, new Rectangle(e.Bounds.X + 25, e.Bounds.Y - 1, e.Bounds.Width, e.Bounds.Height), StringFormat.GenericDefault);
            e.Graphics.DrawString(tileNumber.ToString(), e.Font, Brushes.DarkGray, new Rectangle(e.Bounds.X + 25, e.Bounds.Y, e.Bounds.Width - 27, e.Bounds.Height), ft);
        }

        e.DrawFocusRectangle();
    }

    private void Layer0ToolStripMenuItem_Click(object sender, EventArgs e)
    {
        pbcPreview.ChangeLayerView(LayerView.Layer0);
    }

    private void Layer1ToolStripMenuItem_Click(object sender, EventArgs e)
    {
        pbcPreview.ChangeLayerView(LayerView.Layer1);
    }

    private void Layer2ToolStripMenuItem_Click(object sender, EventArgs e)
    {
        pbcPreview.ChangeLayerView(LayerView.Layer2);
    }

    protected override void OnInvalidated(InvalidateEventArgs e)
    {
        pbcPreview.Invalidate();
        base.OnInvalidated(e);
    }

    public void Undo()
    {
        UndoManager.Undo();

        Invalidate();
    }

    public void Redo()
    {
        UndoManager.Redo();

        Invalidate();
    }

    private void UndoToolStripMenuItem_Click(object sender, EventArgs e) => Undo();
    private void RedoToolStripMenuItem_Click(object sender, EventArgs e) => Redo();
}
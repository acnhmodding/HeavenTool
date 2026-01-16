using HeavenTool.IO.FileFormats.PBC;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace HeavenTool.Forms.PBC;

public partial class PBCEditor : Form
{
    public PBCFileReader CurrentPBC;

    private Action<byte[]> SaveFunction = null;

    public PBCEditor(byte[] fileContent, string fileName, Action<byte[]> saveFunction)
    {
        InitializeComponent();
        SaveFunction = saveFunction;

        pbcPreview.ZoomChanged += ZoomChanged;
        pbcPreview.MouseMove += MouseMoved;
        pbcPreview.QuadrantSelected += QuadrantSelected;
        //pbcPreview.SizeMode = PictureBoxSizeMode.AutoSize;

        Text = $"PBC Editor: {fileName}";
        CurrentPBC = new PBCFileReader(fileContent);
        UpdateStatusLabel();

        gridToolStripMenuItem.Checked = pbcPreview.DisplayGrid;
        viewIDToolStripMenuItem.Checked = pbcPreview.ShowType;

        ReloadPBCImage();

        var colors = Enum.GetValues(typeof(TileType));
        foreach (TileType color in colors)
            colorList.Items.Add(color);

        pbcPreview.TileBrush = TileType.Null;
        colorList.SelectedIndex = colorList.Items.IndexOf(TileType.Null);
    }

    private void QuadrantSelected(PBCFileReader.Quadrant quadrant)
    {
        propertyGrid.SelectedObject = quadrant;
    }

    private void UpdateStatusLabel()
    {
        var statusText = $"Width: {CurrentPBC.Width * 2} | Height: {CurrentPBC.Height * 2} | Offset: (X {CurrentPBC.OffsetX}, Y {CurrentPBC.OffsetY}) ";

        if (pbcPreview != null && pbcPreview.TileBrush != null)
            statusText += $"| Brush: {pbcPreview.TileBrush} ({(byte)pbcPreview.TileBrush})";

        statusLabel.Text = statusText;
    }

    private void MouseMoved(object sender, MouseEventArgs e)
    {
        var currentHeight = pbcPreview.HighlightedHeight != null ? $"| Highlithed Height: {pbcPreview.HighlightedHeight}" : "";
        UpdateStatusLabel();
        statusLabel.Text += currentHeight;
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

    private void zoomPlusButton_Click(object sender, EventArgs e)
    {
        pbcPreview.Zoom++;

        ZoomChanged(pbcPreview.Zoom);
        ReloadPBCImage();
    }

    private void zoomMinusButton_Click(object sender, EventArgs e)
    {
        if (pbcPreview.Zoom > 1)
            pbcPreview.Zoom--;

        ZoomChanged(pbcPreview.Zoom);
        ReloadPBCImage();
    }

    private void viewIDToolStripMenuItem_Click(object sender, EventArgs e)
    {
        pbcPreview.ShowType = !pbcPreview.ShowType;
        viewIDToolStripMenuItem.Checked = pbcPreview.ShowType;

        ReloadPBCImage();
    }

    private void gridToolStripMenuItem_Click(object sender, EventArgs e)
    {
        //GridView = !GridView;

        pbcPreview.DisplayGrid = !pbcPreview.DisplayGrid;
        gridToolStripMenuItem.Checked = pbcPreview.DisplayGrid;

        ReloadPBCImage();
    }

    private void collisionMapToolStripMenuItem_Click(object sender, EventArgs e)
    {
        pbcPreview.CurrentView = ViewType.Collision;
        propertyGrid.SelectedObject = CurrentPBC;
        colorList.Enabled = true;
        ReloadPBCImage();
    }

    private void heightMapToolStripMenuItem_Click(object sender, EventArgs e)
    {
        pbcPreview.CurrentView = ViewType.HeightMap;
        colorList.Enabled = false;
        ReloadPBCImage();
    }    

    private void saveToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (CurrentPBC != null)
            SaveFunction?.Invoke(CurrentPBC.SaveAsBytes());
    }

    private void saveButton_Click(object sender, EventArgs e)
    {
        if (CurrentPBC != null)
            SaveFunction?.Invoke(CurrentPBC.SaveAsBytes());
    }

    private void colorList_SelectedIndexChanged(object sender, EventArgs e)
    {
        if (colorList.SelectedItem != null && colorList.SelectedItem is TileType tileType)
            pbcPreview.TileBrush = tileType;

        UpdateStatusLabel();
    }

    private void colorList_DrawItem(object sender, DrawItemEventArgs e)
    {
        if (e.Index == -1) return;
        
        var tileType = (TileType) colorList.Items[e.Index];

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

        e.Graphics.DrawString(tileType.ToString(), e.Font, Brushes.White, new Rectangle(e.Bounds.X + 25, e.Bounds.Y - 1, e.Bounds.Width, e.Bounds.Height), StringFormat.GenericDefault);


        e.Graphics.DrawString(((byte)tileType).ToString(), e.Font, Brushes.DarkGray, new Rectangle(e.Bounds.X + 25, e.Bounds.Y, e.Bounds.Width - 27, e.Bounds.Height), ft);

        e.DrawFocusRectangle();
    }

}

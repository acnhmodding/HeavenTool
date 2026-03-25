using HeavenTool.Forms.Components;
using HeavenTool.Forms.Editor;
using HeavenTool.IO.FileFormats.PBC;
using HeavenTool.Utility;
using HeavenTool.Utility.UndoSystem;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace HeavenTool.Forms.PBC;

public partial class PBCEditor : BaseEditor
{
    #region IEditor
    public override void LoadFile(Stream stream)
    {
        CurrentPBC = new PBCFileReader(stream);

        ReloadPBCImage();
    }

    public override void SaveFile()
    {
        if (string.IsNullOrEmpty(FilePath)) return;

        // TODO: Probably this editor needs a new interface "IChildFile" that contains a field that points to the parent file (in that case an SARC file)
    }
    #endregion

    public TileEditor Preview { get; } =  new()
    {
        BackColor = Color.Black, 
        CurrentView = ViewType.Collision
    };

    public PropertyGrid PropertyGrid { get; } = new()
    {
        HelpVisible = false
    };

    public ListBox ColorList { get; } = new();

    public PBCFileReader? CurrentPBC;

    private readonly Action<byte[]>? saveFunction;


    public PBCEditor()
    {
        InitializeComponent();

        dockPanel.Theme = new VS2015DarkTheme();
        dockPanel.Dock = DockStyle.Fill;
        dockPanel.DocumentStyle = DocumentStyle.DockingWindow;
    
        var editor = DockableControl.Create(Preview, "Editor");
        var colorControl = DockableControl.Create(ColorList, "Colors");
        var inspector = DockableControl.Create(PropertyGrid, "Inspector");

        editor.Show(dockPanel, DockState.Document);
        colorControl.Show(dockPanel, DockState.DockRight);
        inspector.Show(colorControl.Pane, DockAlignment.Bottom, 0.5);

        Preview.ZoomChanged += ZoomChanged;
        Preview.QuadrantSelected += (q) => PropertyGrid.SelectedObject = q;
        ColorList.DrawMode = DrawMode.OwnerDrawVariable;
        ColorList.DrawItem += ColorList_DrawItem;
        ColorList.SelectedIndexChanged += ColorList_SelectedIndexChanged;

        gridToolStripMenuItem.Checked = Preview.DisplayGrid;
        viewIDToolStripMenuItem.Checked = Preview.ShowType;

        PropertyGrid.PropertyValueChanged += PropertyGrid_PropertyValueChanged;

        foreach (TileType color in Enum.GetValues<TileType>())
            ColorList.Items.Add(color);

        Preview.TileBrush = TileType.Null;
        ColorList.SelectedIndex = ColorList.Items.IndexOf(TileType.Null);

        #region build view submenu
        viewMenuItem.AddSeparator();

        var viewMenuItems = new Dictionary<ViewType, ToolStripMenuItem>();
        var tools = new Dictionary<ViewType, Tools.TileEditorTool>
        {
            { ViewType.HeightMap, Preview.Tools.InspectorTool },
            { ViewType.Collision, Preview.Tools.CollisionBrush },
        };
        foreach (var view in Enum.GetValues<ViewType>())
        {
            viewMenuItems[view] = viewMenuItem.AddItem(view.ToString(), () =>
            {
                Preview.CurrentView = view;
                if (tools.TryGetValue(view, out var tool))
                    Preview.ActiveTool = tool;
                PropertyGrid.SelectedObject = CurrentPBC;
                ColorList.Enabled = view == ViewType.Collision;

                foreach (var (itemView, item) in viewMenuItems)
                    item.Checked = view == itemView;

                ReloadPBCImage();
            });

            viewMenuItems[view].Checked = Preview.CurrentView == view;
        }

        viewMenuItem.AddSeparator();

        LayerView[] layers = Enum.GetValues<LayerView>();
        for (int i = 0; i < layers.Length; i++)
        {
            var layer = layers[i];
            viewMenuItem.AddItem($"Layer {i}", () => Preview.ChangeLayerView(layer));
        }
        #endregion
    }

    public PBCEditor(byte[] fileContent, string fileName, Action<byte[]> saveFunction) : this()
    {
        this.saveFunction = saveFunction;

        Text = $"PBC Editor: {fileName}";
        CurrentPBC = new PBCFileReader(fileContent);

        ReloadPBCImage();
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


    private void ZoomChanged(int zoom)
    {
        currentZoomMenu.Text = $"Zoom: {zoom}x";
    }

    public void ReloadPBCImage()
    {
        Preview.PBCFile = CurrentPBC;
        Preview.Invalidate();

        PropertyGrid.SelectedObject = CurrentPBC;
    }

    private void ZoomPlusButton_Click(object sender, EventArgs e)
    {
        Preview.Zoom++;

        ZoomChanged(Preview.Zoom);
        ReloadPBCImage();
    }

    private void ZoomMinusButton_Click(object sender, EventArgs e)
    {
        if (Preview.Zoom > 1)
            Preview.Zoom--;

        ZoomChanged(Preview.Zoom);
        ReloadPBCImage();
    }

    private void ViewIDToolStripMenuItem_Click(object sender, EventArgs e)
    {
        Preview.ShowType = !Preview.ShowType;
        viewIDToolStripMenuItem.Checked = Preview.ShowType;

        ReloadPBCImage();
    }

    private void GridToolStripMenuItem_Click(object sender, EventArgs e)
    {
        Preview.DisplayGrid = !Preview.DisplayGrid;
        gridToolStripMenuItem.Checked = Preview.DisplayGrid;

        ReloadPBCImage();
    }

    private void CollisionMapToolStripMenuItem_Click(object sender, EventArgs e)
    {
        Preview.CurrentView = ViewType.Collision;
        Preview.ActiveTool = Preview.Tools.CollisionBrush;
        PropertyGrid.SelectedObject = CurrentPBC;
        ColorList.Enabled = true;
        ReloadPBCImage();
    }

    private void HeightMapToolStripMenuItem_Click(object sender, EventArgs e)
    {
        Preview.CurrentView = ViewType.HeightMap;
        Preview.ActiveTool = Preview.Tools.InspectorTool;
        ColorList.Enabled = false;
        ReloadPBCImage();
    }

    private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (CurrentPBC != null)
            saveFunction?.Invoke(CurrentPBC.SaveAsBytes());
    }

    private void ColorList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (ColorList.SelectedItem != null && ColorList.SelectedItem is TileType tileType)
            Preview.TileBrush = tileType;
    }

    private void ColorList_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index == -1 || ColorList.Items[e.Index] is not TileType tileType) return;

        byte tileNumber = (byte)tileType;

        e.DrawBackground();
        var rect = new Rectangle(e.Bounds.X + 10, e.Bounds.Y + 2, 12, e.Bounds.Height - 4);

        using (SolidBrush brush = new(PBCImageUtilities.GetColor(tileType)))
            e.Graphics.FillRectangle(brush, rect);
        
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

    protected override void OnInvalidated(InvalidateEventArgs e)
    {
        Preview.Invalidate();
        ColorList.Invalidate();

        base.OnInvalidated(e);
    }

    private void UndoToolStripMenuItem_Click(object sender, EventArgs e) => Undo();
    private void RedoToolStripMenuItem_Click(object sender, EventArgs e) => Redo();
}
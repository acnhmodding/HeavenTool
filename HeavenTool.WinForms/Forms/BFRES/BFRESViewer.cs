using HeavenTool.IO.FileFormats.BFRES;
using HeavenTool.Shaders;
using HeavenTool.Rendering.BFRES;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace HeavenTool.Forms.BFRES;

public partial class BFRESViewer : Form
{
    private static readonly Color PaneBack = Color.FromArgb(37, 37, 38), FieldBack = Color.FromArgb(51, 51, 55);

    private readonly BfresViewport viewport = null!;
    private readonly TreeView hierarchy = new()
    {
        Dock = DockStyle.Fill, BackColor = PaneBack, ForeColor = Color.Gainsboro,
        CheckBoxes = true, HideSelection = false, BorderStyle = BorderStyle.None
    };
    private CancellationTokenSource? loading;
    private IReadOnlyList<BfresAsset> assets = [];
    private string sourceName = "";
    private bool populating;
    private int assetRequestVersion;

    public BFRESViewer()
    {
        InitializeComponent();
        if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;
        viewport = new BfresViewport();
        InitializeDockWindows();
        openToolStripMenuItem.Click += async (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Switch models|*.bfres;*.sbfres;*.zs;*.szs;*.sarc|All files|*.*" };
            if (dialog.ShowDialog(this) == DialogResult.OK) await LoadFileAsync(dialog.FileName);
        };
        loadShaderItem.Click += async (_, _) =>
        {
            using var dialog = new OpenFileDialog { Title = "Load game materials", Filter = "Game material files|*.bfsha;*.szs;*.sarc|All files|*.*" };
            if (dialog.ShowDialog(this) == DialogResult.OK) await LoadShaderArchiveAsync(dialog.FileName);
        };
        frameAllItem.Click += (_, _) => viewport.Renderer.FrameScene();
        wireframeItem.CheckedChanged += (_, _) => viewport.Renderer.Wireframe = wireframeItem.Checked;
        InitializeLightingMenu();
        InitializeBonesMenu();
        materialPaneItem.Click += (_, _) => { if (materialPaneItem.Checked) materialWindow.Hide(); else ShowMaterialEditor(); };
        scenePaneItem.Click += (_, _) => { if (scenePaneItem.Checked) sceneWindow.Hide(); else ShowDockWindow(sceneWindow); };
        restoreLayoutItem.Click += (_, _) => RestoreDefaultLayout();
        hierarchy.NodeMouseDoubleClick += (_, e) => { if (e.Node?.Tag is BfresMesh) ShowMaterialEditor(); };
        hierarchy.AfterCheck += (_, e) =>
        {
            if (!populating) SetNodeVisibility(e.Node);
        };
        hierarchy.AfterSelect += (_, e) =>
        {
            if (e.Node?.Tag is BfresMesh mesh)
            {
                viewport.Renderer.SelectBone(null);
                UpdateMaterialEditor(mesh);
                return;
            }
            SetMaterial(null);
            if (e.Node?.Tag is BfresBone bone) SelectPreviewBone(bone);
            else viewport.Renderer.SelectBone(null);
        };
        viewport.RenderFailed += ex =>
        {
            status.Text = "3D rendering failed.";
            System.Diagnostics.Debug.WriteLine(ex);
            MessageBox.Show(this, "The 3D view could not be opened. Check that your graphics driver supports OpenGL 4.4.", "3D view unavailable", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        viewport.Renderer.DrawStatusChanged += () =>
        {
            UpdateViewerStatus();
            if (hierarchy.SelectedNode?.Tag is BfresMesh mesh) UpdateMaterialEditor(mesh);
        };
        AllowDrop = true;
        DragEnter += (_, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
        DragDrop += async (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0) await LoadFileAsync(paths[0]);
        };
    }

    #region Loading

    public async Task LoadFileAsync(string path)
    {
        int version = ++assetRequestVersion;
        await LoadAsync(() => File.OpenRead(path), Path.GetFileName(path));
        if (IsDisposed || assets.Count == 0 || version != assetRequestVersion) return;
        if (shaderArchive == null && BfresScene.FindShaderArchive(path) is { } shader)
            await LoadShaderArchiveAsync(shader);
    }

    public Task LoadBytesAsync(byte[] bytes, string name)
    {
        assetRequestVersion++;
        return LoadAsync(() => new MemoryStream(bytes, false), name);
    }

    private async Task LoadAsync(Func<Stream> open, string name)
    {
        loading?.Cancel();
        var request = new CancellationTokenSource();
        loading = request;
        status.Text = $"Loading {name}...";
        try
        {
            var (loadedAssets, scene) = await Task.Run(() =>
            {
                using var stream = open();
                var scene = BfresScene.Load(stream, request.Token);
                return (scene.Assets, scene.Geometry);
            }, request.Token);

            if (IsDisposed || request.IsCancellationRequested) return;
            assets = loadedAssets;
            SetMaterial(null);
            sourceName = name;
            viewport.Renderer.SetScene(scene);
            viewport.Renderer.SetSkeletons(assets.SelectMany(a => a.Document.Models).Select(m => m.Bones));
            populating = true;
            hierarchy.BeginUpdate();
            try
            {
                hierarchy.Nodes.Clear();
                boneNodes.Clear();
                foreach (var asset in assets)
                {
                    var root = hierarchy.Nodes.Add(asset.EntryName);
                    root.Checked = true;
                    foreach (var model in asset.Document.Models)
                    {
                        var node = root.Nodes.Add(model.Name);
                        node.Checked = true;
                        AddSkeletonNodes(node, model.Bones);
                        foreach (var mesh in model.Meshes) node.Nodes.Add(new TreeNode(mesh.Name) { Tag = mesh, Checked = true });
                        node.Expand();
                    }
                    root.Expand();
                }
            }
            finally { hierarchy.EndUpdate(); populating = false; }
            UpdateDocumentTitle();
            UpdateViewerStatus();
            foreach (string warning in assets.SelectMany(x => x.Document.Warnings).Distinct()) System.Diagnostics.Debug.WriteLine("BFRES: " + warning);
            if (shaderArchive != null) await PrepareNativeShadersAsync(shaderArchive, request.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (IsDisposed || request.IsCancellationRequested) return;
            status.Text = $"Could not load {name}.";
            MessageBox.Show(this, ex.Message, "BFRES load failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (ReferenceEquals(loading, request)) loading = null;
            request.Dispose();
        }
    }

    private void UpdateDocumentTitle()
    {
        bool edited = assets.SelectMany(a => a.Document.Models).SelectMany(m => m.Materials).Any(m => m.HasChanges);
        Text = $"BFRES Viewer — {sourceName}" + (edited ? " *" : "");
    }

    private void UpdateViewerStatus()
    {
        if (assets.Count == 0) { status.Text = "Open a model to begin."; return; }
        int meshes = viewport.Renderer.NativeDrawCount + viewport.Renderer.FallbackDrawCount;
        string text = $"{meshes:N0} meshes · {CurrentLighting}";
        if (viewport.Renderer.FallbackDrawCount > 0) text += " · Some materials use simplified shading";
        if (assets.Any(a => a.Document.Warnings.Count > 0)) text += " · Some model features are unavailable";
        status.Text = text;
    }

    #endregion

    #region Shaders

    private SwitchShaderArchive? shaderArchive;
    private CancellationTokenSource? shaderLoading;

    public async Task LoadShaderArchiveAsync(string path)
    {
        status.Text = "Loading materials...";
        shaderLoading?.Cancel();
        var request = new CancellationTokenSource(); shaderLoading = request;
        try
        {
            var archive = await Task.Run(() => SwitchShaderArchive.Load(path), request.Token);
            if (IsDisposed || request.IsCancellationRequested) return;
            shaderArchive = archive;
            await PrepareNativeShadersAsync(archive, request.Token);
            if (hierarchy.SelectedNode?.Tag is BfresMesh mesh) UpdateMaterialEditor(mesh);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!IsDisposed) MessageBox.Show(this, ex.Message, "Shader archive load failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { if (ReferenceEquals(shaderLoading, request)) shaderLoading = null; request.Dispose(); }
    }

    private async Task PrepareNativeShadersAsync(SwitchShaderArchive archive, CancellationToken cancellation)
    {
        var snapshot = assets;
        status.Text = "Preparing materials...";
        var prepared = await Task.Run(() => BfresScene.PrepareShaders(snapshot, archive, cancellation), cancellation);
        if (IsDisposed || cancellation.IsCancellationRequested || !ReferenceEquals(snapshot, assets)) return;
        foreach (var error in prepared.Errors) System.Diagnostics.Debug.WriteLine($"Material shader: {error.Key.Name}: {error.Value}");
        viewport.Renderer.SetNativeShaders(prepared.Programs);
        status.Text = "Materials loaded.";
    }

    #endregion

    #region Scene explorer

    private readonly Dictionary<BfresBone, TreeNode> boneNodes = new(ReferenceEqualityComparer.Instance);

    private void InitializeBonesMenu()
    {
        void shortcuts(object? _, KeyEventArgs e)
        {
            if (e.Modifiers != Keys.None) return;
            if (e.KeyCode == Keys.B) { showBonesItem.Checked = !showBonesItem.Checked; e.SuppressKeyPress = true; }
            if (e.KeyCode == Keys.F) { viewport.Renderer.FrameSelection(); e.SuppressKeyPress = true; }
        }
        viewport.KeyDown += shortcuts;
        hierarchy.KeyDown += shortcuts;
        showBonesItem.CheckedChanged += (_, _) => viewport.Renderer.ShowBones = showBonesItem.Checked;
        frameBoneItem.Click += (_, _) => viewport.Renderer.FrameSelectedBone();
        viewport.Renderer.BonePicked += bone =>
        {
            if (boneNodes.TryGetValue(bone, out var node)) { hierarchy.SelectedNode = node; node.EnsureVisible(); }
        };
    }

    private void AddSkeletonNodes(TreeNode modelNode, IReadOnlyList<BfresBone> bones)
    {
        if (bones.Count == 0) return;
        var root = modelNode.Nodes.Add($"Skeleton ({bones.Count} bones)");
        root.Checked = true;
        var nodes = bones.Select(b => new TreeNode(b.Name) { Tag = b, Checked = true }).ToArray();
        foreach (var bone in bones)
        {
            boneNodes.Add(bone, nodes[bone.Index]);
            (bone.ParentIndex < 0 ? root : nodes[bone.ParentIndex]).Nodes.Add(nodes[bone.Index]);
        }
    }

    private void SelectPreviewBone(BfresBone bone)
    {
        showBonesItem.Checked = true;
        viewport.Renderer.SelectBone(bone);
    }

    private void SetNodeVisibility(TreeNode? node)
    {
        if (node == null) return;
        bool previous = populating;
        populating = true;
        try
        {
            if (node.Tag is BfresMesh mesh) viewport.Renderer.SetVisible(mesh, node.Checked);
            if (node.Tag is BfresBone bone) viewport.Renderer.SetBoneVisible(bone, node.Checked);
            foreach (TreeNode child in node.Nodes)
            {
                child.Checked = node.Checked;
                SetNodeVisibility(child);
            }
        }
        finally { populating = previous; }
    }

    #endregion

    #region Material properties

    // Edits are a live preview only: they change the in-memory material and are never written back to a file.
    private Label heading = null!, usageSummary = null!;
    private TextBox search = null!;
    private CheckBox showAllProperties = null!;
    private DataGridView grid = null!;
    private Button resetValue = null!, resetMaterial = null!;
    private BfresMaterial? material;
    private IReadOnlySet<(string Name, int Component)>? visibility;
    private bool materialInitialized, populatingGrid;

    private DockContent CreateMaterialWindow()
    {
        heading = new() { AutoSize = true, Dock = DockStyle.Fill, Margin = new(3, 3, 3, 10), MinimumSize = new(0, 24) };
        search = new()
        {
            Dock = DockStyle.Fill, PlaceholderText = "Search properties...", Margin = new(3, 3, 3, 8),
            BackColor = FieldBack, ForeColor = Color.Gainsboro, BorderStyle = BorderStyle.FixedSingle, TabIndex = 0
        };
        showAllProperties = new() { AutoSize = true, Text = "Show all properties", TabIndex = 1, Margin = new(3, 0, 3, 4) };
        usageSummary = new() { AutoSize = true, Dock = DockStyle.Fill, Margin = new(3, 0, 3, 8) };
        grid = new()
        {
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = PaneBack, BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill, EnableHeadersVisualStyles = false, GridColor = Color.FromArgb(65, 65, 68), MultiSelect = false,
            RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.CellSelect, TabIndex = 2,
            DefaultCellStyle = new() { BackColor = PaneBack, ForeColor = Color.Gainsboro, SelectionBackColor = Color.FromArgb(0, 90, 150), SelectionForeColor = Color.White },
            ColumnHeadersDefaultCellStyle = new() { BackColor = FieldBack, ForeColor = Color.Gainsboro }
        };
        grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { FillWeight = 60, HeaderText = "Property", MinimumWidth = 130, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable },
            new DataGridViewTextBoxColumn { FillWeight = 40, HeaderText = "Value", MinimumWidth = 65, SortMode = DataGridViewColumnSortMode.NotSortable });
        resetValue = new() { AutoSize = true, CausesValidation = false, FlatStyle = FlatStyle.Flat, Text = "Reset value", TabIndex = 0 };
        resetMaterial = new() { AutoSize = true, CausesValidation = false, FlatStyle = FlatStyle.Flat, Text = "Reset material", TabIndex = 1 };
        var resetButtons = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill, Margin = new(0, 6, 0, 4), TabIndex = 3
        };
        resetButtons.Controls.AddRange([resetValue, resetMaterial]);
        var notice = new Label { AutoSize = true, Dock = DockStyle.Fill, Text = "Changes apply to every mesh using this material.", Margin = new(3, 4, 3, 4) };

        var layout = new TableLayoutPanel { ColumnCount = 1, RowCount = 7, Dock = DockStyle.Fill, Padding = new(8), Size = new(450, 700) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Control[] rows = [heading, search, showAllProperties, usageSummary, grid, resetButtons, notice];
        for (int row = 0; row < rows.Length; row++)
        {
            layout.RowStyles.Add(rows[row] == grid ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(rows[row], 0, row);
        }

        var window = CreatePane("Material properties", DockState.DockRight, layout, new(450, 700));
        window.BackColor = PaneBack;
        window.ForeColor = Color.Gainsboro;

        search.TextChanged += (_, _) => Populate();
        showAllProperties.CheckedChanged += (_, _) => Populate();
        grid.CellValidating += (_, e) =>
        {
            if (populatingGrid || e.ColumnIndex != 1 || e.RowIndex < 0) return;
            bool valid = TryValue(e.FormattedValue, out float _);
            e.Cancel = !valid;
            grid.Rows[e.RowIndex].Cells[1].ErrorText = valid ? "" : "Enter a finite number, for example 0.5.";
        };
        grid.CellEndEdit += (_, e) =>
        {
            if (populatingGrid || material == null || e.ColumnIndex != 1 || e.RowIndex < 0) return;
            var row = grid.Rows[e.RowIndex];
            if (row.Tag is not ValueTuple<string, int> key || !TryValue(row.Cells[1].Value, out float value)) return;
            material.SetParameterComponent(key.Item1, key.Item2, value);
            UpdateRow(row, key.Item1, key.Item2);
            OnMaterialEdited(material);
        };
        resetValue.Click += (_, _) =>
        {
            if (material == null || grid.CurrentRow?.Tag is not ValueTuple<string, int> key) return;
            grid.CancelEdit();
            material.SetParameterComponent(key.Item1, key.Item2, material.OriginalParameters[key.Item1].Float(key.Item2, 0));
            UpdateRow(grid.CurrentRow, key.Item1, key.Item2);
            OnMaterialEdited(material);
        };
        resetMaterial.Click += (_, _) =>
        {
            if (material == null) return;
            grid.CancelEdit(); material.ResetParameters(); Populate();
            OnMaterialEdited(material);
        };
        window.FormClosing += (_, _) => grid.CancelEdit();
        window.DockStateChanged += (_, _) => { if (window.IsHidden) grid.CancelEdit(); };
        SetMaterial(null);
        return window;
    }

    private void OnMaterialEdited(BfresMaterial edited)
    {
        viewport.Renderer.RefreshMaterial(edited);
        if (hierarchy.SelectedNode?.Tag is BfresMesh mesh) UpdateMaterialEditor(mesh);
        UpdateDocumentTitle();
        status.Text = edited.HasChanges ? $"Material edited: {edited.Name}" : $"Material reset: {edited.Name}";
    }

    private void UpdateMaterialEditor(BfresMesh mesh) => SetMaterial(mesh.Material, viewport.Renderer.GetMaterialVisibility(mesh));

    private void ShowMaterialEditor()
    {
        if (hierarchy.SelectedNode?.Tag is BfresMesh selected) UpdateMaterialEditor(selected);
        else SetMaterial(null);
        ShowDockWindow(materialWindow);
    }

    private void SetMaterial(BfresMaterial? value, IReadOnlySet<(string Name, int Component)>? usage = null)
    {
        if (materialInitialized && ReferenceEquals(material, value) && ReferenceEquals(visibility, usage)) return;
        materialInitialized = true;
        grid.CancelEdit(); material = value; visibility = usage;
        heading.Text = value == null ? "Select a mesh to edit its material." : value.Name;
        Populate();
    }

    private void Populate()
    {
        populatingGrid = true; grid.CancelEdit(); grid.Rows.Clear();
        try
        {
            grid.Enabled = resetValue.Enabled = resetMaterial.Enabled = material != null;
            usageSummary.Text = "";
            if (material == null) return;
            foreach (var property in BfresMaterialProperty.Enumerate(material, visibility, showAllProperties.Checked, search.Text))
            {
                var row = grid.Rows[grid.Rows.Add(property.Label, "")];
                row.Tag = (property.Name, property.Component);
                UpdateRow(row, property.Name, property.Component);
                row.Cells[0].ToolTipText = property.IsActive ? "Active in this preview." : "Inactive in this preview.";
                if (!property.IsActive) row.Cells[0].Style.ForeColor = Color.Gray;
            }
            usageSummary.Text = grid.Rows.Count == 0 ? "No matching properties." : $"{grid.Rows.Count} properties";
        }
        finally { populatingGrid = false; }
    }

    private void UpdateRow(DataGridViewRow row, string name, int component)
    {
        float value = material!.Parameters[name].Float(component, 0), original = material.OriginalParameters[name].Float(component, 0);
        row.Cells[1].Value = value.ToString("G", CultureInfo.InvariantCulture);
        row.Cells[1].Style.BackColor = value == original ? grid.DefaultCellStyle.BackColor : Color.FromArgb(75, 64, 28);
        row.Cells[1].ErrorText = "";
    }

    private static bool TryValue(object? text, out float value) => BfresMaterialProperty.TryParse(text, out value);

    #endregion

    #region Lighting

    private int exposureHalfStops;
    public string CurrentLighting { get; private set; } = "Day";

    private void InitializeLightingMenu()
    {
        dayLightingItem.Click += (_, _) => SelectLighting("Day");
        nightLightingItem.Click += (_, _) => SelectLighting("Night");
        skyFillItem.CheckedChanged += (_, _) => ApplyLighting();
        darkerItem.Click += (_, _) => ChangeExposure(-1);
        resetExposureItem.Click += (_, _) => { exposureHalfStops = 0; ChangeExposure(0); };
        brighterItem.Click += (_, _) => ChangeExposure(1);
        restoreLightingItem.Click += (_, _) => SelectLighting(CurrentLighting);
        ApplyLighting();
    }

    public void SelectLighting(string name)
    {
        if (name is not ("Day" or "Night")) throw new ArgumentException("Unknown lighting preset.", nameof(name));
        CurrentLighting = name;
        dayLightingItem.Checked = name == "Day"; nightLightingItem.Checked = name == "Night";
        ApplyLighting();
    }

    private void ApplyLighting()
    {
        var preset = CurrentLighting == "Night" ? AcnhLightingPreset.Night : AcnhLightingPreset.Day;
        if (!skyFillItem.Checked) preset = preset with { PreviewIndirectLight = System.Numerics.Vector3.Zero };
        viewport.Renderer.SetLighting(preset);
        UpdateViewerStatus();
        if (hierarchy.SelectedNode?.Tag is BfresMesh mesh) UpdateMaterialEditor(mesh);
    }

    private void ChangeExposure(int step)
    {
        exposureHalfStops = Math.Clamp(exposureHalfStops + step, -8, 8);
        viewport.Renderer.SetExposure(.06f * MathF.Pow(2, exposureHalfStops * .5f));
        exposureItem.Text = $"Exposure: {exposureHalfStops * .5f:+0.0;-0.0;0} EV";
    }

    #endregion

    #region Docking

    private DockContent sceneWindow = null!, viewportWindow = null!, materialWindow = null!;

    private void InitializeDockWindows()
    {
        dockPanel1.Theme = new VS2015DarkTheme();
        sceneWindow = CreatePane("Scene explorer", DockState.DockLeft, hierarchy, new(380, 500));
        var viewportHost = new Panel { Dock = DockStyle.Fill, BackColor = PaneBack, ForeColor = Color.Gainsboro };
        viewportHost.Controls.Add(viewport);
        viewportWindow = CreatePane("3D viewport", DockState.Document, viewportHost, new(380, 500), hideOnClose: false);
        viewportWindow.CloseButton = viewportWindow.CloseButtonVisible = false;
        viewportWindow.DockAreas = DockAreas.Document | DockAreas.Float;
        viewportWindow.DockStateChanged += (_, _) =>
        {
            if (viewportWindow.DockState == DockState.Float && viewportWindow.FloatPane?.FloatWindow is { } window) window.ControlBox = false;
        };
        materialWindow = CreateMaterialWindow();
        foreach (var window in new[] { sceneWindow, materialWindow })
        {
            window.DockStateChanged += (_, _) => UpdatePaneMenuState();
            window.Disposed += (_, _) => UpdatePaneMenuState();
        }
        UpdatePaneMenuState();
    }

    // Panes only hide when the user closes them; the viewport pane can't be closed at all.
    private static DockContent CreatePane(string text, DockState hint, Control content, Size size, bool hideOnClose = true)
    {
        var pane = new DockContent();
        pane.SuspendLayout();
        pane.AutoScaleDimensions = new SizeF(7F, 15F);
        pane.AutoScaleMode = AutoScaleMode.Font;
        pane.ClientSize = size;
        pane.Controls.Add(content);
        pane.HideOnClose = hideOnClose;
        pane.Text = text;
        pane.ShowHint = hint;
        pane.ResumeLayout(false);
        pane.PerformLayout();
        pane.FormClosing += (_, e) =>
        {
            if (e.CloseReason != CloseReason.UserClosing || pane.Disposing) return;
            e.Cancel = true;
            if (hideOnClose) pane.Hide();
        };
        return pane;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (LicenseManager.UsageMode != LicenseUsageMode.Designtime) RestoreDefaultLayout();
    }

    private void RestoreDefaultLayout()
    {
        dockPanel1.SuspendLayout(true);
        try
        {
            dockPanel1.DockLeftPortion = .19;
            dockPanel1.DockRightPortion = .31;
            sceneWindow.Show(dockPanel1, DockState.DockLeft);
            materialWindow.Show(dockPanel1, DockState.DockRight);
            viewportWindow.Show(dockPanel1, DockState.Document);
            viewportWindow.Activate();
        }
        finally { dockPanel1.ResumeLayout(true, true); }
    }

    private void ShowDockWindow(DockContent window)
    {
        if (window.DockPanel == null) window.Show(dockPanel1, window.ShowHint);
        else window.Show();
        window.Activate();
    }

    private void UpdatePaneMenuState()
    {
        static bool IsOpen(DockContent? window) => window is { IsDisposed: false, IsHidden: false } && window.DockState != DockState.Unknown;
        scenePaneItem.Checked = IsOpen(sceneWindow);
        materialPaneItem.Checked = IsOpen(materialWindow);
    }

    private void DisposeDockWindows()
    {
        loading?.Cancel(); shaderLoading?.Cancel();
        viewportWindow?.Dispose();
        sceneWindow?.Dispose();
        materialWindow?.Dispose();
    }

    #endregion
}

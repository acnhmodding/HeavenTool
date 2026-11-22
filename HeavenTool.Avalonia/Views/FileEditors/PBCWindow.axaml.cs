using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Avalonia;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Core;
using FluentAvalonia.UI.Windowing;
using HeavenTool.Avalonia.Controls;
using HeavenTool.Avalonia.ViewModels;
using HeavenTool.IO.FileFormats.PBC;
using HeavenTool.Rendering.PBC;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.Views.FileEditors;

public partial class PBCViewportPane : UserControl
{
    public PBCViewportPane() => AvaloniaXamlLoader.Load(this);
    private PbcViewport ViewportControl => this.FindControl<PbcViewport>("Viewport")!;
    private void FrameAllClicked(object? sender, RoutedEventArgs e) => ViewportControl.Renderer.FrameScene();
    private void FrameSelectionClicked(object? sender, RoutedEventArgs e) => ViewportControl.Renderer.FrameSelection();
}

public partial class PBCMaterialsPane : UserControl
{
    public PBCMaterialsPane() => AvaloniaXamlLoader.Load(this);
}

public partial class PBCInspectorPane : UserControl
{
    public PBCInspectorPane() => AvaloniaXamlLoader.Load(this);
}

public partial class PBCWindow : FAAppWindow, IPBCView
{
    private readonly PBCViewModel viewModel;
    private readonly PBCViewportPane viewportPane = new();
    private readonly PBCMaterialsPane materialsPane = new();
    private readonly PBCInspectorPane inspectorPane = new();
    private readonly PbcViewport viewport;
    private readonly DockControl workspace;
    private Document viewportDocument = null!;
    private Tool materialsTool = null!, inspectorTool = null!;
    private bool meshPending, keepCamera, closeConfirmed, closingPrompt;

    public PBCWindow()
    {
        AvaloniaXamlLoader.Load(this);
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.Height = 32;
        viewModel = new PBCViewModel(this);
        DataContext = viewportPane.DataContext = materialsPane.DataContext = inspectorPane.DataContext = viewModel;
        viewport = viewportPane.FindControl<PbcViewport>("Viewport")!;
        workspace = this.FindControl<DockControl>("Workspace")!;

        viewport.RenderFailed += viewModel.ReportRenderFailure;
        viewport.Renderer.ModelFailed += viewModel.ReportModelFailure;
        viewport.Renderer.ShowModel = viewModel.ShowModel;
        viewport.Renderer.SurfaceOpacity = (float)(viewModel.SurfaceOpacity / 100);
        viewport.TrianglePicked += position => viewModel.Selection = position;
        viewport.StrokeCompleted += viewModel.RecordStroke;
        viewport.HeightStrokeCompleted += viewModel.RecordHeights;
        viewport.TilesPainted += () => ScheduleMesh(keep: true);
        viewport.HoverChanged += viewModel.SetHover;
        viewport.Tool = viewModel.Tool;
        viewport.Brush = viewModel.Brush;
        viewport.BrushSize = viewModel.BrushSize;
        viewport.HeightStep = (float)(viewModel.HeightStep ?? 0);
        viewport.HeightFaces = viewModel.HeightFaces;
        viewModel.TilesChanged += () =>
        {
            ScheduleMesh(keep: true);
            UpdateHighlight();
        };
        viewModel.FileReplaced += () =>
        {
            viewport.File = viewModel.File;
            ScheduleMesh(keep: false);
            UpdateHighlight();
        };
        viewModel.PropertyChanged += OnViewModelChanged;

        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.O, KeyModifiers.Control), Command = viewModel.OpenFileCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.S, KeyModifiers.Control), Command = viewModel.SaveFileCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.S, KeyModifiers.Control | KeyModifiers.Shift), Command = viewModel.SaveAsCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Z, KeyModifiers.Control), Command = viewModel.UndoCommand });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.Y, KeyModifiers.Control), Command = viewModel.RedoCommand });
        AddHandler(KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DragOverEvent, (_, e) =>
            e.DragEffects = !viewModel.IsBusy && DroppedFile(e) != null ? DragDropEffects.Copy : DragDropEffects.None);
        AddHandler(DragDrop.DropEvent, async (_, e) =>
        {
            if (DroppedFile(e) is not { } file) return;
            e.Handled = true;
            await viewModel.OpenFileAsync(file);
        });
        Closing += OnClosing;
        CreateDefaultLayout();
    }

    private static IStorageFile? DroppedFile(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles()?.FirstOrDefault(PBCViewModel.IsPbcFile) as IStorageFile;

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PBCViewModel.Tool): viewport.Tool = viewModel.Tool; break;
            case nameof(PBCViewModel.Brush): viewport.Brush = viewModel.Brush; break;
            case nameof(PBCViewModel.BrushSize): viewport.BrushSize = viewModel.BrushSize; break;
            case nameof(PBCViewModel.HeightStep): viewport.HeightStep = (float)(viewModel.HeightStep ?? 0); break;
            case nameof(PBCViewModel.HeightFaces): viewport.HeightFaces = viewModel.HeightFaces; break;
            case nameof(PBCViewModel.ModelRenderer): ShowModelRenderer(); break;
            case nameof(PBCViewModel.ShowModel): viewport.Renderer.ShowModel = viewModel.ShowModel; break;
            case nameof(PBCViewModel.SurfaceOpacity): viewport.Renderer.SurfaceOpacity = (float)(viewModel.SurfaceOpacity / 100); break;
            case nameof(PBCViewModel.Show3DGrid): viewport.Renderer.ShowGrid = viewModel.Show3DGrid; break;
            case nameof(PBCViewModel.Selection): UpdateHighlight(); break;
        }
    }

    private void ShowModelRenderer()
    {
        // The header offsets are where the grid starts in the model, in tiles: the model's bounds span
        // (offset .. offset + size) × tile size, which is how every romfs PBC lines up with its model.
        if (viewModel.File is { } file)
            viewport.Renderer.ModelOffset = new OpenTK.Mathematics.Vector3(file.OffsetX * PbcGeometry.TileSize, 0, file.OffsetZ * PbcGeometry.TileSize);
        viewport.Renderer.Model = viewModel.ModelRenderer;
    }

    // Painting changes many triangles in a row; the mesh is rebuilt once the UI is idle.
    private void ScheduleMesh(bool keep)
    {
        keepCamera = meshPending ? keepCamera && keep : keep;
        if (meshPending) return;
        meshPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            meshPending = false;
            var mesh = viewModel.File is { } file ? PbcMesh.Build(file) : PbcMesh.Empty;
            viewport.Renderer.SetMesh(mesh, keepCamera);
            // Undo or an inspector edit can move the surface under a still cursor.
            viewport.RefreshHover();
        }, DispatcherPriority.Background);
    }

    private void UpdateHighlight()
    {
        viewport.Renderer.SetHighlight(viewModel.File is { } file && viewModel.Selection is { } at
            ? PbcMesh.Outline(file, at.TileX, at.TileZ, at.Triangle) : []);
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Source is Visual source && source.GetSelfAndVisualAncestors().Any(control => control is TextBox or Menu or MenuItem)) return;
        if (viewport.HandleNavigationKey(e) || e.KeyModifiers != KeyModifiers.None) return;
        if (e.Key == Key.F)
        {
            viewport.Renderer.FrameSelection();
            e.Handled = true;
        }
    }

    private void CreateDefaultLayout()
    {
        var factory = new Factory { HideToolsOnClose = true };
        viewportDocument = new Document { Id = "Viewport", Title = "3D View", Content = viewportPane, CanClose = false, CanFloat = false };
        materialsTool = new Tool { Id = "Materials", Title = "Materials", Content = materialsPane, CanPin = false, CanClose = false, CanFloat = false };
        inspectorTool = new Tool { Id = "Inspector", Title = "Inspector", Content = inspectorPane, CanPin = false, CanClose = false, CanFloat = false };
        var viewportDock = new DocumentDock { Id = "ViewportDock", Proportion = 0.8, CanCreateDocument = false, IsCollapsable = false,
            VisibleDockables = factory.CreateList<IDockable>(viewportDocument), ActiveDockable = viewportDocument };
        var materialsDock = new ToolDock { Id = "MaterialsDock", Alignment = Alignment.Right, Proportion = 0.5,
            VisibleDockables = factory.CreateList<IDockable>(materialsTool), ActiveDockable = materialsTool };
        var inspectorDock = new ToolDock { Id = "InspectorDock", Alignment = Alignment.Right, Proportion = 0.5,
            VisibleDockables = factory.CreateList<IDockable>(inspectorTool), ActiveDockable = inspectorTool };
        var sideDock = new ProportionalDock { Id = "Side", Proportion = 0.2, Orientation = Dock.Model.Core.Orientation.Vertical,
            VisibleDockables = factory.CreateList<IDockable>(materialsDock, new ProportionalDockSplitter(), inspectorDock),
            ActiveDockable = materialsDock };
        var mainDock = new ProportionalDock { Id = "Main", Orientation = Dock.Model.Core.Orientation.Horizontal,
            VisibleDockables = factory.CreateList<IDockable>(viewportDock, new ProportionalDockSplitter(), sideDock),
            ActiveDockable = viewportDock };
        var rootDock = new RootDock { Id = "Root", IsCollapsable = false,
            VisibleDockables = factory.CreateList<IDockable>(mainDock), ActiveDockable = mainDock, DefaultDockable = mainDock };
        factory.InitLayout(rootDock);
        workspace.Factory = factory;
        workspace.Layout = rootDock;
    }

    private void RestoreLayoutClicked(object? sender, RoutedEventArgs e)
    {
        workspace.Layout = null;
        // The old models must release their controls before the new layout takes ownership.
        viewportDocument.Content = null;
        materialsTool.Content = inspectorTool.Content = null;
        CreateDefaultLayout();
    }

    private void CloseClicked(object? sender, RoutedEventArgs e) => Close();

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (closeConfirmed) return;
        e.Cancel = true;
        if (closingPrompt || viewModel.IsBusy) return;
        closingPrompt = true;
        try
        {
            if (await viewModel.ConfirmChangesAsync())
            {
                closeConfirmed = true;
                Close();
            }
        }
        finally { closingPrompt = false; }
    }

    public Task<bool?> ConfirmSaveChangesAsync()
    {
        var dialog = new Window
        {
            Title = "Unsaved PBC changes", Width = 400, Height = 120,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var save = new Button { Content = "Save", IsDefault = true };
        var discard = new Button { Content = "Discard" };
        var cancel = new Button { Content = "Cancel", IsCancel = true };
        save.Click += (_, _) => dialog.Close(true);
        discard.Click += (_, _) => dialog.Close(false);
        cancel.Click += (_, _) => dialog.Close(null);
        var buttons = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8, Children = { save, discard, cancel }
        };
        var grid = new Grid
        {
            Margin = new Thickness(20),
            RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
            Children = { new TextBlock { Text = "Save changes before closing this file?" }, buttons }
        };
        Grid.SetRow(buttons, 1);
        dialog.Content = grid;
        return dialog.ShowDialog<bool?>(this);
    }
}

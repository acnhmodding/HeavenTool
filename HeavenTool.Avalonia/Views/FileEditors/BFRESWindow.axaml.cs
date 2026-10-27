using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using Dock.Model.Avalonia;
using Dock.Model.Avalonia.Controls;
using Dock.Model.Controls;
using FluentAvalonia.UI.Windowing;
using HeavenTool.Avalonia.Controls;
using HeavenTool.Avalonia.ViewModels;
using HeavenTool.IO.FileFormats.BFRES;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.Views.FileEditors;

public partial class BFRESWindow : FAAppWindow
{
    private readonly BfresViewModel viewModel;
    private readonly BfresViewport viewport;
    private readonly BfresScenePane scenePane;
    private readonly BfresViewportPane viewportPane;
    private readonly BfresMaterialPane materialPane;
    private readonly DockControl workspace;
    private readonly HashSet<Window> editorWindows = [];
    private IFactory dockFactory = null!;
    private IDockable sceneTool = null!, materialTool = null!, viewportDocument = null!;
    private IRootDock rootDock = null!;
    private IDock documentDock = null!;
    private bool syncingPanels, resettingLayout, returningViewport, closing;

    public static readonly FuncValueConverter<bool, IBrush?> ChangedBackground = new(changed =>
        changed && Application.Current?.TryGetResource("ChangedValueBackground", null, out var brush) == true ? brush as IBrush : Brushes.Transparent);

    public BFRESWindow()
    {
        AvaloniaXamlLoader.Load(this);
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.Height = 32;
        scenePane = new();
        viewportPane = new();
        materialPane = new();
        viewport = viewportPane.FindControl<BfresViewport>("Viewport")!;
        viewModel = new(viewport.Renderer, StorageProvider);
        DataContext = viewModel;
        scenePane.DataContext = viewportPane.DataContext = materialPane.DataContext = viewModel;
        workspace = this.FindControl<DockControl>("Workspace")!;
        viewport.RenderFailed += viewModel.ReportRenderFailure;
        Closing += (_, _) => closing = true;
        Closed += (_, _) => viewModel.Dispose();
        ConfigureEditorWindow(this);

        var grid = materialPane.FindControl<DataGrid>("MaterialGrid")!;
        grid.Columns[1].CellStyleClasses.Add("value");
        grid.CellEditEnding += (_, e) =>
        {
            if (e.EditAction == DataGridEditAction.Commit && e.EditingElement is TextBox box && !viewModel.CheckValue(box.Text)) e.Cancel = true;
        };
        grid.CellEditEnded += (_, _) => viewModel.MaterialError = null;
        var tree = scenePane.FindControl<TreeView>("SceneTree")!;
        tree.DoubleTapped += (_, _) => { if (viewModel.SelectedNode?.Source is BfresMesh) viewModel.ShowMaterialPanel = true; };
        viewModel.PropertyChanged += (_, e) =>
        {
            if (syncingPanels || resettingLayout) return;
            if (e.PropertyName == nameof(BfresViewModel.ShowScenePanel))
                SetDockVisible(sceneTool, viewModel.ShowScenePanel);
            else if (e.PropertyName == nameof(BfresViewModel.ShowMaterialPanel))
                SetDockVisible(materialTool, viewModel.ShowMaterialPanel);
        };
        CreateDefaultLayout();
    }

    private void ConfigureEditorWindow(Window window)
    {
        if (!editorWindows.Add(window)) return;
        window.Closed += (_, _) => editorWindows.Remove(window);
        window.KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.O, KeyModifiers.Control), Command = viewModel.OpenCommand });
        window.AddHandler(KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(window, true);
        window.AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = DroppedFile(e) != null ? DragDropEffects.Copy : DragDropEffects.None);
        window.AddHandler(DragDrop.DropEvent, async (_, e) =>
        {
            if (closing || DroppedFile(e) is not { } file) return;
            e.Handled = true;
            await viewModel.LoadFileAsync(file);
        });
    }

    private static IStorageFile? DroppedFile(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles()?.FirstOrDefault(BfresViewModel.IsModelFile) as IStorageFile;

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        // Numeric material edits and search boxes must keep their normal keyboard behavior.
        if (e.Handled || e.Source is Visual source && source.GetSelfAndVisualAncestors().Any(control => control is TextBox or DataGrid or Menu or MenuItem)) return;
        if (viewport.HandleNavigationKey(e) || e.KeyModifiers != KeyModifiers.None) return;
        if (e.Key == Key.B) { viewModel.ToggleBonesCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.F) { viewModel.FrameSelectionCommand.Execute(null); e.Handled = true; }
    }

    private void CreateDefaultLayout()
    {
        var factory = new Factory { HideToolsOnClose = true };
        dockFactory = factory;
        sceneTool = new Tool { Id = "Scene", Title = "Scene", Content = scenePane, CanPin = false };
        materialTool = new Tool { Id = "Material", Title = "Material", Content = materialPane, CanPin = false };
        viewportDocument = new Document { Id = "Viewport", Title = "3D View", Content = viewportPane, CanClose = false };
        var sceneDock = new ToolDock { Id = "SceneDock", Alignment = Alignment.Left, Proportion = 0.21,
            IsCollapsable = true, VisibleDockables = factory.CreateList<IDockable>(sceneTool), ActiveDockable = sceneTool };
        var materialDock = new ToolDock { Id = "MaterialDock", Alignment = Alignment.Right, Proportion = 0.27,
            IsCollapsable = true, VisibleDockables = factory.CreateList<IDockable>(materialTool), ActiveDockable = materialTool };
        // Keep a home for the viewport when its document is floating.
        documentDock = new DocumentDock { Id = "ViewportDock", Proportion = 0.52, CanCreateDocument = false, IsCollapsable = false,
            VisibleDockables = factory.CreateList<IDockable>(viewportDocument), ActiveDockable = viewportDocument };
        var mainDock = new ProportionalDock { Id = "Main", Orientation = Dock.Model.Core.Orientation.Horizontal,
            VisibleDockables = factory.CreateList<IDockable>(sceneDock, new ProportionalDockSplitter(), documentDock, new ProportionalDockSplitter(), materialDock),
            ActiveDockable = documentDock };
        rootDock = new RootDock { Id = "Root", IsCollapsable = false,
            VisibleDockables = factory.CreateList<IDockable>(mainDock), ActiveDockable = mainDock, DefaultDockable = mainDock };
        factory.WindowOpened += (_, e) =>
        {
            if (e.Window?.Host is Window host) ConfigureEditorWindow(host);
        };
        // Closing a floating viewport returns it to its document area instead of losing it.
        factory.WindowClosing += (_, e) =>
        {
            if (!closing && !resettingLayout && !returningViewport && e.Window?.Layout is { } layout && factory.FindRoot(viewportDocument) == layout)
                ReturnViewport();
        };
        factory.DockableHidden += (_, e) => SetPanelVisible(e.Dockable, false);
        factory.DockableRestored += (_, e) => SetPanelVisible(e.Dockable, true);
        factory.InitLayout(rootDock);
        workspace.Factory = factory;
        workspace.Layout = rootDock;
    }

    private void CloseClicked(object? sender, RoutedEventArgs e) => Close();
    private void DockViewportClicked(object? sender, RoutedEventArgs e) => ReturnViewport();
    private void RestoreLayoutClicked(object? sender, RoutedEventArgs e)
    {
        resettingLayout = true;
        try
        {
            materialPane.FindControl<DataGrid>("MaterialGrid")!.CancelEdit();
            foreach (var window in rootDock.Windows?.ToArray() ?? []) window.Exit();
            workspace.Layout = null;
            // The old models must release their controls before the new layout takes ownership.
            ((Tool)sceneTool).Content = null;
            ((Tool)materialTool).Content = null;
            ((Document)viewportDocument).Content = null;
            CreateDefaultLayout();
            viewModel.ShowScenePanel = viewModel.ShowMaterialPanel = true;
        }
        finally { resettingLayout = false; }
    }

    private void ReturnViewport()
    {
        if (returningViewport) return;
        returningViewport = true;
        try
        {
            if (viewportDocument.Owner is IDock owner && owner != documentDock)
                dockFactory.MoveDockable(owner, documentDock, viewportDocument, null);
            dockFactory.SetActiveDockable(viewportDocument);
        }
        finally { returningViewport = false; }
    }

    private void SetDockVisible(IDockable dockable, bool visible)
    {
        if (!visible)
        {
            if (dockable == materialTool) materialPane.FindControl<DataGrid>("MaterialGrid")!.CancelEdit();
            dockFactory.HideDockable(dockable);
            return;
        }
        bool closedWindow = dockable.Owner is IRootDock hiddenRoot && hiddenRoot != rootDock &&
            !(rootDock.Windows?.Any(window => window.Layout == hiddenRoot) ?? false);
        dockFactory.RestoreDockable(dockable);
        if (closedWindow)
        {
            dockFactory.RemoveDockable(dockable, false);
            bool scene = dockable == sceneTool;
            var dock = new ToolDock { Id = scene ? "SceneDock" : "MaterialDock",
                Alignment = scene ? Alignment.Left : Alignment.Right, IsCollapsable = true,
                VisibleDockables = dockFactory.CreateList<IDockable>(dockable), ActiveDockable = dockable };
            dockFactory.SplitToDock(documentDock, dock, scene ? DockOperation.Left : DockOperation.Right);
        }
    }

    private void SetPanelVisible(IDockable? dockable, bool visible)
    {
        if (resettingLayout) return;
        syncingPanels = true;
        try
        {
            if (dockable == sceneTool) viewModel.ShowScenePanel = visible;
            else if (dockable == materialTool)
            {
                if (!visible) materialPane.FindControl<DataGrid>("MaterialGrid")!.CancelEdit();
                viewModel.ShowMaterialPanel = visible;
            }
        }
        finally { syncingPanels = false; }
    }

    public Task LoadFileAsync(string path) => viewModel.LoadFileAsync(path);
    public Task LoadBytesAsync(byte[] bytes, string name) => viewModel.LoadBytesAsync(bytes, name);
}

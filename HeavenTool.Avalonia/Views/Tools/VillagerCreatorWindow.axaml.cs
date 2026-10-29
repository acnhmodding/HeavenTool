using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Windowing;
using HeavenTool.Avalonia.Controls;
using HeavenTool.Avalonia.ViewModels.VillagerCreator;
using System.Linq;

namespace HeavenTool.Avalonia.Views.Tools;

public partial class VillagerCreatorWindow : FAAppWindow
{
    private readonly VillagerCreatorViewModel viewModel;
    private readonly BfresViewport viewport;

    public VillagerCreatorWindow()
    {
        AvaloniaXamlLoader.Load(this);
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.Height = 32;
        viewModel = new(StorageProvider, Launcher);
        DataContext = viewModel;

        viewport = this.FindControl<VillagerHouseStep>("HouseStep")!.FindControl<BfresViewport>("Viewport")!;
        viewModel.House.Attach(viewport.Renderer);
        viewport.RenderFailed += viewModel.House.ReportRenderFailure;
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

        Opened += async (_, _) => await viewModel.LoadAsync();
        Closed += (_, _) => viewModel.Dispose();
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        // The preview's camera keys work on the house step, unless a text box or list wants the key.
        if (e.Handled || viewModel.StepIndex != VillagerCreatorViewModel.HouseStep) return;
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().Any(control => control is TextBox or ComboBox or NumericUpDown)) return;
        viewport.HandleNavigationKey(e);
    }
}

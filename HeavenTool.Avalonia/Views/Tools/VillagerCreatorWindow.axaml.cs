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
    private readonly BfresViewport houseViewport, roomViewport, fitViewport;

    public VillagerCreatorWindow()
    {
        AvaloniaXamlLoader.Load(this);
        TitleBar.ExtendsContentIntoTitleBar = true;
        TitleBar.Height = 32;
        viewModel = new(StorageProvider, Launcher);
        viewModel.PickItem = picker => new ItemPickerWindow(picker).ShowDialog<bool>(this);
        DataContext = viewModel;

        houseViewport = this.FindControl<VillagerHouseStep>("HouseStep")!.FindControl<BfresViewport>("Viewport")!;
        viewModel.House.Attach(houseViewport.Renderer);
        houseViewport.RenderFailed += viewModel.House.ReportRenderFailure;
        roomViewport = this.FindControl<VillagerRoomStep>("RoomStep")!.FindControl<BfresViewport>("Viewport")!;
        viewModel.Room.Attach(roomViewport.Renderer);
        roomViewport.RenderFailed += viewModel.Room.ReportRenderFailure;
        fitViewport = this.FindControl<VillagerFitStep>("FitStep")!.FindControl<BfresViewport>("Viewport")!;
        viewModel.Fit.Attach(fitViewport.Renderer);
        fitViewport.RenderFailed += viewModel.Fit.ReportRenderFailure;
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

        Opened += async (_, _) => await viewModel.LoadAsync();
        Closed += (_, _) => viewModel.Dispose();
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        // The preview's camera keys work on the house, room and fit steps, unless a text box or list wants the key.
        var viewport = viewModel.StepIndex switch
        {
            VillagerCreatorViewModel.HouseStep => houseViewport,
            VillagerCreatorViewModel.RoomStep => roomViewport,
            VillagerCreatorViewModel.FitStep => fitViewport,
            _ => null,
        };
        if (e.Handled || viewport == null) return;
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors()
            .Any(control => control is TextBox or ComboBox or NumericUpDown or ListBox)) return;
        viewport.HandleNavigationKey(e);
    }
}

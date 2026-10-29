using CommunityToolkit.Mvvm.ComponentModel;
using HeavenTool.ModManager.Villagers;
using HeavenTool.Rendering.BFRES;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>The house exterior: its shape, wall, roof, door and curtains, with a 3D preview.</summary>
public partial class HouseStepViewModel(VillagerCreatorViewModel creator) : ObservableObject, IDisposable
{
    private VillagerProject? project;
    private BfresRenderer? renderer;
    private CancellationTokenSource? previewLoad;
    private (HousePart Wall, HousePart Roof, HousePart Door)? shown;
    private bool previewVisible, changingShape;

    [ObservableProperty] private IReadOnlyList<int> shapes = [];
    [ObservableProperty] private IReadOnlyList<HousePart> walls = [];
    [ObservableProperty] private IReadOnlyList<HousePart> roofs = [];
    [ObservableProperty] private IReadOnlyList<HousePart> doors = [];
    [ObservableProperty] private int? shape;
    [ObservableProperty] private HousePart? wall;
    [ObservableProperty] private HousePart? roof;
    [ObservableProperty] private HousePart? door;
    [ObservableProperty] private decimal? curtain;
    [ObservableProperty] private bool isPreviewLoading;
    [ObservableProperty] private string? previewWarning;
    [ObservableProperty] private string? renderError;

    /// <summary>The curtain patterns vanilla houses use.</summary>
    public int MaxCurtain => 11;

    internal void Initialize()
    {
        Shapes = creator.Catalog.Shapes;
        Doors = creator.Catalog.Doors;
    }

    /// <summary>Connects the viewport the preview draws in.</summary>
    public void Attach(BfresRenderer value) => renderer = value;

    public void ReportRenderFailure(Exception error) => RenderError = error.Message;

    internal void Load(VillagerProject value)
    {
        project = null;
        var house = value.House ?? new HouseExterior(creator.Catalog.Shapes[0], 0, 0, 0, 0, HouseExterior.NoDeco);
        Shape = house.Shape;
        Wall = Walls.FirstOrDefault(part => part.Id == house.Wall) ?? Walls.FirstOrDefault();
        Roof = Roofs.FirstOrDefault(part => part.Id == house.Roof) ?? Roofs.FirstOrDefault();
        Door = Doors.FirstOrDefault(part => part.Id == house.Door) ?? Doors.FirstOrDefault();
        Curtain = house.Curtain;
        project = value;
        Commit();
    }

    partial void OnShapeChanged(int? value)
    {
        if (value is not { } id) return;
        // Walls and roofs only fit their own shape, so keep the current ones only if they still fit.
        var (wall, roof) = (Wall, Roof);
        // The new wall and roof go in together, as one house.
        changingShape = true;
        try
        {
            Walls = [.. creator.Catalog.WallsFor(id)];
            Roofs = [.. creator.Catalog.RoofsFor(id)];
            Wall = Walls.FirstOrDefault(part => part.Name == wall?.Name) ?? Walls.FirstOrDefault();
            Roof = Roofs.FirstOrDefault(part => part.Name == roof?.Name) ?? Roofs.FirstOrDefault();
        }
        finally { changingShape = false; }
        Commit();
    }

    // A list that changes clears its combo box's selection for a moment; that empty choice isn't the user's.
    partial void OnWallChanged(HousePart? value) { if (value != null) Commit(); }
    partial void OnRoofChanged(HousePart? value) { if (value != null) Commit(); }
    partial void OnDoorChanged(HousePart? value) { if (value != null) Commit(); }
    partial void OnCurtainChanged(decimal? value) { if (value != null) Commit(); }

    private void Commit()
    {
        if (changingShape || project == null || Shape is not { } shape || Wall == null || Roof == null || Door == null) return;
        project.House = new HouseExterior(shape, Wall.Id, Roof.Id, Door.Id, (int)Math.Clamp(Curtain ?? 0, 0, MaxCurtain),
            project.House?.DecoItem ?? HouseExterior.NoDeco);
        RefreshPreview();
    }

    /// <summary>Starts drawing the preview, the first time the step is shown.</summary>
    internal void ShowPreview()
    {
        previewVisible = true;
        RefreshPreview();
    }

    private async void RefreshPreview()
    {
        if (!previewVisible || renderer == null || Wall is not { } wall || Roof is not { } roof || Door is not { } door) return;
        if (shown == (wall, roof, door)) return;

        previewLoad?.Cancel();
        var request = previewLoad = new CancellationTokenSource();
        IsPreviewLoading = true;
        try
        {
            var scene = await creator.Preview.BuildAsync(wall, roof, door, request.Token);
            if (scene == null || request.IsCancellationRequested) return;
            renderer.SetScene(scene.Geometry);
            renderer.SetNativeShaders(scene.Shaders);
            PreviewWarning = scene.Warnings.Count > 0 ? string.Join(Environment.NewLine, scene.Warnings) : null;
            shown = (wall, roof, door);
        }
        catch (Exception ex)
        {
            if (!request.IsCancellationRequested) PreviewWarning = $"Couldn't show the house: {ex.Message}";
        }
        finally
        {
            if (previewLoad == request)
            {
                previewLoad = null;
                IsPreviewLoading = false;
            }
            request.Dispose();
        }
    }

    public void Dispose() => previewLoad?.Cancel();
}

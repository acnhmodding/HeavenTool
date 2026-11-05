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
    private IReadOnlyList<string> sceneWarnings = [];
    private CancellationTokenSource? previewLoad;
    private (HousePart Wall, HousePart Roof, HousePart Door)? shown;
    private HouseScene? scene;
    private bool previewVisible, changingShape;

    [ObservableProperty]
    public partial IReadOnlyList<int> Shapes { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<HousePart> Walls { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<HousePart> Roofs { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<HousePart> Doors { get; set; } = [];

    [ObservableProperty]
    public partial int? Shape { get; set; }

    [ObservableProperty]
    public partial HousePart? Wall { get; set; }

    [ObservableProperty]
    public partial HousePart? Roof { get; set; }

    [ObservableProperty]
    public partial HousePart? Door { get; set; }

    [ObservableProperty]
    public partial decimal? Curtain { get; set; }

    [ObservableProperty]
    public partial bool IsPreviewLoading { get; set; }

    [ObservableProperty]
    public partial string? PreviewWarning { get; set; }

    [ObservableProperty]
    public partial string? RenderError { get; set; }

    /// <summary>The curtain patterns vanilla houses use.</summary>
    public static int MaxCurtain => 11;

    internal void Initialize()
    {
        Shapes = creator.Catalog.Shapes;
    }

    /// <summary>Connects the viewport the preview draws in.</summary>
    public void Attach(BfresRenderer value)
    {
        if (renderer != null) renderer.NativeShaderWarningsChanged -= UpdatePreviewWarning;
        renderer = value;
        renderer.NativeShaderWarningsChanged += UpdatePreviewWarning;
    }

    private void UpdatePreviewWarning()
    {
        var warnings = sceneWarnings.Concat(renderer?.NativeShaderWarnings ?? []).ToArray();
        PreviewWarning = warnings.Length > 0 ? string.Join(Environment.NewLine, warnings) : null;
    }

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
        // Walls and roofs only fit their own shape, and doors only the doorways of some, so keep the current ones only if they
        // still fit. A door of the other top keeps its name.
        var (wall, roof, door) = (Wall, Roof, Door);
        // The new wall and roof go in together, as one house.
        changingShape = true;
        try
        {
            Walls = [.. creator.Catalog.WallsFor(id)];
            Roofs = [.. creator.Catalog.RoofsFor(id)];
            Doors = [.. creator.Catalog.DoorsFor(id)];
            Wall = Walls.FirstOrDefault(part => part.Name == wall?.Name) ?? Walls.FirstOrDefault();
            Roof = Roofs.FirstOrDefault(part => part.Name == roof?.Name) ?? Roofs.FirstOrDefault();
            Door = Doors.FirstOrDefault(part => part.Id == door?.Id) ?? Doors.FirstOrDefault(part => part.Name == door?.Name)
                ?? Doors.FirstOrDefault();
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
        ShowCurtains();
        RefreshPreview();
    }

    // Curtains are only a color, so a new one repaints the house on show instead of building it again.
    private void ShowCurtains()
    {
        if (renderer == null || scene == null || project?.House is not { } house) return;
        var colors = creator.Catalog.CurtainColors;
        if (house.Curtain < 0 || house.Curtain >= colors.Count) return;
        HousePreview.PaintCurtains(scene, colors[house.Curtain]);
        foreach (var material in scene.Curtains) renderer.RefreshMaterial(material);
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
            var built = await creator.Preview.BuildAsync(wall, roof, door, request.Token);
            if (built == null || request.IsCancellationRequested) return;
            scene = built;
            ShowCurtains();
            sceneWarnings = built.Warnings;
            renderer.SetScene(built.Geometry);
            if (built.Lighting != null) renderer.SetLighting(built.Lighting);
            renderer.SetNativeShaders(built.Shaders);
            UpdatePreviewWarning();
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

    public void Dispose()
    {
        if (renderer != null) renderer.NativeShaderWarningsChanged -= UpdatePreviewWarning;
        previewLoad?.Cancel();
        GC.SuppressFinalize(this);
    }
}

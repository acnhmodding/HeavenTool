using HeavenTool.IO.AcnhEnvironment;
using HeavenTool.IO.FileFormats.BFRES;
using HeavenTool.ModManager.Villagers;
using HeavenTool.Rendering.BFRES;
using HeavenTool.Shaders;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>
/// A house exterior put together from its wall, roof and door models, ready to show in a viewport. <see cref="Curtains"/> are
/// the wall's curtain materials, which <see cref="HousePreview.PaintCurtains"/> colors.
/// </summary>
public sealed record HouseScene(BfresRenderer.PreparedScene Geometry, IReadOnlyDictionary<BfresMesh, BfresMeshShaders> Shaders,
    IReadOnlyList<BfresMaterial> Curtains, IReadOnlyList<string> Warnings, AcnhLightingPreset? Lighting);

/// <summary>
/// Builds house previews from the romfs models. Wall and roof models share the house's origin; the door sits on the wall's
/// <c>DoorModel0</c> bone. Models and the game's material archive load once and are reused.
/// </summary>
public sealed class HousePreview(string romfs, Func<AcnhEnvironmentResources> environment)
{
    private const string DoorBone = "DoorModel0";
    // Snow only shows in winter, over the roof.
    private const string WinterMaterial = "mWinterSnow";
    // The game colors the curtains behind the windows by setting this parameter of the wall's curtain material.
    private const string CurtainMaterial = "mCurtain";
    private const string CurtainColor = "const_color2";

    private readonly ConcurrentDictionary<string, Lazy<BfresModel?>> models = new();
    private readonly Lazy<SwitchShaderArchive?> shaders = new(() =>
        BfresScene.FindRomfsShaderArchive(romfs) is { } path ? SwitchShaderArchive.Load(path) : null);

    /// <summary>The house with these parts, or null when <paramref name="cancellation"/> stops it first.</summary>
    public Task<HouseScene?> BuildAsync(HousePart wall, HousePart roof, HousePart door, CancellationToken cancellation) =>
        Task.Run(() =>
        {
            // A newer pick cancels the build often, so the cancellation ends here instead of escaping the task.
            try { return Build(wall, roof, door, cancellation); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return null; }
        });

    private HouseScene? Build(HousePart wall, HousePart roof, HousePart door, CancellationToken cancellation)
    {
        var warnings = new List<string>();
        AcnhLightingPreset? lighting = null;
        try
        {
            lighting = AcnhSceneLighting.Evaluate(environment(), 12);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warnings.Add($"Couldn't load the game's noon lighting: {ex.Message}");
        }

        BfresModel? Load(HousePart part)
        {
            var model = models.GetOrAdd(part.ModelName, _ => new(() => Read(part))).Value;
            if (model == null) warnings.Add($"The {part.Name} model ({part.ModelName}) isn't in the romfs.");
            return model;
        }

        var wallModel = Load(wall);
        var roofModel = Load(roof);
        var doorModel = Load(door);
        if (cancellation.IsCancellationRequested) return null;

        var doorPlace = wallModel?.Bones.FirstOrDefault(bone => bone.Name == DoorBone)?.WorldTransform ?? Matrix4x4.Identity;
        var meshes = new List<BfresMesh>();
        meshes.AddRange(Visible(wallModel));
        meshes.AddRange(Visible(roofModel));
        meshes.AddRange(Visible(doorModel).Select(mesh => mesh.Transformed(doorPlace)));

        var geometry = BfresRenderer.PrepareScene(meshes, cancellation);
        IReadOnlyDictionary<BfresMesh, BfresMeshShaders> programs = new Dictionary<BfresMesh, BfresMeshShaders>();
        try
        {
            if (shaders.Value is { } archive)
            {
                var prepared = BfresScene.PrepareShaders(meshes, archive, cancellation);
                programs = prepared.Programs;
                warnings.AddRange(prepared.Errors.Select(error => $"{error.Key.Name}: {error.Value}"));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warnings.Add($"Couldn't load the game materials: {ex.Message}");
        }
        var curtains = Visible(wallModel).Select(mesh => mesh.Material)
            .Where(material => material.Name == CurtainMaterial && material.Parameters.ContainsKey(CurtainColor))
            .Distinct()
            .ToList();
        return new HouseScene(geometry, programs, curtains, warnings, lighting);
    }

    /// <summary>
    /// Sets the color of a scene's curtains, linear RGBA. The materials belong to the cached wall model, so this repaints every
    /// scene built from it; a viewport showing the scene needs to refresh them.
    /// </summary>
    public static void PaintCurtains(HouseScene scene, Vector4 color)
    {
        float[] channels = [color.X, color.Y, color.Z, color.W];
        foreach (var material in scene.Curtains)
            for (int i = 0; i < channels.Length; i++) material.SetParameterComponent(CurtainColor, i, channels[i]);
    }

    private static IEnumerable<BfresMesh> Visible(BfresModel? model) =>
        model?.Meshes.Where(mesh => !mesh.Material.Name.Contains(WinterMaterial, StringComparison.Ordinal)) ?? [];

    private BfresModel? Read(HousePart part)
    {
        var path = HouseCatalog.ModelPath(romfs, part);
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        return BfresFileReader.Load(stream).SelectMany(asset => asset.Document.Models).FirstOrDefault();
    }
}

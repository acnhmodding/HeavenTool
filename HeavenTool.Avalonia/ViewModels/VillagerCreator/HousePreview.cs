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

/// <summary>A house exterior put together from its wall, roof and door models, ready to show in a viewport.</summary>
public sealed record HouseScene(BfresRenderer.PreparedScene Geometry, IReadOnlyDictionary<BfresMesh, TranslatedShader> Shaders, IReadOnlyList<string> Warnings);

/// <summary>
/// Builds house previews from the romfs models. Wall and roof models share the house's origin; the door sits on the wall's
/// <c>DoorModel0</c> bone. Models and the game's material archive load once and are reused.
/// </summary>
public sealed class HousePreview(string romfs)
{
    private const string DoorBone = "DoorModel0";
    // Snow only shows in winter, over the roof.
    private const string WinterMaterial = "mWinterSnow";

    private readonly ConcurrentDictionary<string, Lazy<BfresModel?>> models = new();
    private readonly Lazy<SwitchShaderArchive?> shaders = new(() =>
        BfresScene.FindRomfsShaderArchive(romfs) is { } path ? SwitchShaderArchive.Load(path) : null);

    public Task<HouseScene> BuildAsync(HousePart wall, HousePart roof, HousePart door, CancellationToken cancellation) =>
        Task.Run(() => Build(wall, roof, door, cancellation), cancellation);

    private HouseScene Build(HousePart wall, HousePart roof, HousePart door, CancellationToken cancellation)
    {
        var warnings = new List<string>();
        BfresModel? Load(HousePart part)
        {
            var model = models.GetOrAdd(part.ModelName, _ => new(() => Read(part))).Value;
            if (model == null) warnings.Add($"The {part.Name} model ({part.ModelName}) isn't in the romfs.");
            return model;
        }

        var wallModel = Load(wall);
        var roofModel = Load(roof);
        var doorModel = Load(door);
        cancellation.ThrowIfCancellationRequested();

        var doorPlace = wallModel?.Bones.FirstOrDefault(bone => bone.Name == DoorBone)?.WorldTransform ?? Matrix4x4.Identity;
        var meshes = new List<BfresMesh>();
        meshes.AddRange(Visible(wallModel));
        meshes.AddRange(Visible(roofModel));
        meshes.AddRange(Visible(doorModel).Select(mesh => mesh.Transformed(doorPlace)));

        var geometry = BfresRenderer.PrepareScene(meshes, cancellation);
        IReadOnlyDictionary<BfresMesh, TranslatedShader> programs = new Dictionary<BfresMesh, TranslatedShader>();
        try
        {
            if (shaders.Value is { } archive) programs = BfresScene.PrepareShaders(meshes, archive, cancellation).Programs;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warnings.Add($"Couldn't load the game materials: {ex.Message}");
        }
        return new HouseScene(geometry, programs, warnings);
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

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

/// <summary>A villager's room put together from the room models and its items, ready to show in a viewport.</summary>
public sealed record RoomScene(BfresRenderer.PreparedScene Geometry, IReadOnlyDictionary<BfresMesh, TranslatedShader> Shaders,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Builds room previews from the romfs models: the floor and the back and side walls of a 6×6 room, with its wallpaper and
/// flooring, and the items in it. The front wall is left out so the room can be seen into. Models, textures and translated
/// game shaders load once and are reused.
/// </summary>
public sealed class RoomPreview(VillagerGameData data)
{
    // One half tile is 5 units, and the room's middle is between cells 31 and 32.
    private const float Cell = 5;
    private const int Middle = 32;
    private const float HalfRoom = (VillagerRoom.LastCell + 1 - Middle) * Cell;
    // A wall model is one 2-tile piece, facing +Z.
    private const float WallPiece = 20;

    private const string FloorModel = "RoomMdlFloor6x6";
    private const string WallModel = "RoomMdlWall";
    private const string FloorMaterial = "mDummyFloor";
    private const string WallMaterial = "mDummyWall";
    // The room models' shadow casters and the strip around the floor only show from outside.
    private static readonly string[] HiddenMaterials = ["mShadow", "mOutside"];

    private readonly ConcurrentDictionary<string, Lazy<BfresModel?>> models = new();
    private readonly ConcurrentDictionary<string, Lazy<IReadOnlyList<BntxTexture>>> textures = new();
    private readonly ConcurrentDictionary<BfresMesh, TranslatedShader?> programs = new();
    private readonly Lazy<SwitchShaderArchive?> shaders = new(() =>
        BfresScene.FindRomfsShaderArchive(data.Romfs) is { } path ? SwitchShaderArchive.Load(path) : null);

    /// <summary>The room, or null when <paramref name="cancellation"/> stops it first.</summary>
    public Task<RoomScene?> BuildAsync(VillagerRoom room, CancellationToken cancellation) =>
        Task.Run(() =>
        {
            // A newer edit cancels the build often, so the cancellation ends here instead of escaping the task.
            try { return Build(room, cancellation); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return null; }
        });

    private RoomScene? Build(VillagerRoom room, CancellationToken cancellation)
    {
        var warnings = new List<string>();
        // Each placed mesh, with the model's own mesh its game shader is translated from.
        var placed = new List<(BfresMesh Source, BfresMesh Mesh)>();
        void Place(BfresModel model, Matrix4x4 transform, IReadOnlyDictionary<string, BntxTexture> swap)
        {
            foreach (var mesh in model.Meshes.Where(mesh => !HiddenMaterials.Contains(mesh.Material.Name)))
                placed.Add((mesh, (swap.Count > 0 ? mesh.WithMaterial(mesh.Material.WithTextures(swap)) : mesh).Transformed(transform)));
        }

        if (Model(FloorModel) is { } floor) Place(floor, Matrix4x4.Identity, Surface(room.Flooring, FloorMaterial));
        if (Model(WallModel) is { } wall)
        {
            var wallpaper = Surface(room.Wallpaper, WallMaterial);
            foreach (var layer in (RoomLayer[])[RoomLayer.BackWall, RoomLayer.LeftWall, RoomLayer.RightWall])
                for (float along = -HalfRoom + WallPiece / 2; along < HalfRoom; along += WallPiece)
                    Place(wall, Facing(layer) * Matrix4x4.CreateTranslation(WallPoint(layer, along, 0)), wallpaper);
        }
        cancellation.ThrowIfCancellationRequested();

        var missing = new List<string>();
        var floorItems = new List<(Footprint Area, float Top)>();
        // Items on furniture stand on what's under them, so the floor goes first.
        foreach (var item in room.Items.OrderBy(item => item.Layer == RoomLayer.OnFurniture))
        {
            cancellation.ThrowIfCancellationRequested();
            var resource = data.ItemResource(item.Id);
            // Room markers stand in for things the game puts there itself, and have no model.
            if (string.IsNullOrEmpty(resource)) continue;
            if (Model(resource) is not { } model)
            {
                missing.Add(data.ItemNames.GetValueOrDefault(item.Id) ?? item.Id.ToString());
                continue;
            }

            var size = data.ItemSize(item.Id);
            var (transform, top) = Placement(item, size, floorItems);
            Place(model, transform, Remake(resource, item.Variant));
            if (item.Layer == RoomLayer.Floor) floorItems.Add((Area(item, size), top + Top(model)));
        }
        if (missing.Count > 0)
            warnings.Add($"No model to show for {string.Join(", ", missing.Distinct())}.");

        var meshes = placed.Select(piece => piece.Mesh).ToList();
        var geometry = BfresRenderer.PrepareScene(meshes, cancellation);
        var translated = new Dictionary<BfresMesh, TranslatedShader>();
        try
        {
            if (shaders.Value is { } archive)
            {
                var sources = placed.Select(piece => piece.Source).Distinct().Where(source => !programs.ContainsKey(source)).ToList();
                var prepared = BfresScene.PrepareShaders(sources, archive, cancellation).Programs;
                foreach (var source in sources) programs.TryAdd(source, prepared.GetValueOrDefault(source));
                foreach (var (source, mesh) in placed)
                    if (programs.GetValueOrDefault(source) is { } program) translated[mesh] = program;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warnings.Add($"Couldn't load the game materials: {ex.Message}");
        }
        return new RoomScene(geometry, translated, warnings);
    }

    /// <summary>Where an item's model goes, and the height it stands at.</summary>
    private static (Matrix4x4 Transform, float Floor) Placement(RoomItem item, ItemFootprint size, List<(Footprint Area, float Top)> floorItems)
    {
        if (item.OnWall)
        {
            // Wall models hang from their middle, and rows count down from the ceiling.
            float along = Edge(item.X) + size.X * Cell / 2;
            float height = (VillagerRoom.TopRow + 8 - item.Z - size.Z / 2f) * Cell;
            return (Facing(item.Layer) * Matrix4x4.CreateTranslation(WallPoint(item.Layer, along, height)), 0);
        }

        var area = Area(item, size);
        float floor = 0;
        // An item on furniture stands on the floor item under its middle.
        if (item.Layer == RoomLayer.OnFurniture)
            floor = floorItems.LastOrDefault(under => under.Area.Contains(area.MiddleX, area.MiddleZ)).Top;
        var position = new Vector3(Edge(0) + area.MiddleX * Cell, floor, Edge(0) + area.MiddleZ * Cell);
        return (Matrix4x4.CreateRotationY(item.Rotation * MathF.PI / 2) * Matrix4x4.CreateTranslation(position), floor);
    }

    // The cells an item covers on the floor, from its corner, turned with it.
    private readonly record struct Footprint(int X, int Z, int Width, int Depth)
    {
        public float MiddleX => X + Width / 2f;
        public float MiddleZ => Z + Depth / 2f;
        public bool Contains(float x, float z) => x >= X && x <= X + Width && z >= Z && z <= Z + Depth;
    }

    private static Footprint Area(RoomItem item, ItemFootprint size) => item.Rotation % 2 == 0
        ? new(item.X, item.Z, size.X, size.Z)
        : new(item.X, item.Z, size.Z, size.X);

    // The world coordinate of a cell's back or left edge.
    private static float Edge(int cell) => (cell - Middle) * Cell;

    // A model facing +Z, turned to face away from a wall.
    private static Matrix4x4 Facing(RoomLayer wall) => Matrix4x4.CreateRotationY((RoomItem.WallRotation(wall) ?? 0) * MathF.PI / 2);

    // A point on a wall, at a distance along it from the room's middle and a height above the floor.
    private static Vector3 WallPoint(RoomLayer wall, float along, float height) => wall switch
    {
        RoomLayer.LeftWall => new(-HalfRoom, height, along),
        RoomLayer.FrontWall => new(along, height, HalfRoom),
        RoomLayer.RightWall => new(HalfRoom, height, along),
        _ => new(along, height, -HalfRoom),
    };

    // The highest point of a model, which is about where things stand on it.
    private static float Top(BfresModel model)
    {
        float top = 0;
        foreach (var mesh in model.Meshes.Where(mesh => !HiddenMaterials.Contains(mesh.Material.Name)))
            foreach (var position in mesh.Positions.Span) top = MathF.Max(top, position.Y);
        return top;
    }

    /// <summary>
    /// A wallpaper's or flooring's textures, keyed by the names the room model's placeholder material asks for. A
    /// <c>RoomTexWall</c> file names its textures by its own name, with the same suffixes: <c>FlowerPop00_Alb</c> for
    /// <c>mDummyWall_Alb</c>.
    /// </summary>
    private Dictionary<string, BntxTexture> Surface(int item, string material)
    {
        var swap = new Dictionary<string, BntxTexture>();
        if (data.ItemResource(item) is not { Length: > 0 } resource) return swap;
        foreach (var texture in Textures(resource))
            if (texture.Name.LastIndexOf('_') is var split and > 0) swap.TryAdd(material + texture.Name[split..], texture);
        return swap;
    }

    /// <summary>
    /// The textures of a remake: furniture keeps its body and fabric colors in <c>&lt;resource&gt;ReBody&lt;n&gt;</c> and
    /// <c>ReFabric&lt;n&gt;</c> files, named as its materials ask for them. The first one stands in for a remake the romfs lacks.
    /// </summary>
    private Dictionary<string, BntxTexture> Remake(string resource, uint variant)
    {
        var swap = new Dictionary<string, BntxTexture>();
        foreach (var (part, index) in (ReadOnlySpan<(string, uint)>)[("ReBody", variant & 31), ("ReFabric", variant >> 5 & 31)])
        {
            var found = Textures(resource + part + index);
            foreach (var texture in found.Count > 0 ? found : Textures(resource + part + 0)) swap.TryAdd(texture.Name, texture);
        }
        return swap;
    }

    private BfresModel? Model(string name) => models.GetOrAdd(name, _ => new(() =>
    {
        if (Open(name) is not { } stream) return null;
        using (stream) return BfresFileReader.Load(stream).SelectMany(asset => asset.Document.Models).FirstOrDefault();
    })).Value;

    private IReadOnlyList<BntxTexture> Textures(string name) => textures.GetOrAdd(name, _ => new(() =>
    {
        if (Open(name) is not { } stream) return [];
        using (stream) return BfresFileReader.LoadTextures(stream);
    })).Value;

    private FileStream? Open(string name)
    {
        var path = Path.Combine(data.Romfs, "Model", name + ".Nin_NX_NVN.zs");
        return File.Exists(path) ? File.OpenRead(path) : null;
    }
}

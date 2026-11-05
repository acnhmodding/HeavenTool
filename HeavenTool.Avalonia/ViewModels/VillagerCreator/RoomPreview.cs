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
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>A villager's room put together from the room models and its items, ready to show in a viewport.</summary>
public sealed record RoomScene(BfresRenderer.PreparedScene Geometry, IReadOnlyDictionary<BfresMesh, BfresMeshShaders> Shaders,
    IReadOnlyList<string> Warnings, AcnhLightingPreset? Lighting);

/// <summary>
/// Builds room previews from the romfs models: the floor and the back and side walls of a 6×6 room, with its wallpaper and
/// flooring, and the items in it. The front wall and the ceiling are left out so the room can be seen into, but the environment
/// probe still sees them, as the game's does. Models, textures and translated game shaders load once and are reused.
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
    // Authored at the top of the walls; the game paints it with the wallpaper.
    private const string CeilingModel = "RoomMdlCeling6x6";
    private const string FloorMaterial = "mDummyFloor";
    private const string WallMaterial = "mDummyWall";
    // Keep authored shadow-only meshes for the light pass; only the outside floor strip is excluded from the scene.
    private static readonly string[] HiddenMaterials = ["mOutside"];

    private const string SharedTextures = "RoomTexShareTexture";
    // The room maps the placeholder materials ask for, by the shared texture the game gives them. Without them the floor reads
    // as all edge, which the floor material shades darkest.
    private static readonly (string Name, string From)[] SharedMaps =
    [
        ("mDummyFloor_FlrGrd", "RoomTexShareTextureFloor_FlrGrd"),
        ("mDummyFloor_OclMlp", "RoomTexShareTextureFloorAO6x6_OclMlp"),
    ];

    // Gift spots show as faint boxes, drawn by the preview's own shader: a flat color, mostly see-through.
    private const float SpotHeight = 4, SpotDepth = 1, SpotInset = .5f;
    private static readonly BfresMaterial SpotMaterial = new()
    {
        Name = "GiftSpot", ShaderArchive = "Park_UBER", ShadingModel = "park_uber",
        AttributeAssignments = new Dictionary<string, string>(), SamplerAssignments = new Dictionary<string, string>(),
        ShaderOptions = new Dictionary<string, string> { ["material_diffuse_color"] = "100" },
        OriginalParameters = new Dictionary<string, BfresShaderParameter>
        {
            ["const_color0"] = new(15, MemoryMarshal.AsBytes<float>([.12f, .3f, .4f, 1]).ToArray()),
            ["material_force_alpha"] = new(12, MemoryMarshal.AsBytes<float>([.3f]).ToArray()),
        },
        RenderInfos = new Dictionary<string, BfresRenderInfo>
        {
            ["gsys_render_state_mode"] = new(["translucent"], [], []),
            ["gsys_render_state_blend_mode"] = new(["color"], [], []),
        },
        Samplers = [],
    };

    private readonly ConcurrentDictionary<string, Lazy<IReadOnlyList<BfresModel>>> models = new();
    private readonly ConcurrentDictionary<string, Lazy<IReadOnlyList<BntxTexture>>> textures = new();
    private readonly ConcurrentDictionary<BfresMesh, BfresMeshShaders?> programs = new();
    private readonly ConcurrentDictionary<BfresMesh, string> shaderErrors = new();
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
        AcnhLightingPreset? lighting = null;
        try
        {
            lighting = AcnhSceneLighting.Evaluate(data.Environment, 12, inside: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warnings.Add($"Couldn't load the game's noon room lighting: {ex.Message}");
        }

        // Each placed mesh, with the model's own mesh its game shader is translated from, which gift spots don't have.
        var placed = new List<(BfresMesh? Source, BfresMesh Mesh)>();
        var captureOnly = new HashSet<BfresMesh>();
        void Place(BfresModel model, Matrix4x4 transform, IReadOnlyDictionary<string, BntxTexture> swap, bool unseen = false)
        {
            foreach (var mesh in model.Meshes.Where(mesh => !HiddenMaterials.Contains(mesh.Material.Name) && (!unseen || mesh.Material.InEnvironmentCapture)))
            {
                var piece = (swap.Count > 0 ? mesh.WithMaterial(mesh.Material.WithTextures(swap)) : mesh).Transformed(transform);
                placed.Add((mesh, piece));
                if (unseen) captureOnly.Add(piece);
            }
        }

        // A special wallpaper or flooring brings its own room pieces, which the game draws in place of the plain ones.
        if (Special(room.Flooring, FloorModel["RoomMdl".Length..]) is { } specialFloor) Place(specialFloor, Matrix4x4.Identity, NoSwap);
        else if (Model(FloorModel) is { } floor) Place(floor, Matrix4x4.Identity, Surface(room.Flooring, FloorMaterial));
        var specialWall = Special(room.Wallpaper, WallModel["RoomMdl".Length..]);
        if ((specialWall ?? Model(WallModel)) is { } wall)
        {
            var wallpaper = specialWall != null ? NoSwap : Surface(room.Wallpaper, WallMaterial);
            var window = specialWall != null ? null : Window(room.Wallpaper);
            foreach (var layer in (RoomLayer[])[RoomLayer.BackWall, RoomLayer.LeftWall, RoomLayer.RightWall, RoomLayer.FrontWall])
                for (float along = -HalfRoom + WallPiece / 2; along < HalfRoom; along += WallPiece)
                {
                    var at = Facing(layer) * Matrix4x4.CreateTranslation(WallPoint(layer, along, 0));
                    bool unseen = layer == RoomLayer.FrontWall;
                    // The side walls' middle pieces are the room's windows.
                    if (window is { } w && along == 0 && layer is RoomLayer.LeftWall or RoomLayer.RightWall)
                    {
                        Place(w.Wall, at, wallpaper, unseen);
                        if (w.Frame != null) Place(w.Frame, at, NoSwap, unseen);
                        if (w.Curtain != null) Place(w.Curtain, at, w.CurtainTextures, unseen);
                    }
                    else Place(wall, at, wallpaper, unseen);
                }
            var specialCeiling = Special(room.Wallpaper, CeilingModel["RoomMdl".Length..]);
            if ((specialCeiling ?? Model(CeilingModel)) is { } ceiling)
                Place(ceiling, Matrix4x4.Identity, specialCeiling != null ? NoSwap : Surface(room.Wallpaper, WallMaterial), unseen: true);
        }
        cancellation.ThrowIfCancellationRequested();

        var missing = new List<string>();
        var floorItems = new List<(Footprint Area, float RideHeight)>();
        // Items on furniture stand on what's under them, so the floor goes first.
        foreach (var item in room.Items.OrderBy(item => item.Layer == RoomLayer.OnFurniture))
        {
            cancellation.ThrowIfCancellationRequested();
            var resource = data.ItemResource(item.Id);
            // Room markers hold spots for things the game puts there itself, and have no model.
            if (data.RoomMarkers.ContainsKey(item.Id))
            {
                var spot = data.ItemSize(item.Id);
                placed.Add((null, Spot(spot, item.OnWall).Transformed(Placement(item, spot, floorItems))));
                continue;
            }
            if (string.IsNullOrEmpty(resource)) continue;
            if (Model(resource) is not { } model)
            {
                missing.Add(data.ItemNames.GetValueOrDefault(item.Id) ?? item.Id.ToString());
                continue;
            }

            var size = data.ItemSize(item.Id);
            Place(model, Placement(item, size, floorItems), Remake(resource, item.Variant));
            if (item.Layer == RoomLayer.Floor)
                floorItems.Add((Area(item, size), data.ItemRideHeight(item.Id)));
        }
        if (missing.Count > 0)
            warnings.Add($"No model to show for {string.Join(", ", missing.Distinct())}.");

        var meshes = placed.Select(piece => piece.Mesh).ToList();
        var geometry = BfresRenderer.PrepareScene(meshes, cancellation, captureOnly);
        var translated = new Dictionary<BfresMesh, BfresMeshShaders>();
        try
        {
            if (shaders.Value is { } archive)
            {
                var sources = placed.Select(piece => piece.Source).OfType<BfresMesh>().Distinct().ToList();
                var unprepared = sources.Where(source => !programs.ContainsKey(source)).ToList();
                var prepared = BfresScene.PrepareShaders(unprepared, archive, cancellation);
                foreach (var source in unprepared) programs.TryAdd(source, prepared.Programs.GetValueOrDefault(source));
                foreach (var error in prepared.Errors) shaderErrors.TryAdd(error.Key, error.Value);

                foreach (var (source, mesh) in placed)
                    if (source != null && programs.GetValueOrDefault(source) is { } program) translated[mesh] = program;
                foreach (var source in sources)
                    if (shaderErrors.TryGetValue(source, out var error)) warnings.Add($"{source.Name}: {error}");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warnings.Add($"Couldn't load the game materials: {ex.Message}");
        }
        return new RoomScene(geometry, translated, warnings, lighting);
    }

    /// <summary>Where an item's model goes.</summary>
    private static Matrix4x4 Placement(RoomItem item, ItemFootprint size, List<(Footprint Area, float RideHeight)> floorItems)
    {
        if (item.OnWall)
        {
            // Wall models hang from their middle, and rows count down from the ceiling.
            float along = Edge(item.X) + size.X * Cell / 2;
            float height = (VillagerRoom.TopRow + 8 - item.Z - size.Z / 2f) * Cell;
            return Facing(item.Layer) * Matrix4x4.CreateTranslation(WallPoint(item.Layer, along, height));
        }

        var area = Area(item, size);
        float floor = 0;
        // An item on furniture stands on the floor item under its middle.
        if (item.Layer == RoomLayer.OnFurniture)
            floor = floorItems.LastOrDefault(under => under.Area.Contains(area.MiddleX, area.MiddleZ)).RideHeight;
        var position = new Vector3(Edge(0) + area.MiddleX * Cell, floor, Edge(0) + area.MiddleZ * Cell);
        return Matrix4x4.CreateRotationY(item.Rotation * MathF.PI / 2) * Matrix4x4.CreateTranslation(position);
    }

    /// <summary>
    /// A gift spot's box, placed like a model: a low block on the floor, from the floor up, or a thin panel hanging from its middle
    /// on a wall. It's a little smaller than its cells, so spots side by side stay apart.
    /// </summary>
    private static BfresMesh Spot(ItemFootprint size, bool onWall)
    {
        float width = size.X * Cell / 2 - SpotInset, depth = size.Z * Cell / 2 - SpotInset;
        var (min, max) = onWall
            ? (new Vector3(-width, -depth, 0), new Vector3(width, depth, SpotDepth))
            : (new Vector3(-width, 0, -depth), new Vector3(width, SpotHeight, depth));
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var indices = new List<uint>();
        foreach (var normal in (Vector3[])[Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ])
        {
            // Two axes across the face, each corner taken from the min or max side.
            var (u, v) = normal.X != 0 ? (Vector3.UnitY, Vector3.UnitZ) : normal.Y != 0 ? (Vector3.UnitZ, Vector3.UnitX) : (Vector3.UnitX, Vector3.UnitY);
            uint first = (uint)positions.Count;
            foreach (var (a, b) in (ReadOnlySpan<(float, float)>)[(0, 0), (1, 0), (1, 1), (0, 1)])
            {
                var side = (normal + u * (a * 2 - 1) + v * (b * 2 - 1) + Vector3.One) / 2;
                positions.Add(min + (max - min) * side);
                normals.Add(normal);
            }
            indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3]);
        }
        return new BfresMesh
        {
            Name = "GiftSpot", Material = SpotMaterial, Positions = positions.ToArray(), Normals = normals.ToArray(),
            Indices = indices.ToArray(), NativeAttributes = new Dictionary<string, ReadOnlyMemory<Vector4>>(),
        };
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

    /// <summary>
    /// A wallpaper's or flooring's textures, keyed by the names the room model's placeholder material asks for. A
    /// <c>RoomTexWall</c> file names its textures by its own name, with the same suffixes: <c>FlowerPop00_Alb</c> for
    /// <c>mDummyWall_Alb</c>.
    /// </summary>
    private Dictionary<string, BntxTexture> Surface(int item, string material)
    {
        var swap = new Dictionary<string, BntxTexture>();
        if (data.ItemResource(item) is { Length: > 0 } resource)
            foreach (var texture in Textures(resource))
                if (texture.Name.LastIndexOf('_') is var split and > 0) swap.TryAdd(material + texture.Name[split..], texture);
        // The maps every room shares come from one file, under names of their own.
        var shared = Textures(SharedTextures);
        foreach (var (name, from) in SharedMaps)
            if (name.StartsWith(material, StringComparison.Ordinal) && shared.FirstOrDefault(texture => texture.Name == from) is { } texture)
                swap.TryAdd(name, texture);
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

    private static readonly Dictionary<string, BntxTexture> NoSwap = [];

    private const string CurtainMaterial = "mDummyCurtain";

    // The wall piece each RoomWindowParam wall type cuts its opening with, by the type's name hash.
    private static readonly Dictionary<uint, string> WindowWalls = new()
    {
        [3394203039] = "RoomMdlWindowWallSquare2m",
        [3541786703] = "RoomMdlWindowWallCircle2m",
        [1478528372] = "RoomMdlWindowWallArch2m",
    };

    private sealed record RoomWindow(BfresModel Wall, BfresModel? Frame, BfresModel? Curtain, IReadOnlyDictionary<string, BntxTexture> CurtainTextures);

    /// <summary>
    /// The window a wallpaper comes with: its RoomWallParam row names a RoomWindowParam frame, whose wall type picks the wall
    /// piece with the opening, and a RoomCurtainParam curtain painted with a RoomCurtainTexParam texture. Null for none.
    /// </summary>
    private RoomWindow? Window(int wallpaper)
    {
        var walls = data.Table("RoomWallParam");
        int item = walls.ColumnIndex("ItemTableId u16");
        var row = walls.Entries.FirstOrDefault(entry => Convert.ToInt32(entry[item]) == wallpaper);
        if (row == null) return null;

        string? Resource(string table, int id, out object[]? found)
        {
            var param = data.Table(table);
            int unique = param.ColumnIndex("UniqueID u16"), resource = param.ColumnIndex("ResourceName string64");
            found = param.Entries.FirstOrDefault(entry => Convert.ToInt32(entry[unique]) == id);
            return found?[resource] is string { Length: > 0 } name ? name : null;
        }

        var windows = data.Table("RoomWindowParam");
        string? frame = Resource("RoomWindowParam", Convert.ToInt32(row[walls.ColumnIndex("WindowUniqueID u16")]), out var windowRow);
        if (windowRow == null || !WindowWalls.TryGetValue(Convert.ToUInt32(windowRow[windows.ColumnIndex("RoomWindowWallType.hshCstringRef")]), out var piece)
            || Model(piece) is not { } wall)
            return null;

        string? curtain = Resource("RoomCurtainParam", Convert.ToInt32(row[walls.ColumnIndex("CurtainUniqueID u16")]), out _);
        string? fabric = Resource("RoomCurtainTexParam", Convert.ToInt32(row[walls.ColumnIndex("CurtainTexUniqueID u16")]), out _);
        // A curtain texture file names its maps by its own name, as a wallpaper's does: BlindPlastic00_Alb for mDummyCurtain_Alb.
        var swap = new Dictionary<string, BntxTexture>();
        if (fabric != null)
            foreach (var texture in Textures(fabric))
                if (texture.Name.LastIndexOf('_') is var split and > 0) swap.TryAdd(CurtainMaterial + texture.Name[split..], texture);
        return new(wall, frame != null ? Model(frame) : null, curtain != null ? Opened(curtain) : null, swap);
    }

    /// <summary>A curtain held at the end of its Open animation, as the game shows it by day; one without the animation as authored.</summary>
    private BfresModel? Opened(string name)
    {
        if (Open(name) is not { } stream) return null;
        using (stream)
        {
            var document = BfresFileReader.Load(stream).Select(asset => asset.Document).FirstOrDefault(document => document.Models.Count > 0);
            if (document == null) return null;
            var open = document.SkeletalAnimations.FirstOrDefault(animation => animation.Name == "Open");
            return open != null ? document.Models[0].Posed(open, open.FrameCount) : document.Models[0];
        }
    }

    /// <summary>
    /// A special wallpaper's or flooring's own room piece, such as <c>RoomSpWallHighLands00Wall</c>, or null for a plain one. The
    /// plain pieces would read its maps wrong: a special wallpaper's mix map has no occlusion, which the plain wall takes from it.
    /// </summary>
    private BfresModel? Special(int item, string piece) => data.ItemResource(item) is { Length: > 0 } resource
        ? Models(resource).FirstOrDefault(model => model.Name == resource + piece) : null;

    private BfresModel? Model(string name) => Models(name).FirstOrDefault();

    private IReadOnlyList<BfresModel> Models(string name) => models.GetOrAdd(name, _ => new(() =>
    {
        if (Open(name) is not { } stream) return [];
        using (stream) return [.. BfresFileReader.Load(stream).SelectMany(asset => asset.Document.Models)];
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

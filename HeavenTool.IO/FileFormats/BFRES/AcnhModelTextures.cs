#nullable enable
using System.Collections.Concurrent;

namespace HeavenTool.IO.FileFormats.BFRES;

/// <summary>
/// Finds the textures a model asks for but doesn't embed, in the files the game binds them from:
/// <list type="bullet">
/// <item>Its own <c>&lt;Model&gt;_Tex_&lt;texture&gt;</c> files, which ActorModelResLoader names with "%s_Tex_%s".</item>
/// <item>Its per-language archive from LocalizeNameConvertParam (<see cref="AcnhLocalizedNames"/>), which holds sign textures.</item>
/// <item>For field models, FieldUnitModelMgr's shared pool: FldUnit and the unit archives, in the game's order.</item>
/// <item>A ShareTexture file, which each actor type binds by name (e.g. one binds both PltShareTexture and FenceShareTexture). The
/// file named like the model's prefix (HouseWall… → HouseWallShareTexture) is taken, else the one file holding the texture.</item>
/// </list>
/// </summary>
public static class AcnhModelTextures
{
    public const string ArchiveSuffix = ".Nin_NX_NVN.zs";

    private static readonly string[] FieldPool = ["FldUnit", "FldProcGrass", "FldUnitCliff", "FldUnitFall", "FldUnitRiver", "FldUnitRoadStone",
        "FldUnitRoadSoil", "FldUnitRoadBrick", "FldUnitRoadDarkSoil", "FldUnitRoadFanPattern", "FldUnitRoadSand", "FldUnitRoadTile",
        "FldUnitRoadWood", "FldUnitRoadMyDesign"];
    // What FieldUnitModelMgr adds to the pool on a Happy Home Paradise island (FldUnitWherearen, FldUnitWherearenGardenSand) or a
    // Mystery Tour (FldUnitWherearen, FldUnitMileToursSP). Only those islands' models ask for their textures.
    private static readonly string[] IslandFieldPool = ["FldUnitWherearen", "FldUnitWherearenGardenSand", "FldUnitMileToursSP"];
    private const string ShareSuffix = "ShareTexture";

    // The shared files are the same for every model; their textures are read once.
    private static readonly ConcurrentDictionary<(string Path, DateTime Written), IReadOnlyList<BntxTexture>> cache = new();

    /// <summary>
    /// The textures found for <paramref name="wanted"/>, looked for in <paramref name="folders"/> in order, and the names no file
    /// holds. <paramref name="model"/> is the model archive's name, e.g. IdrMarket01; <paramref name="localized"/> is its
    /// per-language texture archive (<see cref="AcnhLocalizedNames.TextureArchive"/>), e.g. StrcMarketA02_USen.
    /// </summary>
    public static (Dictionary<string, BntxTexture> Found, List<string> Missing) Resolve(string model, IReadOnlyList<string> folders,
        IReadOnlyCollection<string> wanted, CancellationToken cancellation = default, string? localized = null)
    {
        var found = new Dictionary<string, BntxTexture>();
        void Take(string name, IReadOnlyList<BntxTexture> textures)
        {
            if (!found.ContainsKey(name) && textures.FirstOrDefault(texture => texture.Name == name) is { } texture) found[name] = texture;
        }

        foreach (string name in wanted)
            if (Find(folders, $"{model}_Tex_{name}") is { } own) Take(name, Load(own, cancellation));

        if (localized != null && !wanted.All(found.ContainsKey) && Find(folders, localized) is { } languagePath)
        {
            var textures = Load(languagePath, cancellation);
            foreach (string name in wanted) Take(name, textures);
        }

        if (model.StartsWith("Fld", StringComparison.Ordinal))
            foreach (string archive in FieldPool.Concat(IslandFieldPool))
            {
                if (wanted.All(found.ContainsKey)) break;
                if (Find(folders, archive) is not { } path) continue;
                var textures = Read(path, cancellation);
                foreach (string name in wanted) Take(name, textures);
            }

        if (!wanted.All(found.ContainsKey))
        {
            var shares = ShareFiles(folders).Select(share => (share.Name, Textures: Read(share.Path, cancellation))).ToList();
            foreach (string name in wanted.Where(name => !found.ContainsKey(name)))
            {
                var holders = shares.Where(share => share.Textures.Any(texture => texture.Name == name)).ToList();
                var byPrefix = holders.Where(share => model.StartsWith(share.Name[..^ShareSuffix.Length], StringComparison.Ordinal)).ToList();
                var chosen = byPrefix.Count == 1 ? byPrefix : holders.Count == 1 ? holders : [];
                if (chosen.Count == 1) Take(name, chosen[0].Textures);
            }
        }
        return (found, [.. wanted.Where(name => !found.ContainsKey(name))]);
    }

    private static string? Find(IReadOnlyList<string> folders, string name) =>
        folders.Select(folder => Path.Combine(folder, name + ArchiveSuffix)).FirstOrDefault(File.Exists);

    /// <summary>Every *ShareTexture file in the folders, the first folder's copy of each.</summary>
    private static IEnumerable<(string Name, string Path)> ShareFiles(IReadOnlyList<string> folders)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string folder in folders.Where(Directory.Exists))
            foreach (string path in Directory.EnumerateFiles(folder, $"*{ShareSuffix}{ArchiveSuffix}"))
            {
                string name = Path.GetFileName(path)[..^ArchiveSuffix.Length];
                if (name.Length > ShareSuffix.Length && seen.Add(name)) yield return (name, path);
            }
    }

    private static IReadOnlyList<BntxTexture> Read(string path, CancellationToken cancellation) =>
        cache.GetOrAdd((Path.GetFullPath(path), File.GetLastWriteTimeUtc(path)), key => Load(key.Path, cancellation));

    private static IReadOnlyList<BntxTexture> Load(string path, CancellationToken cancellation)
    {
        using var stream = File.OpenRead(path);
        return BfresFileReader.LoadTextures(stream, cancellation);
    }
}

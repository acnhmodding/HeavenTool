#nullable enable
using HeavenTool.IO.FileFormats.BCSV;
using System.Collections.Concurrent;

namespace HeavenTool.IO.FileFormats.BFRES;

/// <summary>
/// Bcsv/LocalizeNameConvertParam: which archive holds a model's per-language resources. 71006dabc0 takes the row whose BaseName
/// hashes like the model's name (7100639320 looks it up by Murmur3), checks its LocalizeTexture/LocalizeModel/LocalizeAnim flag,
/// reads the system language's column (the Read_JPja… table at 7104816870) and formats "%s_%s" with BaseName and that column's
/// <see cref="LanguageCodes"/> entry, so a language can borrow another's files (StrcTailorA00 in USen loads StrcTailorA00_JPja).
/// </summary>
public sealed class AcnhLocalizedNames
{
    /// <summary>game::System::sLanguageCodes (7104812310), indexed by the language columns' values.</summary>
    public static readonly string[] LanguageCodes = ["JPja", "USen", "USes", "USfr", "USpt", "EUen", "EUes", "EUfr", "EUde", "EUit",
        "EUpt", "EUnl", "EUru", "KRko", "CNzh", "TWzh"];

    /// <summary>The language previews show signs in.</summary>
    public const string PreviewLanguage = "USen";

    private static readonly ConcurrentDictionary<(string Path, DateTime Written), AcnhLocalizedNames> cache = new();

    private readonly BinaryCSV table;

    private AcnhLocalizedNames(BinaryCSV table) => this.table = table;

    public static AcnhLocalizedNames Load(string path) =>
        cache.GetOrAdd((Path.GetFullPath(path), File.GetLastWriteTimeUtc(path)), key =>
        {
            using var stream = File.OpenRead(key.Path);
            return new(new BinaryCSV(stream));
        });

    /// <summary>The archive <paramref name="model"/>'s textures come from in <paramref name="language"/>, or null when they aren't localized.</summary>
    public string? TextureArchive(string model, string language = PreviewLanguage)
    {
        int name = table.ColumnIndex("BaseName string32"), flag = table.ColumnIndex("LocalizeTexture u8");
        int column = table.ColumnIndex($"{language} u8");
        var row = table.Entries.FirstOrDefault(entry => entry[name] is string baseName && baseName == model);
        if (row == null || Convert.ToInt32(row[flag]) == 0) return null;
        // 71006dabc0 gives an index past the table a placeholder code (DAT_7103f7ebb1) that names no shipped file.
        int code = Convert.ToInt32(row[column]);
        return code < LanguageCodes.Length ? $"{model}_{LanguageCodes[code]}" : null;
    }
}

using HeavenTool.Avalonia.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HeavenTool.Avalonia.FileTypes.PBC;

/// <summary>
/// Finds the model a PBC belongs to: <c>IdrMarket01_pbc.Nin_NX_NVN.zs</c> and its <c>IdrMarket01.pbc</c> pair with
/// <c>IdrMarket01.Nin_NX_NVN.zs</c>, looked for next to the opened file first and then in the romfs Model folder from Settings.
/// </summary>
public static class PbcModelLocator
{
    private static readonly string[] PbcSuffixes = ["_pbc.Nin_NX_NVN.zs", "_pbc.zs", "_pbc.szs", "_pbc.sarc", ".pbc"];
    private static readonly string[] ModelSuffixes = [".Nin_NX_NVN.zs", ".bfres.zs", ".bfres", ".sbfres"];

    /// <summary>The model's name for a PBC entry or file name, e.g. <c>IdrMarket01</c>; null when it has no PBC suffix.</summary>
    public static string? ModelName(string name)
    {
        name = Path.GetFileName(name);
        foreach (string suffix in PbcSuffixes)
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && name.Length > suffix.Length)
                return name[..^suffix.Length];
        return null;
    }

    /// <summary>
    /// The model archive for a PBC entry of the file <paramref name="sourceName"/>, opened from <paramref name="sourcePath"/>
    /// when it's a local file. The entry's name comes first, then the file's: BridgeBricks_pbc holds BridgeBricks03.pbc and
    /// more, which all pair with BridgeBricks.
    /// </summary>
    public static string? Find(string entryName, string sourceName, string? sourcePath)
    {
        var names = new List<string>();
        if (ModelName(entryName) is { } fromEntry) names.Add(fromEntry);
        if (ModelName(sourceName) is { } fromFile && !names.Contains(fromFile, StringComparer.OrdinalIgnoreCase)) names.Add(fromFile);
        foreach (string folder in Folders(sourcePath))
            foreach (string name in names)
                foreach (string suffix in ModelSuffixes)
                {
                    string path = Path.Combine(folder, name + suffix);
                    if (File.Exists(path)) return path;
                }
        return null;
    }

    /// <summary>
    /// Where a model's separate textures are looked for (see <see cref="HeavenTool.IO.FileFormats.BFRES.AcnhModelTextures"/>): where
    /// the PBC is, then next to the model, then in the romfs.
    /// </summary>
    public static List<string> TextureFolders(string modelPath, string? sourcePath)
    {
        var folders = Folders(sourcePath).ToList();
        if (Path.GetDirectoryName(Path.GetFullPath(modelPath)) is { } modelFolder) folders.Insert(Math.Min(1, folders.Count), modelFolder);
        return [.. folders.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>A model archive's name without its suffix, e.g. <c>IdrMarket01</c> for <c>IdrMarket01.Nin_NX_NVN.zs</c>.</summary>
    public static string ModelFileName(string modelPath)
    {
        string name = Path.GetFileName(modelPath);
        foreach (string suffix in ModelSuffixes)
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return name[..^suffix.Length];
        return name;
    }

    /// <summary>The romfs LocalizeNameConvertParam table from Settings, or null without a romfs.</summary>
    public static string? LocalizeTable()
    {
        if (GameFiles.FindRomfs(AppSettings.Current.RomfsFolder) is not { } romfs) return null;
        string path = Path.Combine(romfs, "Bcsv", "LocalizeNameConvertParam.bcsv");
        return File.Exists(path) ? path : null;
    }

    /// <summary>The PBC's own folder when it's a local file, then the romfs Model folder from Settings.</summary>
    private static IEnumerable<string> Folders(string? sourcePath)
    {
        if (sourcePath != null && Path.GetDirectoryName(Path.GetFullPath(sourcePath)) is { } sourceFolder) yield return sourceFolder;
        if (GameFiles.FindRomfs(AppSettings.Current.RomfsFolder) is { } romfs) yield return Path.Combine(romfs, "Model");
    }
}

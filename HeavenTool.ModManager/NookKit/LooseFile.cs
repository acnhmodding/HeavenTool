#nullable enable

using AeonSake.NintendoTools.FileFormats.Byml;
using HeavenTool.IO.FileFormats.BYML;

namespace HeavenTool.ModManager.NookKit;

/// <summary>
/// A file that goes in the romfs as is, at <see cref="Path"/> (<c>Param/Gmo/Gmo_wol13.byml</c>). NookKit's pack patches only
/// replace a pack's own files, and the game reads a file its pack doesn't have from the romfs, so a new file goes there.
/// </summary>
public sealed record LooseFile(string Path, byte[] Data)
{
    /// <summary>A BYML file in the version and byte order of <paramref name="like"/>, a BYML file of the game's.</summary>
    public static LooseFile Byml(string path, byte[] like, IBymlNode root)
    {
        var format = AcnhByml.Read(like);
        var file = new BymlFile { BigEndian = format.BigEndian, Version = format.Version, HasPathHeader = format.HasPathHeader, RootNode = root };
        return new(path, AcnhByml.Write(file));
    }

    /// <summary>Writes the file under <paramref name="romfs"/>, returning where.</summary>
    public string Write(string romfs)
    {
        var path = System.IO.Path.Combine(romfs, Path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Data);
        return path;
    }
}

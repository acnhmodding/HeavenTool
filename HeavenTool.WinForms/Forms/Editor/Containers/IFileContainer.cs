using AeonSake.NintendoTools.FileFormats.Sarc;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace HeavenTool.Forms.Editor.Containers;

public interface IFileContainer
{
    string Name { get; }
    bool IsDirectory { get; }

    IEnumerable<IFileContainer> GetChildren();

    Stream OpenRead();
    void Save(Stream data);

    string FullPath { get; }
}


public class PhysicalFileNode(string path) : IFileContainer
{
    public string FullPath { get; } = path;

    public string Name => Path.GetFileName(FullPath);
    public bool IsDirectory => Directory.Exists(FullPath);

    public IEnumerable<IFileContainer> GetChildren()
    {
        if (!IsDirectory) yield break;

        foreach (var dir in Directory.GetDirectories(FullPath))
            yield return new PhysicalFileNode(dir);

        foreach (var file in Directory.GetFiles(FullPath))
            yield return CreateFileNode(file);
    }

    public Stream OpenRead() => File.OpenRead(FullPath);

    public void Save(Stream data)
    {
        using var fs = File.Create(FullPath);
        data.CopyTo(fs);
    }

    private static IFileContainer CreateFileNode(string path)
    {
        var ext = Path.GetExtension(path).ToLower();

        if (ext == ".zip")
        {
            var zip = ZipFile.Open(path, ZipArchiveMode.Read);
            return new ZipFileNode(path, zip);
        }

        //if (ext == ".sarc")
        //{
        //    using var fileReader = File.OpenRead(path);
        //    var sarc = new SarcFileReader().Read(fileReader);
            
        //    //return new SarcFileNode(sarc);
        //}

        return new PhysicalFileNode(path);
    }
}


public class ZipFileNode(string path, ZipArchive zip, string? virtualPath = null, ZipArchiveEntry? entry = null) : IFileContainer
{
    private readonly ZipArchive _zip = zip;
    private readonly ZipArchiveEntry? _entry = entry;
    private readonly string _virtualPath = virtualPath ?? "";

    public string Name
    {
        get
        {
            // Zip Entry Name
            if (_entry != null)
                return Path.GetFileName(_entry.FullName);

            // Zip Directory Name
            if (!string.IsNullOrEmpty(_virtualPath))
                return new DirectoryInfo(_virtualPath).Name;

            // Root (.zip name)
            return Path.GetFileName(path);
        }
    }

    public bool IsDirectory => _entry == null;

    public IEnumerable<IFileContainer> GetChildren()
    {
        if (!IsDirectory)
            yield break;

        var seen = new HashSet<string>();

        foreach (var e in _zip.Entries)
        {
            var (segment, isFile) = GetNextSegment(e.FullName, _virtualPath);

            if (segment == null)
                continue;

            if (isFile)
                yield return new ZipFileNode(path, _zip, _virtualPath, e);
            else if (seen.Add(segment))
                yield return new ZipFileNode(path, _zip, _virtualPath + segment);
            
        }
    }

    private static (string? dir, bool isFile) GetNextSegment(string fullName, string basePath)
    {
        if (!fullName.StartsWith(basePath))
            return (null, false);

        var rest = fullName[basePath.Length..];
        if (string.IsNullOrEmpty(rest))
            return (null, false);

        var slash = rest.IndexOf('/');

        if (slash >= 0)
            return (rest[..(slash + 1)], false);

        return (rest, true);
    }

    public Stream OpenRead() => _entry?.Open() ?? throw new Exception("Cannot open directory");

    public void Save(Stream data)
    {
        if (_entry == null)
            throw new Exception("Cannot save directory");

        using var stream = _entry.Open();
        data.CopyTo(stream);
    }

    public string FullPath => Path.Combine(path, _entry?.FullName ?? _virtualPath);
}
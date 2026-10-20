using HeavenTool.IO.FileFormats.BARS;
using HeavenTool.IO.FileFormats.BWAV;
using System.Text;

namespace HeavenTool.IO.Audio;

public enum SharedStreamSaveChoice { PatchSiblings, EmbedFullAudio, Cancel }

public static class AudioResourceWriter
{
    public static bool SaveArchive(BARSFileReader archive, string path, string? sourcePath = null,
        Func<IReadOnlyList<string>, SharedStreamSaveChoice>? chooseSharedStreamAction = null)
    {
        foreach (var asset in archive.AudioAssets) asset.ValidateForSave();
        var replacements = archive.AudioAssets.Where(a => a.ExternalWave != null)
            .GroupBy(a => a.AudioMetadata!.AssetName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().ExternalWave!, StringComparer.Ordinal);
        var related = FindRelatedArchives(replacements, sourcePath, path);
        try
        {
            var choice = related.Count == 0 ? SharedStreamSaveChoice.PatchSiblings
                : chooseSharedStreamAction?.Invoke(related.Keys.Order(StringComparer.OrdinalIgnoreCase).ToArray())
                    ?? SharedStreamSaveChoice.PatchSiblings;
            if (choice == SharedStreamSaveChoice.Cancel) return false;
            if (choice == SharedStreamSaveChoice.EmbedFullAudio)
            {
                var sharedNames = related.Values.SelectMany(a => a.AudioAssets)
                    .Where(a => a.IsStreaming).Select(a => a.AudioMetadata!.AssetName).ToHashSet(StringComparer.Ordinal);
                using var embedded = new BARSFileReader(new MemoryStream(archive.ToBytes()));
                ReplaceStreams(embedded, replacements, sharedNames);
                Save(path, embedded.ToBytes(), embedded.AudioAssets);
            }
            else if (choice == SharedStreamSaveChoice.PatchSiblings)
            {
                var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                foreach (var (name, sibling) in related)
                {
                    ReplaceStreams(sibling, replacements);
                    files.Add(name, sibling.ToBytes());
                }
                Save(path, archive.ToBytes(), archive.AudioAssets, relatedArchives: files);
            }
            else throw new ArgumentOutOfRangeException(nameof(choice));
            return true;
        }
        finally
        {
            foreach (var sibling in related.Values) sibling.Dispose();
        }
    }

    public static void SaveAsset(AudioAsset asset, string path)
    {
        asset.ValidateForSave();
        Save(path, asset.BinaryWave!.ToBytes(), [asset], standalone: true);
    }

    private static void ReplaceStreams(BARSFileReader archive, IReadOnlyDictionary<string, BinaryWaveFile> replacements,
        HashSet<string>? embedNames = null)
    {
        foreach (var asset in archive.AudioAssets.Where(a => a.IsStreaming))
            if (replacements.TryGetValue(asset.AudioMetadata!.AssetName, out var full))
            {
                asset.ReplaceWave(full, streaming: embedNames?.Contains(asset.AudioMetadata.AssetName) != true);
                asset.ValidateForSave();
            }
    }

    private static Dictionary<string, BARSFileReader> FindRelatedArchives(
        IReadOnlyDictionary<string, BinaryWaveFile> replacements, string? sourcePath, string outputPath)
    {
        var related = new Dictionary<string, BARSFileReader>(StringComparer.OrdinalIgnoreCase);
        if (sourcePath == null || replacements.Count == 0) return related;
        sourcePath = Path.GetFullPath(sourcePath);
        outputPath = Path.GetFullPath(outputPath);
        var names = replacements.Keys.Select(name => Encoding.UTF8.GetBytes(name + '\0')).ToArray();
        foreach (var path in Directory.EnumerateFiles(Path.GetDirectoryName(sourcePath)!, "*.bars"))
        {
            string candidatePath = Path.GetFullPath(path);
            if (string.Equals(candidatePath, sourcePath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidatePath, outputPath, StringComparison.OrdinalIgnoreCase)) continue;
            byte[] data = File.ReadAllBytes(path);
            if (!names.Any(name => data.AsSpan().IndexOf(name) >= 0)) continue;
            var sibling = ReadRelatedArchive(data, path);
            if (sibling.AudioAssets.Any(a => a.IsStreaming && replacements.ContainsKey(a.AudioMetadata!.AssetName)))
                related.Add(Path.GetFileName(path), sibling);
            else sibling.Dispose();
        }
        return related;
    }

    private static BARSFileReader ReadRelatedArchive(byte[] data, string path)
    {
        try { return new BARSFileReader(new MemoryStream(data)); }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Could not read possible shared-stream dependency '{Path.GetFileName(path)}': {ex.Message}", ex);
        }
    }

    private static void Save(string path, byte[] resource, IEnumerable<AudioAsset> assets, bool standalone = false, Dictionary<string, byte[]>? relatedArchives = null)
    {
        path = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(path)!;
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets.Where(a => a.ExternalWave != null))
        {
            string name = standalone ? Path.GetFileNameWithoutExtension(path)
                : asset.AudioMetadata?.AssetName ?? throw new InvalidDataException("Stream metadata is missing.");
            if (standalone && name.EndsWith(".prefetch", StringComparison.OrdinalIgnoreCase))
                name = name[..^".prefetch".Length];
            if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidDataException("The asset name cannot be used as a stream filename.");
            string target = Path.Combine(directory, "Stream", name + ".bwav");
            byte[] data = asset.ExternalWave!.ToBytes();
            if (files.TryGetValue(target, out var existing) && !existing.AsSpan().SequenceEqual(data))
                throw new InvalidDataException($"Entries require different audio for the same external stream: {name}.");
            files[target] = data;
        }
        if (relatedArchives != null)
            foreach (var (name, data) in relatedArchives)
                files.Add(Path.Combine(directory, name), data);
        if (!files.TryAdd(path, resource))
            throw new InvalidDataException("The output path conflicts with a required companion resource.");
        WriteFiles(files);
    }

    private sealed class PendingFile(string target)
    {
        internal string Target { get; } = target;
        internal string Staged { get; } = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        internal string Backup { get; } = target + "." + Guid.NewGuid().ToString("N") + ".bak";
        internal bool BackedUp { get; set; }
        internal bool Installed { get; set; }
    }

    private static void WriteFiles(Dictionary<string, byte[]> files)
    {
        var pending = new List<PendingFile>();
        bool committed = false;
        try
        {
            foreach (var (path, data) in files)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var file = new PendingFile(path);
                pending.Add(file);
                File.WriteAllBytes(file.Staged, data);
            }
            foreach (var file in pending)
            {
                if (File.Exists(file.Target))
                {
                    File.Move(file.Target, file.Backup);
                    file.BackedUp = true;
                }
                File.Move(file.Staged, file.Target);
                file.Installed = true;
            }
            committed = true;
        }
        catch
        {
            foreach (var file in pending.AsEnumerable().Reverse())
            {
                if (file.Installed) File.Delete(file.Target);
                if (file.BackedUp) File.Move(file.Backup, file.Target);
            }
            throw;
        }
        finally
        {
            foreach (var file in pending)
            {
                TryDelete(file.Staged);
                if (committed) TryDelete(file.Backup);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

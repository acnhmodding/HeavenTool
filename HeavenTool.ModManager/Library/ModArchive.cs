#nullable enable

using SharpCompress.Archives;
using SharpCompress.Common;

namespace HeavenTool.ModManager.Library
{
    /// <summary>A folder inside a mod's files that holds a romfs and/or an exefs, ready to install.</summary>
    /// <param name="Name">The variant's path inside the archive, without the atmosphere/contents/title id folders. Empty when the archive holds a single mod at its root.</param>
    public sealed record ModVariant(string Name, string? Romfs, string? Exefs);

    public static class ModArchive
    {
        // Top level folders of the game's romfs, for mods that ship their contents without a romfs folder.
        private static readonly HashSet<string> RomfsFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "AnimationInfo", "AnimationSequence", "BDLI", "Bcsv", "ELink2", "Effect", "Env", "EventFlow", "Font", "Layout",
            "Message", "Model", "Movie", "Pack", "Param", "Rumble", "SLink2", "Shader", "Sound", "Stage", "Swkbd", "System",
            "VTrigDict", "Videogame", "skyline",
        };

        // Folders the Switch and emulators use to place a mod, which say nothing about the variant.
        private static readonly HashSet<string> LayoutFolders = new(StringComparer.OrdinalIgnoreCase) { "atmosphere", "contents", "load", "mods", "sdmc" };

        // Files of a game save: the island (main), each player (personal) and the save tools' metadata.
        private static readonly HashSet<string> SaveFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "main.dat", "mainHeader.dat", "personal.dat", "personalHeader.dat", ".nx_save_meta.bin",
        };

        /// <summary>Whether extracted files include an Animal Crossing save rather than (or as well as) game file changes.</summary>
        public static bool ContainsSaveData(string folder) => ContainsSaveData(Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories));

        /// <summary>Whether an archive's file paths include an Animal Crossing save.</summary>
        public static bool ContainsSaveData(IEnumerable<string> paths) => paths.Any(path => SaveFiles.Contains(Path.GetFileName(path)));

        /// <summary>Whether an archive's file paths include a romfs, an exefs or known romfs folders, the same folders <see cref="FindVariants"/> looks for.</summary>
        public static bool ContainsGameFiles(IEnumerable<string> paths) =>
            paths.Any(path => path.Split('/', '\\').SkipLast(1).Any(folder =>
                folder.Equals("romfs", StringComparison.OrdinalIgnoreCase) || folder.Equals("exefs", StringComparison.OrdinalIgnoreCase) || RomfsFolders.Contains(folder)));

        /// <summary>Extracts an archive (zip, 7z, rar, ...) into <paramref name="destination"/>, reporting the share of bytes written from 0 to 1.</summary>
        public static void Extract(string archivePath, string destination, IProgress<double>? progress = null, CancellationToken cancellation = default)
        {
            var root = Path.GetFullPath(destination);
            Directory.CreateDirectory(root);
            using var archive = ArchiveFactory.OpenArchive(archivePath);
            if (archive.IsEncrypted) throw new InvalidDataException("The archive is password protected.");

            long total = Math.Max(1, archive.TotalUncompressedSize);
            long written = 0;
            if (archive.IsSolid || archive.Type == ArchiveType.SevenZip)
            {
                // Solid archives decompress as one stream, so read their entries in order rather than opening each.
                using var reader = archive.ExtractAllEntries();
                while (reader.MoveToNextEntry())
                    Write(reader.Entry, reader.WriteEntryTo);
            }
            else
            {
                foreach (var entry in archive.Entries)
                    Write(entry, output =>
                    {
                        using var input = entry.OpenEntryStream();
                        input.CopyTo(output);
                    });
            }

            void Write(IEntry entry, Action<Stream> copy)
            {
                cancellation.ThrowIfCancellationRequested();
                if (entry.IsDirectory || entry.Key == null) return;

                var target = Path.GetFullPath(Path.Combine(root, entry.Key));
                // Never write outside the destination, whatever the entry's name says.
                if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return;
                if (PathUtilities.ContainsDirectory(Path.GetRelativePath(root, target), "__MACOSX")) return;

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using (var output = File.Create(target))
                    copy(output);
                written += entry.Size;
                progress?.Report(Math.Min(1, (double)written / total));
            }
        }

        /// <summary>Finds every folder in extracted mod files that holds a romfs or an exefs.</summary>
        public static List<ModVariant> FindVariants(string folder)
        {
            var root = Path.GetFullPath(folder);
            var variants = new List<ModVariant>();
            Visit(root);
            return variants;

            void Visit(string directory)
            {
                var children = Directory.GetDirectories(directory);
                var romfs = children.FirstOrDefault(child => Path.GetFileName(child).Equals("romfs", StringComparison.OrdinalIgnoreCase));
                var exefs = children.FirstOrDefault(child => Path.GetFileName(child).Equals("exefs", StringComparison.OrdinalIgnoreCase));
                if (romfs != null || exefs != null)
                    variants.Add(new(VariantName(root, directory), romfs, exefs));
                else if (children.Any(child => RomfsFolders.Contains(Path.GetFileName(child))))
                    variants.Add(new(VariantName(root, directory), directory, null));
                else
                    foreach (var child in children.Order(StringComparer.OrdinalIgnoreCase))
                        Visit(child);
            }
        }

        private static string VariantName(string root, string directory)
        {
            var relative = Path.GetRelativePath(root, directory);
            if (relative == ".") return "";
            var parts = relative.Split(Path.DirectorySeparatorChar)
                .Where(part => !LayoutFolders.Contains(part) && !IsTitleId(part));
            return string.Join("/", parts);
        }

        private static bool IsTitleId(string name) => name.Length == 16 && name.All(Uri.IsHexDigit);
    }
}

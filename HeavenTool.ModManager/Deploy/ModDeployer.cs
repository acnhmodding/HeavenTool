#nullable enable

using HeavenTool.IO.FileFormats.ResourceSizeTable;
using HeavenTool.ModManager.Library;
using System.Diagnostics;
using System.Text.Json;

namespace HeavenTool.ModManager.Deploy
{
    /// <summary>
    /// Builds the folder the emulator loads from the enabled library mods: their romfs and exefs files in one mod, files that
    /// several mods change merged where the format allows, Skyline's exefs, and a resource size table that fits the deployed files.
    /// </summary>
    public sealed class ModDeployer
    {
        public const string MarkerFile = "heaven-tool-deploy.json";
        private const string ResourceSizeTablePath = "System/Resource/ResourceSizeTable.srsizetable";
        private static readonly string[] MergeableExtensions = [".bcsv", ".msbt", ".sarc"];
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        /// <summary>The emulator's mod folder for the game, which holds one folder per mod.</summary>
        public required string ModFolder { get; init; }

        /// <summary>The name of the deployed mod's folder in <see cref="ModFolder"/>.</summary>
        public required string FolderName { get; init; }

        /// <summary>The game's own romfs, needed to merge files and to update the resource size table. Null when there is none.</summary>
        public string? VanillaRomfs { get; init; }

        /// <summary>A folder holding Skyline's exefs files, deployed over the mods' own copies. Null to leave Skyline out.</summary>
        public string? SkylineExefs { get; init; }

        public string Target => Path.Combine(ModFolder, FolderName);

        /// <summary>
        /// Replaces the deployed folder with the given mods, highest priority first. With no mods, the deployed folder is removed.
        /// The new folder is built beside the old one, so a failed deploy leaves the previous one in place.
        /// </summary>
        public DeployResult Deploy(IReadOnlyList<InstalledMod> mods, IProgress<string>? progress = null, CancellationToken cancellation = default)
        {
            var result = new DeployResult { Target = Target, ModCount = mods.Count };
            sharedNames = [.. mods.GroupBy(mod => mod.Name).Where(group => group.Count() > 1).Select(group => group.Key)];
            if (Directory.Exists(Target) && !IsOwned(Target))
                throw new InvalidOperationException($"{Target} wasn't made by Heaven Tool, so it won't be replaced. Rename or move it, then deploy again.");

            if (mods.Count == 0)
            {
                if (Directory.Exists(Target)) Directory.Delete(Target, true);
                return result;
            }

            Directory.CreateDirectory(ModFolder);
            var staging = Path.Combine(ModFolder, $".{FolderName}.deploying-{Guid.NewGuid():N}");
            try
            {
                var lowestFirst = mods.Reverse().ToList();
                var vanillaTable = VanillaRomfs != null ? Path.Combine(VanillaRomfs, ResourceSizeTablePath) : null;
                var canBuildTable = vanillaTable != null && File.Exists(vanillaTable);

                // A table built from the game's own fits every mod, so the mods' copies are left out.
                DeployFolder("romfs", lowestFirst, staging, result, VanillaRomfs != null, canBuildTable ? [ResourceSizeTablePath] : [], progress, cancellation);
                // Skyline always wins: the mods' own copies of its files are left out, whatever their priority.
                DeployFolder("exefs", lowestFirst, staging, result, false, SkylineExefs != null ? Skyline.Files : [], progress, cancellation);
                if (SkylineExefs != null) DeploySkyline(lowestFirst, staging, result);

                cancellation.ThrowIfCancellationRequested();
                if (!canBuildTable)
                    result.Warnings.Add("The game's romfs folder isn't set in Settings, so files that several mods change weren't merged and the resource size table wasn't updated. The game can crash when a mod makes a file bigger than the table expects.");
                else if (Directory.Exists(Path.Combine(staging, "romfs")))
                    BuildResourceSizeTable(vanillaTable!, staging, result, progress);

                var marker = new DeployMarker
                {
                    DeployedAt = DateTimeOffset.Now,
                    Mods = [.. mods.Select(mod => new DeployedMod(Path.GetFileName(mod.Folder), mod.Name, mod.InstalledAt))],
                    Skyline = result.SkylineInstalled,
                };
                Directory.CreateDirectory(staging);
                File.WriteAllText(Path.Combine(staging, MarkerFile), JsonSerializer.Serialize(marker, JsonOptions));

                cancellation.ThrowIfCancellationRequested();
                progress?.Report("Replacing the deployed mods...");
                Replace(staging, result);
            }
            finally
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
            }

            var others = Directory.EnumerateDirectories(ModFolder)
                .Select(Path.GetFileName)
                .Where(name => name != null && !name.Equals(FolderName, StringComparison.OrdinalIgnoreCase) && !name.StartsWith('.'))
                .Where(name => Directory.Exists(Path.Combine(ModFolder, name!, "romfs")) || Directory.Exists(Path.Combine(ModFolder, name!, "exefs")))
                .ToList();
            if (others.Count > 0)
                result.Warnings.Add($"The emulator also loads mods that Heaven Tool doesn't manage from its mod folder: {string.Join(", ", others)}. Remove them if they clash with the deployed mods.");
            return result;
        }

        /// <summary>Reads what was last deployed to <paramref name="target"/>, or null when nothing was.</summary>
        public static DeployMarker? ReadMarker(string target)
        {
            var path = Path.Combine(target, MarkerFile);
            if (!File.Exists(path)) return null;
            try { return JsonSerializer.Deserialize<DeployMarker>(File.ReadAllText(path), JsonOptions); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                Trace.WriteLine($"Couldn't read {path}: {ex.Message}");
                return null;
            }
        }

        // Only a folder a deploy made, or an empty one, is replaced.
        private static bool IsOwned(string folder) =>
            File.Exists(Path.Combine(folder, MarkerFile)) || !Directory.EnumerateFileSystemEntries(folder).Any();

        // Names that several of the deployed mods share, like two files from one GameBanana mod.
        private HashSet<string> sharedNames = [];

        // The installed variant tells apart mods that share a name.
        private string Label(InstalledMod mod) =>
            sharedNames.Contains(mod.Name) && !string.IsNullOrEmpty(mod.Variant) ? $"{mod.Name} ({mod.Variant})" : mod.Name;

        private void DeployFolder(string kind, List<InstalledMod> lowestFirst, string staging, DeployResult result, bool merge, IReadOnlyCollection<string> skip,
            IProgress<string>? progress, CancellationToken cancellation)
        {
            // Every file, with the mods that have it, lowest priority first.
            var files = new Dictionary<string, List<(InstalledMod Mod, string Path)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in lowestFirst)
            {
                var folder = Path.Combine(mod.Folder, kind);
                if (!Directory.Exists(folder)) continue;
                foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                {
                    var path = Path.GetRelativePath(folder, file).Replace('\\', '/');
                    if (skip.Contains(path, StringComparer.OrdinalIgnoreCase)) continue;
                    if (!files.TryGetValue(path, out var providers)) files[path] = providers = [];
                    providers.Add((mod, file));
                }
            }

            var done = 0;
            foreach (var (path, providers) in files)
            {
                cancellation.ThrowIfCancellationRequested();
                if (++done % 25 == 1 || done == files.Count) progress?.Report($"Deploying {kind} files ({done}/{files.Count})...");
                var output = Path.Combine(staging, kind, path);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                result.FileCount++;

                var winner = providers[^1];
                if (providers.Count == 1)
                {
                    File.Copy(winner.Path, output);
                    continue;
                }

                var contents = providers.Select(provider => File.ReadAllBytes(provider.Path)).ToList();
                var overwritten = providers.Where((_, i) => !contents[i].AsSpan().SequenceEqual(contents[^1])).Select(provider => Label(provider.Mod)).ToList();
                if (overwritten.Count == 0)
                {
                    File.WriteAllBytes(output, contents[^1]);
                    continue;
                }

                if (merge)
                {
                    progress?.Report($"Merging {path}...");
                    if (TryMerge(path, contents) is { } merged)
                    {
                        File.WriteAllBytes(output, merged);
                        result.MergedFiles.Add($"{kind}/{path}");
                        continue;
                    }
                }

                File.WriteAllBytes(output, contents[^1]);
                result.Conflicts.Add(new FileConflict($"{kind}/{path}", Label(winner.Mod), overwritten));
            }
        }

        /// <summary>Applies each mod's changes, lowest priority first, to the game's copy. Null when the file can't be merged.</summary>
        private byte[]? TryMerge(string path, List<byte[]> contents)
        {
            var name = path.EndsWith(".zs", StringComparison.OrdinalIgnoreCase) ? path[..^3] : path;
            if (!MergeableExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase)) return null;
            var vanilla = Path.Combine(VanillaRomfs!, path);
            if (!File.Exists(vanilla)) return null;

            try
            {
                var merged = FileMerger.TryLoad(File.ReadAllBytes(vanilla), path);
                if (merged == null) return null;
                var changes = contents.Select(bytes => FileMerger.TryLoad(bytes, path)).ToList();
                if (!changes.All(merged.CanMerge)) return null;
                foreach (var change in changes) merged.DoDiff(change);
                return FileMerger.SaveModFile(merged);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Trace.WriteLine($"Couldn't merge {path}: {ex}");
                return null;
            }
        }

        private void DeploySkyline(List<InstalledMod> lowestFirst, string staging, DeployResult result)
        {
            var exefs = Path.Combine(staging, "exefs");
            Directory.CreateDirectory(exefs);
            foreach (var file in Skyline.Files)
            {
                var source = Path.Combine(SkylineExefs!, file);
                var skyline = File.ReadAllBytes(source);
                var replaced = lowestFirst
                    .Where(mod => ModCopies(mod, file).Any(copy => !File.ReadAllBytes(copy).AsSpan().SequenceEqual(skyline)))
                    .Select(Label).Reverse().ToList();
                if (replaced.Count > 0)
                    result.Warnings.Add($"Skyline's exefs/{file} replaces the one from {string.Join(", ", replaced)}. Turn off \"Install Skyline\" in Settings to keep the mod's copy.");
                File.Copy(source, Path.Combine(exefs, file));
                result.FileCount++;
            }
            result.SkylineInstalled = true;
        }

        // A mod's own copies of an exefs file, in any letter case.
        private static IEnumerable<string> ModCopies(InstalledMod mod, string file)
        {
            var exefs = Path.Combine(mod.Folder, "exefs");
            return Directory.Exists(exefs)
                ? Directory.EnumerateFiles(exefs).Where(path => Path.GetFileName(path).Equals(file, StringComparison.OrdinalIgnoreCase))
                : [];
        }

        private static void BuildResourceSizeTable(string vanillaTable, string staging, DeployResult result, IProgress<string>? progress)
        {
            progress?.Report("Updating the resource size table...");
            var romfs = Path.Combine(staging, "romfs");
            try
            {
                using var table = new ResourceSizeTable(File.ReadAllBytes(vanillaTable));
                ResourceSizeTablePatcher.CalculatePatch(romfs).ApplyTo(table);
                var output = Path.Combine(romfs, ResourceSizeTablePath);
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.WriteAllBytes(output, table.Save() ?? throw new InvalidOperationException("the table couldn't be saved"));
                result.FileCount++;
                result.ResourceSizeTableUpdated = true;
            }
            catch (Exception ex) when (ex is not (IOException or UnauthorizedAccessException))
            {
                result.Warnings.Add($"Couldn't update the resource size table: {ex.Message}. The game can crash when a mod makes a file bigger than the table expects.");
            }
        }

        private void Replace(string staging, DeployResult result)
        {
            if (!Directory.Exists(Target))
            {
                Directory.Move(staging, Target);
                return;
            }

            // Keep the previous deploy until the new one is in place.
            var previous = Path.Combine(ModFolder, $".{FolderName}.previous-{Guid.NewGuid():N}");
            Directory.Move(Target, previous);
            try
            {
                Directory.Move(staging, Target);
            }
            catch
            {
                Directory.Move(previous, Target);
                throw;
            }

            try { Directory.Delete(previous, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.Warnings.Add($"Couldn't delete the previous deploy at {previous}: {ex.Message}. Delete it yourself, or the emulator loads it too.");
            }
        }
    }

    /// <summary>What a deploy wrote, kept in the deployed folder.</summary>
    public sealed class DeployMarker
    {
        public DateTimeOffset DeployedAt { get; set; }

        /// <summary>The deployed mods, highest priority first.</summary>
        public List<DeployedMod> Mods { get; set; } = [];

        public bool Skyline { get; set; }

        /// <summary>Whether deploying <paramref name="mods"/> now would deploy the same thing.</summary>
        public bool Matches(IReadOnlyList<InstalledMod> mods, bool skyline) =>
            Skyline == skyline && Mods.Count == mods.Count && Mods.Zip(mods).All(pair =>
                pair.First.Folder.Equals(Path.GetFileName(pair.Second.Folder), StringComparison.OrdinalIgnoreCase) &&
                pair.First.InstalledAt == pair.Second.InstalledAt);
    }

    /// <param name="Folder">The mod's folder name in the library.</param>
    /// <param name="InstalledAt">When the deployed copy was installed, so a reinstall counts as a change.</param>
    public sealed record DeployedMod(string Folder, string Name, DateTimeOffset InstalledAt);
}

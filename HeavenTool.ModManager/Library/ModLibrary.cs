#nullable enable

using System.Diagnostics;
using System.Text.Json;

namespace HeavenTool.ModManager.Library
{
    /// <summary>
    /// Installed mods, each unpacked in its own folder: a romfs and/or exefs folder plus <see cref="MetadataFile"/>.
    /// </summary>
    public sealed class ModLibrary(string folder)
    {
        public const string MetadataFile = "heaven-tool-mod.json";
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public string Folder { get; } = folder;

        /// <summary>Reads every installed mod. Folders without metadata, or with metadata that can't be read, are left out.</summary>
        public List<InstalledMod> Load()
        {
            var mods = new List<InstalledMod>();
            if (!Directory.Exists(Folder)) return mods;
            foreach (var directory in Directory.EnumerateDirectories(Folder))
            {
                var metadata = Path.Combine(directory, MetadataFile);
                if (!File.Exists(metadata)) continue;
                try
                {
                    var mod = JsonSerializer.Deserialize<InstalledMod>(File.ReadAllText(metadata), JsonOptions);
                    if (mod == null) continue;
                    mod.Folder = directory;
                    mods.Add(mod);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    Trace.WriteLine($"Skipping {directory}: {ex.Message}");
                }
            }
            return [.. mods.OrderBy(mod => mod.Name, StringComparer.CurrentCultureIgnoreCase)];
        }

        /// <summary>
        /// Copies a variant's romfs and exefs into the library. Installing the same GameBanana file again, or the same local
        /// archive, replaces the earlier copy. Several variants are merged into one mod, later ones winning when they share a file,
        /// for archives that split a mod into parts.
        /// </summary>
        public InstalledMod Install(InstalledMod mod, IReadOnlyList<ModVariant> variants)
        {
            Directory.CreateDirectory(Folder);
            var existing = Load().FirstOrDefault(installed => IsSameSource(installed, mod));

            // Copy into a hidden folder first so a failed install never leaves a half copied mod behind.
            var staging = Path.Combine(Folder, ".installing-" + Guid.NewGuid().ToString("N"));
            try
            {
                foreach (var variant in variants)
                {
                    if (variant.Romfs != null) CopyDirectory(variant.Romfs, Path.Combine(staging, "romfs"));
                    if (variant.Exefs != null) CopyDirectory(variant.Exefs, Path.Combine(staging, "exefs"));
                }
                mod.Variant = string.Join(", ", variants.Select(variant => variant.Name).Where(name => name.Length > 0));
                mod.InstalledAt = DateTimeOffset.Now;
                Directory.CreateDirectory(staging);
                File.WriteAllText(Path.Combine(staging, MetadataFile), JsonSerializer.Serialize(mod, JsonOptions));

                if (existing == null)
                {
                    mod.Folder = UniqueFolder(mod.Name);
                    Directory.Move(staging, mod.Folder);
                    return mod;
                }

                // Keep the earlier copy until the new one is in place.
                var previous = Path.Combine(Folder, ".replacing-" + Guid.NewGuid().ToString("N"));
                Directory.Move(existing.Folder, previous);
                mod.Folder = existing.Folder;
                try
                {
                    Directory.Move(staging, mod.Folder);
                }
                catch
                {
                    Directory.Move(previous, existing.Folder);
                    throw;
                }
                Directory.Delete(previous, true);
                return mod;
            }
            finally
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
            }
        }

        /// <summary>Deletes an installed mod's folder. Only folders the library made are deleted.</summary>
        public static void Remove(InstalledMod mod)
        {
            if (!File.Exists(Path.Combine(mod.Folder, MetadataFile)))
                throw new InvalidOperationException($"{mod.Folder} isn't a library mod folder.");
            Directory.Delete(mod.Folder, true);
        }

        private static bool IsSameSource(InstalledMod a, InstalledMod b) =>
            a.ModId != null || b.ModId != null
                ? a.ModId == b.ModId && a.FileId == b.FileId
                : string.Equals(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase);

        private string UniqueFolder(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safe = new string([.. name.Select(c => invalid.Contains(c) ? '_' : c)]).Trim().TrimEnd('.');
            if (safe.Length == 0 || safe.StartsWith('.')) safe = "Mod" + safe;
            if (safe.Length > 80) safe = safe[..80].TrimEnd();

            var path = Path.Combine(Folder, safe);
            for (int i = 2; Directory.Exists(path); i++)
                path = Path.Combine(Folder, $"{safe} ({i})");
            return path;
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(destination, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, true);
            }
        }
    }
}

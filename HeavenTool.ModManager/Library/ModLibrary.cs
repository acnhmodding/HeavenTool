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
        public const string LoadOrderFile = "load-order.json";
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public string Folder { get; } = folder;

        /// <summary>
        /// Reads every installed mod, highest priority first, with <see cref="InstalledMod.Enabled"/> set from the load order.
        /// Mods the load order doesn't know yet go on top, newest first. Folders without metadata, or with metadata that
        /// can't be read, are left out.
        /// </summary>
        public List<InstalledMod> Load()
        {
            var mods = LoadUnordered();
            var order = ReadLoadOrder();
            var position = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in order.Order) position.TryAdd(name, position.Count);
            var disabled = new HashSet<string>(order.Disabled, StringComparer.OrdinalIgnoreCase);

            foreach (var mod in mods) mod.Enabled = !disabled.Contains(Path.GetFileName(mod.Folder));
            return [.. mods
                .OrderBy(mod => position.TryGetValue(Path.GetFileName(mod.Folder), out var index) ? index : -1)
                .ThenByDescending(mod => mod.InstalledAt)];
        }

        /// <summary>
        /// Cleans up after installs that were stopped or couldn't finish: a mod left hidden by a reinstall is put back, and the
        /// copies and half finished installs left beside the mods are deleted. Call it while no install runs.
        /// </summary>
        public void Recover()
        {
            foreach (var folder in FolderSwap.Recover(Folder)) Trace.WriteLine($"Put back {folder}, which a reinstall had left hidden");
            if (!Directory.Exists(Folder)) return;
            foreach (var staging in Directory.EnumerateDirectories(Folder, ".installing-*").ToList())
            {
                try { Directory.Delete(staging, true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Trace.WriteLine($"Couldn't delete {staging}: {ex.Message}"); }
            }
        }

        /// <summary>Saves the mods' order, highest priority first, and which of them are enabled.</summary>
        public void SaveLoadOrder(IEnumerable<InstalledMod> mods)
        {
            var order = new LoadOrder();
            foreach (var mod in mods)
            {
                var name = Path.GetFileName(mod.Folder);
                order.Order.Add(name);
                if (!mod.Enabled) order.Disabled.Add(name);
            }
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, LoadOrderFile);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(order, JsonOptions));
            File.Move(temporary, path, true);
        }

        private LoadOrder ReadLoadOrder()
        {
            var path = Path.Combine(Folder, LoadOrderFile);
            if (!File.Exists(path)) return new LoadOrder();
            try { return JsonSerializer.Deserialize<LoadOrder>(File.ReadAllText(path), JsonOptions) ?? new LoadOrder(); }
            catch (JsonException ex)
            {
                Trace.WriteLine($"Ignoring the load order in {path}: {ex.Message}");
                return new LoadOrder();
            }
        }

        private List<InstalledMod> LoadUnordered()
        {
            var mods = new List<InstalledMod>();
            if (!Directory.Exists(Folder)) return mods;
            // Dot folders are installs in progress, or copies a reinstall couldn't delete.
            foreach (var directory in Directory.EnumerateDirectories(Folder).Where(directory => !Path.GetFileName(directory).StartsWith('.')))
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
            return mods;
        }

        /// <summary>
        /// Copies a variant's romfs and exefs into the library. Installing the same GameBanana file again, or the same local
        /// archive, replaces the earlier copy. Several variants are merged into one mod, later ones winning when they share a file,
        /// for archives that split a mod into parts. An update passes the mod it <paramref name="replaces"/>, which keeps its folder,
        /// and so its place in the load order.
        /// </summary>
        public InstalledMod Install(InstalledMod mod, IReadOnlyList<ModVariant> variants, InstalledMod? replaces = null)
        {
            Directory.CreateDirectory(Folder);
            var existing = replaces ?? LoadUnordered().FirstOrDefault(installed => IsSameSource(installed, mod));

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

                mod.Folder = existing?.Folder ?? UniqueFolder(mod.Name);
                if (FolderSwap.Replace(staging, mod.Folder) is { } previous)
                    Trace.WriteLine($"Couldn't delete the replaced copy at {previous}");
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

        private sealed class LoadOrder
        {
            /// <summary>Mod folder names, highest priority first.</summary>
            public List<string> Order { get; set; } = [];
            public List<string> Disabled { get; set; } = [];
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

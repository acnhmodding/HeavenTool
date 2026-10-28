#nullable enable

namespace HeavenTool.ModManager
{
    internal static class FolderSwap
    {
        /// <summary>
        /// Moves <paramref name="staging"/> to <paramref name="target"/>. An existing target is kept aside until the new one is
        /// in place, and put back when the move fails. Returns where the old copy was left when it couldn't be deleted.
        /// </summary>
        public static string? Replace(string staging, string target)
        {
            if (!Directory.Exists(target))
            {
                Directory.Move(staging, target);
                return null;
            }

            // A dot folder beside the target, which the library and the deploy's leftover check know.
            var previous = Path.Combine(Path.GetDirectoryName(target)!, $".{Path.GetFileName(target)}.previous-{Guid.NewGuid():N}");
            Directory.Move(target, previous);
            try
            {
                Directory.Move(staging, target);
            }
            catch (Exception ex)
            {
                try { Directory.Move(previous, target); }
                catch (Exception restore) when (restore is IOException or UnauthorizedAccessException)
                {
                    throw new IOException($"{ex.Message} The earlier copy couldn't be put back either, and is kept at {previous}.", ex);
                }
                throw;
            }

            try
            {
                Directory.Delete(previous, true);
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return previous;
            }
        }

        /// <summary>Whether a folder name is a copy <see cref="Replace"/> set aside for the folder named <paramref name="target"/>.</summary>
        public static bool IsPreviousCopy(string name, string target) =>
            name.StartsWith($".{target}.previous-", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Tidies the copies that swaps in <paramref name="parent"/> set aside and couldn't delete, or left when they were stopped:
        /// a copy whose folder is missing is put back, and the rest are deleted. Returns the folders that could be put back.
        /// </summary>
        public static List<string> Recover(string parent)
        {
            var restored = new List<string>();
            if (!Directory.Exists(parent)) return restored;
            foreach (var folder in Directory.EnumerateDirectories(parent, ".*.previous-*").ToList())
            {
                var name = Path.GetFileName(folder);
                var end = name.LastIndexOf(".previous-", StringComparison.Ordinal);
                if (end <= 1) continue;
                var target = Path.Combine(parent, name[1..end]);
                try
                {
                    if (Directory.Exists(target)) Directory.Delete(folder, true);
                    else
                    {
                        Directory.Move(folder, target);
                        restored.Add(target);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            return restored;
        }
    }
}

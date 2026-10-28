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
            catch
            {
                Directory.Move(previous, target);
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
    }
}

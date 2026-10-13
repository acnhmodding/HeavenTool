namespace HeavenTool.ModManager
{
    internal static class PathUtilities
    {
        public static bool ArePathsEqual(string path1, string path2)
        {
            var normalizedPath1 = NormalizePath(path1);
            var normalizedPath2 = NormalizePath(path2);

            return string.Equals(normalizedPath1, normalizedPath2, StringComparison.OrdinalIgnoreCase);
        }

        public static bool ContainsDirectory(string path, string directoryName)
        {
            char[] separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

            var directories = path.Split(separators, StringSplitOptions.RemoveEmptyEntries);

            return directories.Any(part => string.Equals(part, directoryName, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetRelativePathFromTarget(string fullPath, string targetDirectory)
        {
            char[] separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];
            var parts = fullPath.Split(separators, StringSplitOptions.RemoveEmptyEntries);

            var targetIndex = Array.FindIndex(parts, part => string.Equals(part, targetDirectory, StringComparison.OrdinalIgnoreCase));

            if (targetIndex == -1)
                return null;

            var relativePath = Path.Combine([.. parts.Skip(targetIndex)]);
            return relativePath;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            normalized = normalized.TrimEnd(Path.DirectorySeparatorChar);

            return normalized;
        }
    }
}
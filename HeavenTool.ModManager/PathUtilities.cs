namespace HeavenTool.ModManager
{
    internal static class PathUtilities
    {
        public static bool ArePathsEqual(string path1, string path2)
        {
            string normalizedPath1 = NormalizePath(path1);
            string normalizedPath2 = NormalizePath(path2);

            return string.Equals(normalizedPath1, normalizedPath2, StringComparison.OrdinalIgnoreCase);
        }

        public static bool ContainsDirectory(string path, string directoryName)
        {
            char[] separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

            string[] directories = path.Split(separators, StringSplitOptions.RemoveEmptyEntries);

            return directories.Any(part => string.Equals(part, directoryName, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetRelativePathFromTarget(string fullPath, string targetDirectory)
        {
            char[] separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];
            string[] parts = fullPath.Split(separators, StringSplitOptions.RemoveEmptyEntries);

            int targetIndex = Array.FindIndex(parts, part => string.Equals(part, targetDirectory, StringComparison.OrdinalIgnoreCase));

            if (targetIndex == -1)
                return null;

            string relativePath = Path.Combine([.. parts.Skip(targetIndex)]);
            return relativePath;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            string normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            normalized = normalized.TrimEnd(Path.DirectorySeparatorChar);

            return normalized;
        }
    }
}
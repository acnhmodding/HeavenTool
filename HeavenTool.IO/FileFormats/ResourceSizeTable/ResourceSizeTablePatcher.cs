namespace HeavenTool.IO.FileFormats.ResourceSizeTable;

public static class ResourceSizeTablePatcher
{
    // This phase only reads files. The caller applies the patch on the table's owning thread.
    public static Patch CalculatePatch(string romfsPath, IProgress<(int Index, int Total, string FileName)>? progress = null)
    {
        var files = Directory.GetFiles(romfsPath, "*", SearchOption.AllDirectories);
        var updates = new List<(string Path, long FileSize)>();
        var skipped = 0;

        for (var i = 0; i < files.Length; i++)
        {
            var path = Path.GetRelativePath(romfsPath, files[i]).Replace('\\', '/');
            progress?.Report((i + 1, files.Length, path));

            if (path.StartsWith("skyline") ||
                path is "System/Resource/ResourceSizeTable.srsizetable" or "System/Resource/ResourceSizeTable.rsizetable" ||
                (path.EndsWith(".byml") && path != "EventFlow/Info/EventFlowInfoProduct.byml"))
            {
                skipped++;
                continue;
            }

            if (path.EndsWith(".zs"))
                path = path[..^3];

            updates.Add((path, ResourceSizeTable.GetFileSize(files[i], path)));
        }

        return new Patch(updates, skipped);
    }

    public sealed class Patch
    {
        private readonly List<(string Path, long FileSize)> updates;

        internal Patch(List<(string Path, long FileSize)> updates, int skippedFiles)
        {
            this.updates = updates;
            SkippedFiles = skippedFiles;
        }

        public int SkippedFiles { get; }

        public (int Changed, int Added, int Removed) ApplyTo(ResourceSizeTable table)
        {
            var changed = 0;
            var added = 0;
            var removed = 0;

            foreach (var (path, size) in updates)
            {
                if (size is < 0 or > uint.MaxValue)
                {
                    if (table.Dictionary.Remove(path))
                        removed++;
                }
                else if (table.Dictionary.TryGetValue(path, out var entry))
                {
                    if (entry.FileSize != size)
                    {
                        entry.FileSize = (uint)size;
                        changed++;
                    }
                }
                else
                {
                    table.AddEntry(new ResourceSizeTable.ResourceTableEntry(path, (uint)size, 0, false));
                    added++;
                }
            }

            return (changed, added, removed);
        }
    }
}
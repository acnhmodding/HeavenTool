#nullable enable

namespace HeavenTool.ModManager.Deploy
{
    /// <summary>What a deploy wrote, and what the user should know about it.</summary>
    public sealed class DeployResult
    {
        /// <summary>The deployed folder, or the folder that was removed when no mod was enabled.</summary>
        public string Target { get; init; } = "";
        public int ModCount { get; set; }
        public int FileCount { get; set; }

        /// <summary>Romfs paths that more than one mod changed and were merged.</summary>
        public List<string> MergedFiles { get; } = [];

        /// <summary>Files more than one mod changed that couldn't be merged, so the highest priority mod's copy was used.</summary>
        public List<FileConflict> Conflicts { get; } = [];

        public List<string> Warnings { get; } = [];
        public bool SkylineInstalled { get; set; }
        public bool ResourceSizeTableUpdated { get; set; }
    }

    /// <param name="Path">The file's path in the deployed folder, such as romfs/Bcsv/ItemParam.bcsv.</param>
    /// <param name="Winner">The mod whose copy was deployed.</param>
    /// <param name="Overwritten">The mods whose different copies were left out.</param>
    public sealed record FileConflict(string Path, string Winner, IReadOnlyList<string> Overwritten);
}

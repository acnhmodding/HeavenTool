#nullable enable

using System.Text.Json.Serialization;

namespace HeavenTool.ModManager.Library
{
    /// <summary>What the library knows about an installed mod, kept as JSON beside its files.</summary>
    public sealed class InstalledMod
    {
        public string Name { get; set; } = "";
        public string? Author { get; set; }
        public string? Version { get; set; }

        /// <summary>The GameBanana submission it came from, or null for a mod installed from a local file.</summary>
        public int? ModId { get; set; }

        /// <summary>The GameBanana file that was downloaded. A new upload gets a new id.</summary>
        public int? FileId { get; set; }

        /// <summary>The archive's file name.</summary>
        public string? FileName { get; set; }

        /// <summary>The folders inside the archive that were installed, joined with ", ". Empty when the archive held a single mod.</summary>
        public string Variant { get; set; } = "";

        public string? ProfileUrl { get; set; }
        public string? ImageUrl { get; set; }
        public DateTimeOffset InstalledAt { get; set; }

        /// <summary>The mod's folder in the library.</summary>
        [JsonIgnore]
        public string Folder { get; set; } = "";
    }
}

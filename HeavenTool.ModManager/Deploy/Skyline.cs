#nullable enable

using System.IO.Compression;

namespace HeavenTool.ModManager.Deploy
{
    /// <summary>
    /// The Skyline plugin loader for Animal Crossing, which romfs/skyline/plugins mods need. Downloaded once and kept in a cache folder.
    /// </summary>
    public static class Skyline
    {
        public const string DownloadUrl = "https://github.com/acnhmodding/skyline-acnh/releases/download/beta/skyline.zip";

        /// <summary>The exefs files Skyline replaces.</summary>
        public static readonly string[] Files = ["main.npdm", "subsdk9"];

        /// <summary>Whether <paramref name="exefs"/> holds every Skyline file.</summary>
        public static bool IsComplete(string exefs) => Files.All(file => File.Exists(Path.Combine(exefs, file)));

        /// <summary>
        /// Returns the cached exefs folder, downloading Skyline into <paramref name="cacheFolder"/> first if it isn't there yet.
        /// </summary>
        public static async Task<string> GetExefsAsync(string cacheFolder, HttpClient client, CancellationToken cancellation = default)
        {
            var exefs = Path.Combine(cacheFolder, "exefs");
            if (IsComplete(exefs)) return exefs;

            var bytes = await client.GetByteArrayAsync(DownloadUrl, cancellation);
            var staging = Path.Combine(cacheFolder, ".downloading-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(staging);
                using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
                {
                    foreach (var file in Files)
                    {
                        var entry = zip.Entries.FirstOrDefault(entry =>
                            entry.FullName.Replace('\\', '/').EndsWith("exefs/" + file, StringComparison.OrdinalIgnoreCase))
                            ?? throw new InvalidDataException($"The Skyline download has no exefs/{file}.");
                        entry.ExtractToFile(Path.Combine(staging, file));
                    }
                }

                if (Directory.Exists(exefs)) Directory.Delete(exefs, true);
                Directory.Move(staging, exefs);
                return exefs;
            }
            finally
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
            }
        }
    }
}

using HeavenTool.IO;
using HeavenTool.IO.FileFormats.ResourceSizeTable;
using HeavenTool.ModManager.FileTypes;
using NintendoTools.Compression.Zstd;
using NintendoTools.FileFormats.Bcsv;
using NintendoTools.FileFormats.Msbt;
using NintendoTools.FileFormats.Sarc;
using System.Data;
using System.IO;
using System.IO.Compression;

namespace HeavenTool.ModManager
{
    public class FileMerger(string modsFolder, string outputDirectory)
    {

        #region Decompressors
        public static readonly ZstdCompressor ZstdCompressor = new()
        {
            CompressionLevel = 19
        };
        public static readonly ZstdDecompressor ZstdDecompressor = new();
        #endregion

        public string ModsFolder { get; set; } = modsFolder;
        public string OutputDirectory { get; set; } = outputDirectory;

        public List<string> ModsUsedPaths = [];

        public delegate void FileChangedArgs(string modName, string file);

        public event FileChangedArgs FileChanged;

        /// <summary>
        /// Uncompress (if needed) and assign a stream to a ModFile.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="fileName">The path for the file, in RomFs
        /// <br/>Example: Message/TalkNNpc_EUen.sarc.zs</param>
        /// <returns>
        /// <para>• A <see cref="ModFile"/> based on the file type.</para>
        /// <para>• <see cref="GenericFile"/> if file type is <b>not</b> supported.</para>
        /// </returns>
        public ModFile LoadModFile(byte[] bytes, string fileName)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            ArgumentException.ThrowIfNullOrEmpty(fileName);

            MemoryStream stream = new(bytes);

            var compression = Compression.None;
            if (ZstdDecompressor.CanDecompressStatic(stream))
            {
                compression = Compression.Zstd;

                var decompressedStream = new MemoryStream();
                ZstdDecompressor.Decompress(stream, decompressedStream);

                stream = decompressedStream;
            }

            ModFile modFile;
            if (MsbtFileParser.CanParseStatic(stream))
                modFile = new MSBT(stream, fileName);
            else if (!fileName.StartsWith("romfs\\Model\\") && SarcFileParser.CanParseStatic(stream))
                modFile = new SARC(stream, fileName);
            else if (fileName.EndsWith(".bcsv") && BcsvFileParser.CanParseStatic(stream))
                modFile = new BCSV(stream, fileName);
            else
            {
                ConsoleUtilities.WriteLine($"Moving unsupported file {fileName} to Output", ConsoleColor.Yellow);
                var outputPath = Path.Combine(OutputDirectory, fileName);

                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, bytes);

                return null;
                // TODO: save it to output folder already to save memory
                // modFile = new GenericFile(stream, fileName);
            }

            if (modFile != null) 
                modFile.Compression = compression;

            return modFile;
        }


        public byte[] GetVanillaFile(string fileName)
        {
            ArgumentException.ThrowIfNullOrEmpty(fileName);

            if (!File.Exists("vanilla.zip"))
                throw new FileNotFoundException("There is no vanilla.zip in the root of your installation!");

            using var zipFile = ZipFile.OpenRead("vanilla.zip");

            bool predicate(ZipArchiveEntry entry) => PathUtilities.ArePathsEqual(entry.FullName, fileName);

            if (zipFile.Entries.Any(predicate))
            {
                var entry = zipFile.Entries.Single(predicate);
                using var stream = entry.Open();
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);

                return memoryStream.ToArray();
            }

            return null;
        }

        public ModFile LoadVanillaContent(string fileName) {
            ArgumentException.ThrowIfNullOrEmpty(fileName);

            var fileBytes = GetVanillaFile(fileName);

            if (fileBytes == null) 
                return null;

            return LoadModFile(fileBytes, fileName);
        }

        public void SearchModsContentPaths()
        {
            var modsFolder = Directory.CreateDirectory(ModsFolder);

            foreach (var item in modsFolder.GetFiles("*.zip", SearchOption.TopDirectoryOnly))
            {
                if (item.Extension != ".zip") continue;
 
                using var zipFile = ZipFile.OpenRead(item.FullName);

                var romFsItems = zipFile.Entries.Where(x => PathUtilities.ContainsDirectory(x.FullName, "romfs") && !Path.EndsInDirectorySeparator(x.FullName));

                if (!romFsItems.Any())
                    ConsoleUtilities.WriteLine($"> {item.Name} does not contain a romfs folder", ConsoleColor.Red);
                else
                    ConsoleUtilities.WriteLine($"> {item.Name} found!", ConsoleColor.Green);

                foreach (var romFile in romFsItems)
                {
                    var path = PathUtilities.GetRelativePathFromTarget(romFile.FullName, "romfs");

                    if (path == null || path.EndsWith(".srsizetable")) continue;
                    
                    if (!ModsUsedPaths.Contains(path))
                        ModsUsedPaths.Add(path);
                       
                }
            }
        }

        //public void PatchAndExport()
        //{
        //    Directory.CreateDirectory(OutputDirectory);

        //    foreach (var file in ModsUsedPaths)
        //    {
        //        ModFile baseFile = LoadVanillaContent(file);

        //        foreach (var modZip in Directory.GetFiles(ModsFolder, "*.zip", SearchOption.TopDirectoryOnly))
        //        {
        //            var zipName = Path.GetFileName(modZip);

        //            using var zipFile = ZipFile.OpenRead(modZip);

        //            bool predicate(ZipArchiveEntry entry) {
        //                var path = PathUtilities.GetRelativePathFromTarget(entry.FullName, "romfs");
        //                if (path == null) return false;

        //                return PathUtilities.ArePathsEqual(path, file);
        //            }

        //            if (zipFile.Entries.Any(predicate))
        //            {
        //                FileChanged?.Invoke(zipName, file);

        //                var entry = zipFile.Entries.Single(predicate);
        //                using var stream = entry.Open();

        //                using var memoryStream = new MemoryStream();
        //                stream.CopyTo(memoryStream);

        //                var loadedFile = LoadModFile(memoryStream.ToArray(), file);
        //                if (baseFile != null)                           
        //                    baseFile.DoDiff(loadedFile);

        //                else
        //                    baseFile = loadedFile; // it's a new file, use as base file instead of a vanilla one
        //            }

        //        }

        //        if (baseFile != null) {

        //            var fileBytes = baseFile.SaveFile();

        //            switch (baseFile.Compression)
        //            {
        //                case Compression.Zstd:
        //                    ConsoleUtilities.WriteLine($"Compressing {baseFile.Name} to ZSTD", ConsoleColor.Blue);
        //                    var compressionStream = new MemoryStream();
        //                    ZstdCompressor.Compress(new MemoryStream(fileBytes), compressionStream);
        //                    fileBytes = compressionStream.ToArray();
        //                    break;
        //            }

        //            var outputPath = Path.Combine(OutputDirectory, file);

        //            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        //            File.WriteAllBytes(outputPath, fileBytes);

        //        }
        //    }
        //}
        private readonly Dictionary<string, ModFile> _patchedFiles = [];

        public void Patch()
        {
            _patchedFiles.Clear(); // Clear any previous state

            foreach (var filePath in ModsUsedPaths)
            {
                ModFile baseFile = LoadVanillaContent(filePath);

                foreach (var modZip in Directory.GetFiles(ModsFolder, "*.zip", SearchOption.TopDirectoryOnly))
                {
                    var zipName = Path.GetFileName(modZip);

                    using var zipFile = ZipFile.OpenRead(modZip);

                    bool predicate(ZipArchiveEntry entry)
                    {
                        var path = PathUtilities.GetRelativePathFromTarget(entry.FullName, "romfs");
                        return path != null && PathUtilities.ArePathsEqual(path, filePath);
                    }

                    if (zipFile.Entries.Any(predicate))
                    {
                        FileChanged?.Invoke(zipName, filePath);

                        var entry = zipFile.Entries.Single(predicate);
                        using var stream = entry.Open();
                        using var memoryStream = new MemoryStream();
                        stream.CopyTo(memoryStream);

                        var loadedFile = LoadModFile(memoryStream.ToArray(), filePath);
                        if (baseFile != null)
                            baseFile.DoDiff(loadedFile);
                        else
                            baseFile = loadedFile; // use as base if no vanilla
                    }
                }

                if (baseFile != null)
                    _patchedFiles[filePath] = baseFile;
            }
        }

        public void SaveChanges()
        {
            Directory.CreateDirectory(OutputDirectory);

            foreach (var (path, file) in _patchedFiles)
            {
                var fileBytes = file.SaveFile();

                // Recompress files if it was compressed before patching
                switch (file.Compression)
                {
                    case Compression.Zstd:
                        ConsoleUtilities.WriteLine($"Compressing {file.Name} to ZSTD", ConsoleColor.Blue);
                        using (var compressionStream = new MemoryStream())
                        {
                            ZstdCompressor.Compress(new MemoryStream(fileBytes), compressionStream);
                            fileBytes = compressionStream.ToArray();
                        }
                        break;
                }

                var outputPath = Path.Combine(OutputDirectory, path);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, fileBytes);
            }
        }

        public void CreateResourceSizeTable()
        {
            var romfsPath = Path.Combine(OutputDirectory, "romfs");
            var romfsDirectory = Directory.CreateDirectory(romfsPath);

            var outputPath = Path.Combine(romfsPath, "System", "Resource");
            Directory.CreateDirectory(outputPath);

            var rstbFile = GetVanillaFile("romfs/System/Resource/ResourceSizeTable.srsizetable");

            if (rstbFile != null)
            {
                using var resourceSizeTable = new ResourceSizeTable(rstbFile);

                var allFiles = Directory.GetFiles(romfsPath, "*", SearchOption.AllDirectories);
                foreach (var originalFile in allFiles)
                {

                    var path = Path.GetRelativePath(romfsDirectory.FullName, originalFile).Replace('\\', '/');
                    if (path == "System/Resource/ResourceSizeTable.srsizetable" || path == "System/Resource/ResourceSizeTable.rsizetable")
                        continue;

                    // skip byml files, but not EventFlow byml
                    if (path.EndsWith(".byml") && path != "EventFlow/Info/EventFlowInfoProduct.byml")
                        continue;

                    if (path.EndsWith(".zs"))
                        path = path[..^3];

                    var fileSize = ResourceSizeTable.GetFileSize(originalFile, path);

                    if (fileSize < 0 || fileSize > uint.MaxValue)
                        resourceSizeTable.Dictionary.Remove(path);
                    
                    else if (resourceSizeTable.Dictionary.TryGetValue(path, out var entry) && fileSize != entry.FileSize)
                        entry.FileSize = (uint) fileSize;
                    
                    else if (!resourceSizeTable.Dictionary.ContainsKey(path))
                        resourceSizeTable.AddEntry(new ResourceSizeTable.ResourceTableEntry(path, (uint) fileSize, 0, false));
                    
                }

                outputPath = Path.Combine(outputPath, "ResourceSizeTable.srsizetable");
                File.WriteAllBytes(outputPath, resourceSizeTable.Save());

            }
            else
            {
                ConsoleUtilities.WriteLine("Failed to create RSTB file: Vanilla ResourceSizeTable was not found in vanilla.zip", ConsoleColor.Red);
            }
        }

        public void ExportChangesToJson()
        {
            var diffPath = Path.Combine(OutputDirectory, "diff");

            foreach (var (path, file) in _patchedFiles)
            {
                var outputPath = Path.Combine(diffPath, path);
                file.ExportToJson(outputPath);
            }
        }
    }
}

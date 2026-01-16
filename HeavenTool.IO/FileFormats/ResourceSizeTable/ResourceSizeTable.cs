using System.Text;
using HeavenTool.IO.Compression;
using NintendoTools.Compression.Yaz0;

namespace HeavenTool.IO.FileFormats.ResourceSizeTable;

public class ResourceSizeTable : IDisposable
{
    public static readonly Yaz0Decompressor Decompressor = new();
    public static readonly Yaz0Compressor Compressor = new();

    public class ResourceTableEntry
    {
        /// <summary>
        /// Creates a entry using a hash
        /// </summary>
        /// <param name="hash">CRC32 hash of a <see cref="FileName"/></param>
        /// <param name="fileSize"><inheritdoc cref="FileSize"/></param>
        /// <param name="isDuplicated"></param>
        public ResourceTableEntry(uint hash, uint fileSize, bool isDuplicated)
        {
            CRCHash = hash;
            FileSize = fileSize;
            IsCollided = isDuplicated;
        }

        /// <summary>
        /// Creates a entry using a name
        /// </summary>
        /// <param name="name"></param>
        /// <param name="fileSize"></param>
        /// <param name="isDuplicated"></param>
        public ResourceTableEntry(string name, uint fileSize, uint? dlc, bool isDuplicated)
        {
            CRCHash = name.ToCRC32();
            _fileName = name;
            FileSize = fileSize;
            IsCollided = isDuplicated;
            DLC = dlc;
        }


        /// <summary>
        /// <b>true</b> if <see cref="CRCHash"/> is colliding with another entry
        /// </summary>
        public bool IsCollided { get; set; }

        // Just to prevent a lot of attempts in FileName
        private bool unknownHash;
        private string _fileName;

        /// <summary>
        /// File name, max of 128 characters
        /// </summary>
        public string FileName
        {
            get
            {
                if (CRCHash > 0 && _fileName == null && !unknownHash)
                {
                    // Try to get the file name using our files
                    _fileName = RomFsNameManager.GetValue(CRCHash);

                    // If we don't have the "translated hash" set unknown hash to true
                    unknownHash = _fileName != null;
                }

                return _fileName;
            }

            set { _fileName = value; }
        }

        private uint _hash;

        /// <summary>
        /// CRC32 hash for <see cref="FileName"/>
        /// </summary>
        public uint CRCHash
        {
            get
            {
                if (_hash == 0 && !string.IsNullOrEmpty(_fileName))
                    _hash = _fileName.ToCRC32();

                return _hash;
            }

            set { _hash = value; }
        }

        /// <summary>
        /// File size in bytes 
        /// </summary>
        public uint FileSize { get; set; }

        /// <summary>
        /// Only present on RSTC files, seems to be a DLC number since it's only 1 when it's a file from Happy Home Paradise DLC
        /// </summary>
        public uint? DLC { get; set; }

        /// <summary>
        /// Write entry to binary
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="isRSTC">If the file is RSTC</param>
        public void Write(BinaryWriter writer, bool isRSTC)
        {
            if (IsCollided)
            {
                if (string.IsNullOrEmpty(FileName))
                    throw new Exception($"File name is null! (Hash: {CRCHash})\nMake sure you have a \"extra/romfs-files.txt\" file and it have every ACNH file path!");

                var bytes = Encoding.ASCII.GetBytes(FileName);

                if (bytes.Length > 128)
                    throw new Exception($"File name \"{FileName}\" exceed the 128 bytes!");

                Array.Resize(ref bytes, 128);
                writer.Write(bytes);
            }
            else
            {
                writer.Write(CRCHash);
            }

            writer.Write(FileSize);

            if (isRSTC) writer.Write(DLC ?? 0);
        }
    }

    /// <summary>
    /// RSTB or RSTC
    /// </summary>
    public string HEADER { get; private set; }

    /// <summary>
    /// Get a <seealso cref="ResourceTableEntry"/> from <seealso cref="Entries"/> using <seealso cref="ResourceTableEntry.FileName"/>
    /// </summary>
    public Dictionary<string, ResourceTableEntry> Dictionary { get; private set; }

    /// <summary>
    /// Returns <see cref="Dictionary.Count"/>
    /// </summary>
    public int Length => Dictionary.Count;

    public ResourceTableEntry this[int index]
    {
        get => Dictionary.ElementAt(index).Value;
        set => Dictionary[Dictionary.ElementAt(index).Key] = value;
    }

    /// <summary>
    /// <para>Used when the fileName CRC32 is <b>unique</b>.</para>
    /// Entries in that list does <b>NOT</b> contain the fileName parameter assigned, the CRC should be decrypted using the RomFs folder
    /// </summary>
    public Dictionary<string, ResourceTableEntry> UniqueEntries => Dictionary.Where(x => !x.Value.IsCollided).ToDictionary(x => x.Key, x => x.Value);


    /// <summary>
    /// If have two (or more) file names that have the same hash both are put here
    /// </summary>
    public Dictionary<string, ResourceTableEntry> NonUniqueEntries => Dictionary.Where(x => x.Value.IsCollided).ToDictionary(x => x.Key, x => x.Value);


    public bool IsRSTC => HEADER == "RSTC";

    public bool IsLoaded { get; internal set; }

    /// <summary>
    /// Adds a new entry to the ResourceTable. It will automatically detect for duplicates and move to the right list.
    /// </summary>
    /// <param name="entry"></param>
    public void AddEntry(ResourceTableEntry entry)
    {
        if (entry == null) return;

        if (string.IsNullOrEmpty(entry.FileName))
            throw new Exception("FileName is not defined!");

        if (Dictionary.Values.Any(x => x.CRCHash == entry.CRCHash))
        {
            entry.IsCollided = true;

            foreach (var repeatedEntry in Dictionary.Values.Where(x => x.CRCHash == entry.CRCHash))
                repeatedEntry.IsCollided = true;
        }

        Dictionary.TryAdd(entry.FileName, entry);
    }

    public ResourceSizeTable(byte[] bytes)
    {
        using var compressedStream = new MemoryStream(bytes);
        using var uncompressedStream = new MemoryStream();

        if (Yaz0Decompressor.CanDecompressStatic(compressedStream))
            Decompressor.Decompress(compressedStream, uncompressedStream);
        else 
            compressedStream.CopyTo(uncompressedStream);


        // Start actual reading of our RSTB file
        using var reader = new BinaryFileReader(uncompressedStream);

        HEADER = reader.ReadString(4, Encoding.ASCII);
        if (HEADER != "RSTB" && HEADER != "RSTC")
        {
            ConsoleUtilities.WriteLine($"This is not a valid RSTB/RSTC file! ({HEADER})", ConsoleColor.Red);

            Dispose();
            return;
        }

        var uniqueEntriesCount = reader.ReadUInt32();
        var repeatedEntriesCount = reader.ReadUInt32();

        Dictionary = [];

        for (int i = 0; i < uniqueEntriesCount; i++)
        {
            var hash = reader.ReadUInt32();
            var fileSize = reader.ReadUInt32();

            var entry = new ResourceTableEntry(hash, fileSize, false);

            if (IsRSTC) entry.DLC = reader.ReadUInt32();

            // If the hash is not found, we gonna throw an error and this should prevent the file from loading
            if (entry.FileName != null)
            {
                if (!Dictionary.TryAdd(entry.FileName, entry)) throw new Exception($"Failed to add {entry.FileName} to Dictionary, probably a duplicated entry.");
            }
            else throw new Exception($"Hash {entry.CRCHash:x} not found!");
        }

        for (int i = 0; i < repeatedEntriesCount; i++)
        {
            var fileName = reader.ReadString(128, Encoding.ASCII);
            var fileSize = reader.ReadUInt32();

            var entry = new ResourceTableEntry(fileName, fileSize, null, true);

            if (IsRSTC) entry.DLC = reader.ReadUInt32();

            if (entry.FileName != null)
            {
                if (!Dictionary.TryAdd(entry.FileName, entry)) throw new Exception($"Failed to add {entry.FileName} to Dictionary, probably a duplicated entry.");
            }
        }

        IsLoaded = true;

        UpdateUniques();
    }

    /// <summary>
    /// This function will update <seealso cref="ResourceTableEntry.IsCollided"/> values in <seealso cref="Dictionary"/>.
    /// </summary>
    public void UpdateUniques()
    {
        if (!IsLoaded) return;

        // Group by CRC hash
        var groups = Dictionary.GroupBy(x => x.Value.CRCHash);

        foreach (var group in groups)
        {
            // If this group have more than 1 child, so it is a duplicated entry
            var count = group.Count();

            foreach (var (_, entry) in group)
                entry.IsCollided = count > 1;

        }
    }

    /// <summary>
    /// Save the file
    /// </summary>
    /// <param name="filePath">File Location</param>
    public byte[] Save()
    {
        UpdateUniques();

        try
        {
            var uniqueEntries = Dictionary.Where(x => !x.Value.IsCollided).OrderBy(x => x.Value.CRCHash);
            var nonUniqueEntries = Dictionary.Where(x => x.Value.IsCollided).OrderBy(x => x.Key);

            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream);

            // Write header
            writer.Write(Encoding.ASCII.GetBytes(HEADER));

            writer.Write(uniqueEntries.Count());
            writer.Write(nonUniqueEntries.Count());

            foreach (var (_, entry) in uniqueEntries)
                entry.Write(writer, IsRSTC);

            foreach (var (_, entry) in nonUniqueEntries)
                entry.Write(writer, IsRSTC);


            byte[] array = new byte[memoryStream.Length];
            memoryStream.Position = 0;
            memoryStream.Read(array, 0, array.Length);


            using var compressedStream = new MemoryStream();
            Compressor.Compress(memoryStream, compressedStream);

            return compressedStream.ToArray();
        }
        catch (Exception ex)
        {
            ConsoleUtilities.WriteLine($"Failed to save ResourceSizeTable file: \n{ex.Message}", ConsoleColor.Red);
            return null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="entireFileName"></param>
    /// <param name="romfsName"></param>
    /// <returns>
    /// <para><b>File Size</b> with proper padding</para>
    /// <para>or <b>-1</b> if unsupported Zstd file</para>
    /// <para>or <b>-2</b> if unsupported file</para>
    /// </returns>
    /// <exception cref="OverflowException">Throws when file size exceeds the <see cref="uint.MaxValue"/></exception>
    public static long GetFileSize(string entireFileName, string romfsName)
    {
        // If it's a zstd file
        if (entireFileName.EndsWith(".zs")
            && !romfsName.StartsWith("Layout/")
            && !romfsName.StartsWith("Message/")
            && !romfsName.StartsWith("Model/Layout_"))
            return -1;

        long size;
        if (entireFileName.EndsWith(".zs"))
        {
            var decompressed = ZstdCompressionAlgorithm.Decompress(entireFileName);
            size = decompressed.Length;
        }
        else size = new FileInfo(entireFileName).Length;

        // Round up to the next number divisible by 32
        size = size + 31 & -32;

        if (romfsName.StartsWith("Layout/"))
            size += 8192;
        else if (romfsName.StartsWith("Model/Layout_"))
            size += 9264;
        else if (romfsName.StartsWith("Message/"))
            size += 512;
        else if (entireFileName.EndsWith(".bars"))
            size += 712;
        else if (entireFileName.EndsWith(".bcsv") || entireFileName.EndsWith(".bfevfl") || entireFileName.EndsWith(".byml"))
            size += 416;
        else return -2; // unsupported file

        if (size > uint.MaxValue)
            throw new OverflowException($"{entireFileName} is too big!");

        return size;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private bool disposed = false;
    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                IsLoaded = false;
                Dictionary.Clear();
                Dictionary = null;
            }

            // Indicate that the instance has been disposed.
            disposed = true;
        }
    }
}
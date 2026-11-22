using AeonSake.NintendoTools.Compression.Yaz0;
using AeonSake.NintendoTools.Compression.Zstd;
using AeonSake.NintendoTools.FileFormats;
using AeonSake.NintendoTools.FileFormats.Sarc;
using System.Buffers.Binary;

namespace HeavenTool.IO.FileFormats.PBC;

public enum PbcCompression { None, Zstd, Yaz0 }

/// <summary>
/// A file that holds PBCs: a bare .pbc, or a SARC (such as romfs/Model/*_pbc.Nin_NX_NVN.zs) whose .pbc entries are edited
/// while every other entry is written back as it was.
/// </summary>
public sealed class PbcContainer
{
    private const uint ZstdMagic = 0xfd2fb528;

    // Every entry of the romfs *_pbc archives starts on a 0x80 boundary, as does their data section.
    private static readonly AlignmentTable Alignment = CreateAlignment();

    private static AlignmentTable CreateAlignment()
    {
        var table = new AlignmentTable();
        table.Add(".pbc", 0x80);
        return table;
    }

    private readonly SarcFile? archive;

    public PbcCompression Compression { get; }
    public bool IsArchive => archive != null;
    /// <summary>PBC entries in archive order; a bare file has one, named after the file.</summary>
    public IReadOnlyList<(string Name, PBCFileReader File)> Entries { get; }

    private PbcContainer(SarcFile? archive, PbcCompression compression, List<(string, PBCFileReader)> entries)
    {
        this.archive = archive;
        Compression = compression;
        Entries = entries;
    }

    public static PbcContainer Read(byte[] data, string name)
    {
        var (content, compression) = Decompress(data);
        if (PBCFileReader.CheckMagic(content.AsSpan(0, Math.Min(4, content.Length)).ToArray()))
            return new(null, compression, [(name, new PBCFileReader(content))]);
        if (!content.AsSpan().StartsWith("SARC"u8))
            throw new InvalidDataException("Expected a PBC file or a SARC archive of PBCs.");
        using var stream = new MemoryStream(content, false);
        var archive = new SarcFileReader().Read(stream);
        var entries = archive.Files.Where(file => file.Data.Length >= 4 && PBCFileReader.CheckMagic(file.Data[..4]))
            .Select(file => (file.Name, new PBCFileReader(file.Data))).ToList();
        if (entries.Count == 0) throw new InvalidDataException("The archive contains no PBC files.");
        return new(archive, compression, entries);
    }

    private static (byte[] Data, PbcCompression Compression) Decompress(byte[] data)
    {
        if (data.Length >= 4 && BinaryPrimitives.ReadUInt32LittleEndian(data) == ZstdMagic)
        {
            using var input = new MemoryStream(data, false);
            using var output = new MemoryStream();
            new ZstdDecompressor().Decompress(input, output);
            return (output.ToArray(), PbcCompression.Zstd);
        }
        if (data.AsSpan().StartsWith("Yaz0"u8))
        {
            using var input = new MemoryStream(data, false);
            using var output = new MemoryStream();
            new Yaz0Decompressor().Decompress(input, output);
            return (output.ToArray(), PbcCompression.Yaz0);
        }
        return (data, PbcCompression.None);
    }

    /// <summary>The whole file with the current PBC contents, in its original container and compression.</summary>
    public byte[] Save()
    {
        byte[] content;
        if (archive == null) content = Entries[0].File.SaveAsBytes();
        else
        {
            int index = 0;
            foreach (var file in archive.Files)
                if (index < Entries.Count && file.Name == Entries[index].Name)
                    file.Data = Entries[index++].File.SaveAsBytes();
            using var stream = new MemoryStream();
            new SarcFileWriter { Alignment = Alignment }.Write(archive, stream);
            content = stream.ToArray();
        }
        if (Compression == PbcCompression.None) return content;
        using var input = new MemoryStream(content, false);
        using var output = new MemoryStream();
        if (Compression == PbcCompression.Zstd) new ZstdCompressor { CompressionLevel = 19 }.Compress(input, output);
        else new Yaz0Compressor().Compress(input, output);
        return output.ToArray();
    }
}

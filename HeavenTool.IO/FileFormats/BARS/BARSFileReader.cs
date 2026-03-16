using HeavenTool.IO.FileFormats.BWAV;
using System.Reflection.PortableExecutable;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS;

// TODO: We need to hash the audio data to avoid duplicates, because some files shares the same assetOffset,
// but different metadata (and thus different hash).
//
// Hashing the audio data allows us to identify duplicates without using much memory,
// because we can just store the hash instead of the entire audio data in memory.

public class BARSFileReader : IDisposable
{
    public const string MAGIC = "BARS";

    /// <summary>
    /// Indicates if file is on big-endian byte order.
    /// </summary>
    public bool BigEndian { get; }

    /// <summary>
    /// Major version of BARS header, currently only version 1.1 and 1.2 are supported.
    /// </summary>
    public byte VersionMajor { get; }

    /// <summary>
    /// Minor version of BARS header, currently only version 1.1 and 1.2 are supported.
    /// </summary>
    public byte VersionMinor { get; }

    public AudioAsset[] AudioAssets { get; }

    public BARSFileReader(Stream stream)
    {
        using var reader = new BinaryReader(stream);

        var magic = reader.ReadString(4);

        if (magic != MAGIC)
            throw new Exception("This is not a BARS file");

        var size = reader.ReadInt32();
        var endian = reader.ReadUInt16();

        reader.BigEndian = BigEndian = endian != 0xFEFF;

        VersionMajor = reader.ReadByte();
        VersionMinor = reader.ReadByte();

        if (VersionMajor != 1 || (VersionMinor != 1 && VersionMinor != 2))
            throw new Exception("BARS version not supported");

        var assetCount = reader.ReadInt32();
        AudioAssets = new AudioAsset[assetCount];

        // crc hashes
        for (int i = 0; i < assetCount; i++)
        {
            AudioAssets[i] = new AudioAsset()
            {
                Hash = reader.ReadUInt32()
            };
        }

        for (int i = 0; i < assetCount; i++)
        {
            var audioAsset = AudioAssets[i];

            var metadataOffset = reader.ReadUInt32();

            using (reader.CreateScopeAt(metadataOffset))
            {
                reader.Skip(4); // magic
                reader.Skip(2); // endianness
                reader.Skip(2); // version
                var metadataSize = reader.ReadInt32();

                audioAsset.RawAudioMetadata = reader.ReadByteArrayAt(metadataOffset, metadataSize);
                audioAsset.AudioMetadata = new AudioMetadata(audioAsset);
            }

            var checkHash = audioAsset.AudioMetadata.AssetName.ToCRC32();
            if (checkHash != audioAsset.Hash) 
                throw new Exception($"Invalid CRC32 Hash for {i}: {audioAsset}!\n" +
                    $"Expected: {audioAsset.Hash:x} | Got {checkHash:x}");

            audioAsset.AssetOffset = reader.ReadInt32();
        }

        // Sort AudioAssets by lowest assetOffset
        Array.Sort(AudioAssets, (a, b) => a.AssetOffset.CompareTo(b.AssetOffset));

        // Here we have to group because some files shares the same assetOffset
        var groups = AudioAssets.GroupBy(x => x.AssetOffset).ToList();
        int groupCount = groups.Count;

        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            var assetOffset = group.Key;

            if (assetOffset <= 0)
                throw new Exception("Invalid asset offset, file may be corrupted");

            // Read the magic string for BWAV check
            var assetMagic = reader.ReadStringAt(assetOffset, 4);
            if (assetMagic != "BWAV")
                throw new Exception($"Only BWAV files are supported at this moment. (Got {assetMagic})");

            var nextAssetOffset = (i + 1 < groupCount) ? groups[i + 1].Key : (int) reader.Length;
            var assetData = reader.ReadByteArrayAt(assetOffset, nextAssetOffset - assetOffset);
            var binaryWave = new BinaryWaveFile(assetData);

            // Assign the BinaryWave to all the audio assets in the group
            foreach (var audioAsset in group)
            {
                audioAsset.BinaryWave = binaryWave;
                audioAsset.RawBinaryWave = assetData;
            }
        }
    }

    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(MAGIC);
        var size = writer.CreatePointer();

        // write endian
        writer.Write((ushort)(BigEndian ? 0xFEFF : 0xFFFE));
        writer.Write(VersionMajor);
        writer.Write(VersionMinor);
        writer.Write(AudioAssets.Length);

        foreach(var audioAsset in AudioAssets)
        {
            writer.Write(audioAsset.Hash);
        }

        var pointers = new (WriterScopePointer metadataPointer, WriterScopePointer assetPointer)[AudioAssets.Length];

        for (int i = 0; i < AudioAssets.Length; i++)
            pointers[i] = (writer.CreatePointer(), writer.CreatePointer());

        foreach (var audioAsset in AudioAssets)
        {
            writer.Write(audioAsset.AudioMetadata?.ToBytes() ?? throw new Exception($"Failed to save audio metadata"));
        }
        

        // Write file size at the beginning of the file
        size.Resolve((uint) writer.Length);
        return stream.ToArray();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private bool _disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        _disposed = true;
    }
}
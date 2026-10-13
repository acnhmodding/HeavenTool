using HeavenTool.IO.FileFormats.BARS.MINF.Sections;
using HeavenTool.IO.FileFormats.BWAV;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS;

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

    /// <summary>
    /// Public asset name hashes found in BARS v1.2.
    /// </summary>
    public uint[]? Hashes { get; }

    public AudioAsset[] AudioAssets { get; }

    public BARSFileReader(Stream stream, PerformanceSequenceFormat? performanceFormat = null)
    {
        using var reader = new BinaryReader(stream);

        var magic = reader.ReadString(4);

        if (magic != MAGIC)
            throw new Exception("This is not a BARS file");

        var size = reader.ReadInt32();
        var endian = reader.ReadUInt16();

        reader.BigEndian = BigEndian = endian != 0xFEFF;

        var version = reader.ReadUInt16();
        VersionMajor = (byte)(version >> 8);
        VersionMinor = (byte)version;

        if (VersionMajor != 1 || VersionMinor is not 1 and not 2)
            throw new Exception("BARS version not supported");

        var assetCount = reader.ReadInt32();
        AudioAssets = new AudioAsset[assetCount];

        for (var i = 0; i < assetCount; i++)
        {
            AudioAssets[i] = new AudioAsset()
            {
                Hash = reader.ReadUInt32()
            };
        }

        for (var i = 0; i < assetCount; i++)
        {
            var audioAsset = AudioAssets[i];

            var metadataOffset = reader.ReadUInt32();

            using (reader.CreateScopeAt(metadataOffset))
            {
                var archiveEndian = reader.BigEndian;
                var bom = reader.ReadUInt16At(metadataOffset + 4);
                int metadataSize;
                reader.BigEndian = bom == (archiveEndian ? 0xFEFF : 0xFFFE);
                try { metadataSize = reader.ReadInt32At(metadataOffset + 8); }
                finally { reader.BigEndian = archiveEndian; }

                audioAsset.RawAudioMetadata = reader.ReadByteArrayAt(metadataOffset, metadataSize);
                audioAsset.AudioMetadata = new AudioMetadata(audioAsset, performanceFormat);
            }

            var checkHash = audioAsset.AudioMetadata.AssetName.ToCRC32();
            if (checkHash != audioAsset.Hash)
                throw new Exception($"Invalid CRC32 Hash for {i}: {audioAsset}!\n" +
                    $"Expected: {audioAsset.Hash:x} | Got {checkHash:x}");

            audioAsset.AssetOffset = reader.ReadInt32();
        }

        if (VersionMinor is 2)
        {
            Hashes = new uint[reader.ReadUInt32()];

            for (var i = 0; i < Hashes.Length; i++)
                Hashes[i] = reader.ReadUInt32();
        }

        // Ordered payload offsets delimit each shared BWAV buffer.
        Array.Sort(AudioAssets, (a, b) => a.AssetOffset.CompareTo(b.AssetOffset));

        var groups = AudioAssets.GroupBy(x => x.AssetOffset).ToArray();

        for (var i = 0; i < groups.Length; i++)
        {
            var group = groups[i];
            var assetOffset = group.Key;

            if (assetOffset <= 0)
                throw new Exception("Invalid asset offset, file may be corrupted");

            var assetMagic = reader.ReadStringAt(assetOffset, 4);
            if (assetMagic != "BWAV")
                throw new Exception($"Only BWAV files are supported at this moment. (Got {assetMagic})");

            var nextAssetOffset = (i + 1 < groups.Length) ? groups[i + 1].Key : (int)reader.Length;
            var assetData = reader.ReadByteArrayAt(assetOffset, nextAssetOffset - assetOffset);
            var binaryWave = new BinaryWaveFile(assetData);

            foreach (var audioAsset in group)
            {
                audioAsset.BinaryWave = binaryWave;
                audioAsset.RawBinaryWave = assetData;
                audioAsset.IsPrefetch = binaryWave.AssetType == AssetType.Prefetch;
            }
        }
    }

    public byte[] ToBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.BigEndian = BigEndian;
        var assets = AudioAssets.OrderBy(a => a.AudioMetadata?.AssetName.ToCRC32() ?? a.Hash).ToArray();

        writer.Write(MAGIC);
        var size = writer.CreatePointer();

        writer.Write((ushort)0xFEFF);
        writer.Write((ushort)((VersionMajor << 8) | VersionMinor));
        writer.Write(assets.Length);

        foreach (var audioAsset in assets)
        {
            writer.Write(audioAsset.AudioMetadata?.AssetName.ToCRC32() ?? audioAsset.Hash);
        }

        var pointers = new (WriterScopePointer metadataPointer, WriterScopePointer assetPointer)[AudioAssets.Length];

        for (var i = 0; i < AudioAssets.Length; i++)
            pointers[i] = (writer.CreatePointer(), writer.CreatePointer());

        if (VersionMinor is 2)
        {
            writer.Write(Hashes?.Length ?? 0);

            if (Hashes != null)
            {
                foreach (var hash in Hashes)
                    writer.Write(hash);
            }
        }

        for (var i = 0; i < assets.Length; i++)
        {
            writer.Align(4);
            pointers[i].metadataPointer.Resolve((uint)writer.Position);
            writer.Write(assets[i].AudioMetadata?.ToBytes()
                ?? throw new InvalidDataException("Audio metadata is missing."));
        }

        // Entries sharing a BWAV instance continue to share one payload.
        var offsets = new Dictionary<BinaryWaveFile, uint>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < assets.Length; i++)
        {
            var wave = assets[i].BinaryWave ?? throw new InvalidDataException("BWAV data is missing.");
            if (!offsets.TryGetValue(wave, out var offset))
            {
                writer.Align(0x40);
                offset = checked((uint)writer.Position);
                offsets.Add(wave, offset);
                writer.Write(wave.ToBytes());
            }
            pointers[i].assetPointer.Resolve(offset);
        }

        size.Resolve((uint)writer.Length);
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

using HeavenTool.IO.FileFormats.BWAV;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.IO.FileFormats.BARS;

public class AudioAsset
{
    /// <summary>
    /// CRC32 Hash for the Asset name
    /// </summary>
    public uint Hash { get; set; }

    /// <summary>
    /// Offset for the asset data, this is used to read the actual audio data from the file.
    /// </summary>
    public int AssetOffset { get; set; }
    public bool IsPrefetch { get; set; } = false;

    /// <summary>
    /// Gets or sets the metadata information associated with the audio content.
    /// </summary>
    public AudioMetadata AudioMetadata { get; set; }

    /// <summary>
    /// Gets or sets the binary wave file associated with the current instance.
    /// </summary>
    public BinaryWaveFile BinaryWave { get; set; }

    public override string ToString()
    {
        return AudioMetadata?.AssetName ?? "[Metadata not found]";
    }
}

public class BARSFileReader
{
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

        if (magic != "BARS")
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

            // Read Audio Metadata from offset
            using (reader.CreateScopeAt(reader.ReadUInt32()))
                audioAsset.AudioMetadata = new AudioMetadata(audioAsset, reader);

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
            var magicBwav = reader.ReadStringAt(assetOffset, 4);
            if (magicBwav != "BWAV")
                throw new Exception($"Only BWAV files are supported at this moment. (Got {magicBwav})");

            var nextAssetOffset = (i + 1 < groupCount) ? groups[i + 1].Key : (int) reader.Length;
            var assetData = reader.ReadByteArrayAt(assetOffset, nextAssetOffset - assetOffset);
            var binaryWave = new BinaryWaveFile(assetData);

            // Assign the BinaryWave to all the audio assets in the group
            foreach (var audioAsset in group)
                audioAsset.BinaryWave = binaryWave;
        }
    }
}
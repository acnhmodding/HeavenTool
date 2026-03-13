using HeavenTool.IO.FileFormats.BWAV;

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
    public AudioMetadata? AudioMetadata { get; set; }

    /// <summary>
    /// Gets or sets the binary wave file associated with the current instance.
    /// </summary>
    public BinaryWaveFile? BinaryWave { get; set; }

    public byte[]? RawBinaryWave { get; set; }
    public byte[]? RawAudioMetadata { get; internal set; }

    public override string ToString()
    {
        return AudioMetadata?.AssetName ?? "[Metadata not found]";
    }
}

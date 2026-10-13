namespace HeavenTool.IO.FileFormats.BARS.AMTA;

[Flags]
public enum MetadataFlags : uint
{
    None = 0,
    IsStreaming = 1 << 0,
    HasLocalSamples = 1 << 1,
    IsLoop = 1 << 2,
    IsOpus = 1 << 4,
    IsOpusDecodeFourFramesAtOnce = 1 << 6,
    IsUniformAttenuationPerTrack = 1 << 7
}

namespace HeavenTool.IO.FileFormats.BARS.AMTA;

[Flags]
public enum OptionalDataFlags : uint
{
    None = 0,
    MaxAmplitude = 1 << 0,
    Unknown01 = 1 << 1,
    MaxMomentaryLufs = 1 << 2,
    IntegratedLufs = 1 << 3,
    TailLength = 1 << 4,
    Points = 1 << 5,
    Unknown06 = 1 << 6,
    Unknown07 = 1 << 7
}

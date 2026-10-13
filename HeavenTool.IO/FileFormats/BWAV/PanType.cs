namespace HeavenTool.IO.FileFormats.BWAV;

public enum PanType : ushort
{
    Left = 0,
    Right = 1,
    Center = 2,
    LowFrequencyEffects = 3,
    LeftSurround = 4,
    RightSurround = 5,
    Invalid = 6
}

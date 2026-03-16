namespace HeavenTool.IO.FileFormats.BARS.MINF;

public static class PitchUtilities
{
    private static readonly Dictionary<byte, string> Pitches = new()
    {
        { 0x56, "D7" },
        { 0x55, "C#7" },
        { 0x54, "C7" },
        { 0x53, "B6" },
        { 0x52, "A#6" },
        { 0x51, "A6" },
        { 0x50, "G#6" },
        { 0x4F, "G6" },
        { 0x4E, "F#6" },
        { 0x4D, "F6" },
        { 0x4C, "E6" },
        { 0x4B, "D#6" },
        { 0x4A, "D6" },
        { 0x49, "C#6" },
        { 0x48, "C6" },
        { 0x47, "B5" },
        { 0x46, "A#5" },
        { 0x45, "A5" },
        { 0x44, "G#5" },
        { 0x43, "G5" },
        { 0x42, "F#5" },
        { 0x41, "F5" },
        { 0x40, "E5" },
        { 0x3F, "D#5" },
        { 0x3E, "D5" },
        { 0x3D, "C#5" },
        { 0x3C, "C5" },
        { 0x3B, "B4" },
        { 0x3A, "A#4" },
        { 0x39, "A4" },
        { 0x38, "G#4" },
        { 0x37, "G4" },
        { 0x36, "F#4" },
        { 0x35, "F4" },
        { 0x34, "E4" },
        { 0x33, "D#4" },
        { 0x32, "D4" },
        { 0x31, "C#4" },
        { 0x30, "C4" },
        { 0x2F, "B3" }
    };


    public static string ByteToChord(byte hex)
    {
        if (hex == 0) return "";

        if (Pitches.TryGetValue(hex, out string? chord))
            return chord;

        throw new Exception("Chord not supported");
    }

    public static byte ChordToByte(string chord)
    {
        foreach (var pair in Pitches)
            if (pair.Value.Equals(chord, StringComparison.OrdinalIgnoreCase))
                return pair.Key;

        throw new Exception("Chord not supported");
    }
}

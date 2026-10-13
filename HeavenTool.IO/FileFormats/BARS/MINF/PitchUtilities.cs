namespace HeavenTool.IO.FileFormats.BARS.MINF;

public static class PitchUtilities
{
    private static readonly string[] PitchNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    public static string ByteToChord(byte hex)
    {
        if (hex == 0) return "";

        if (hex is >= 0x2F and <= 0x56)
            return $"{PitchNames[hex % 12]}{hex / 12}";

        return $"0x{hex:X2}";
    }

    public static byte ChordToByte(string chord)
    {
        if (string.IsNullOrEmpty(chord)) return 0;
        if (chord.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && byte.TryParse(chord.AsSpan(2), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var raw))
            return raw;
        for (byte pitch = 0x2F; pitch <= 0x56; pitch++)
            if (ByteToChord(pitch).Equals(chord, StringComparison.OrdinalIgnoreCase))
                return pitch;

        throw new Exception("Chord not supported");
    }
}

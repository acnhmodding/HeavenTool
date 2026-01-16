using System.Text;

namespace HeavenTool.IO;

public class BinaryFileReader : BinaryReader
{
    public long Position
    {
        get => BaseStream.Position;
        set => BaseStream.Position = value;
    }

    public BinaryFileReader(Stream stream, bool leaveOpen = false) : base(stream, Encoding.ASCII, leaveOpen)
    {
        Position = 0;
    }

    public static byte[] TrimEnd(byte[] array)
    {
        int lastIndex = Array.FindLastIndex(array, b => b != 0);

        Array.Resize(ref array, lastIndex + 1);

        return array;
    }

    public string ReadString(int length, Encoding encoding)
    {
        var bytes = TrimEnd(ReadBytes(length));

        return encoding.GetString(bytes);
    }
}

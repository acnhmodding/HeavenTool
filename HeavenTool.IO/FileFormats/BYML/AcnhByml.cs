#nullable enable

using AeonSake.NintendoTools.FileFormats.Byml;
using System.Text;

namespace HeavenTool.IO.FileFormats.BYML;

/// <summary>
/// Reads and writes the game's BYML files. Their strings are UTF-8, where NintendoTools defaults to Shift JIS, which garbles
/// Japanese text and sorts the string tables in an order the game refuses.
/// </summary>
public static class AcnhByml
{
    public static BymlFile Read(Stream stream) => new BymlFileReader { Encoding = Encoding.UTF8 }.Read(stream);

    public static BymlFile Read(byte[] data) => Read(new MemoryStream(data, false));

    public static byte[] Write(BymlFile file)
    {
        using var stream = new MemoryStream();
        new BymlFileWriter { Encoding = Encoding.UTF8 }.Write(file, stream);
        return stream.ToArray();
    }
}

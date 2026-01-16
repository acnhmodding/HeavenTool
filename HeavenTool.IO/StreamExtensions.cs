using System.Text;

namespace HeavenTool.IO;

public static class StreamExtensions
{
    /// <summary>
    /// Copy stream to a byte array
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public static byte[] ToArray(this Stream stream)
    {
        byte[] copy = new byte[stream.Length];
        stream.Seek(0, SeekOrigin.Begin);
        stream.Read(copy, 0, copy.Length);
        return copy;
    }

    /// <summary>
    /// Read a byte string in the current stream
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="size"></param>
    /// <param name="encoding"></param>
    /// <returns></returns>
    public static string ReadString(this Stream stream, int size, Encoding encoding)
    {
        byte[] bytes = new byte[size];
        stream.Read(bytes, 0, bytes.Length);

        return encoding.GetString(bytes);
    }
}

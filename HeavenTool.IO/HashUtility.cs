using Force.Crc32;
using MurmurHash;
using System.Text;

namespace HeavenTool.IO;

public static class HashUtility
{
    public static uint ToCRC32(this string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Crc32Algorithm.Compute(bytes);
    }

    public static uint ToMurmur(this string value)
    {
        ReadOnlySpan<byte> inputSpan = Encoding.UTF8.GetBytes(value).AsSpan();

        return MurmurHash3.Hash32(ref inputSpan, 0);
    }
}

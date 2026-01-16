using HeavenTool.IO.FileFormats.BCSV;

namespace HeavenTool.IO;

public static class FileExtensions
{
    public static Type ToType(this DataType type)
    {
        return type switch
        {
            DataType.String => typeof(string),
            DataType.S8 => typeof(sbyte),
            DataType.U8 => typeof(byte),
            DataType.Int16 => typeof(short),
            DataType.UInt16 => typeof(ushort),
            DataType.Int32 => typeof(int),
            DataType.UInt32 => typeof(uint),
            DataType.Float32 => typeof(float),
            DataType.Float64 => typeof(double),

            DataType.BitField => typeof(byte[]),

            //TODO: Give Murmur and CRC their own classes
            DataType.MMH3 => typeof(uint),
            DataType.CRC32 => typeof(uint),
            _ => throw new NotImplementedException(),
        };
    }
}

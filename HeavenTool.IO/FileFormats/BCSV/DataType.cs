namespace HeavenTool.IO.FileFormats.BCSV;

public enum DataType
{
    S8, // sByte
    U8, // Byte
    Int16,
    UInt16,
    Int32,
    UInt32,

    Float32,
    Float64,

    CRC32,
    MMH3,

    String,
    BitField,
}

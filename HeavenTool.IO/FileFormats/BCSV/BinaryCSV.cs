using System.Buffers.Binary;
using System.Text;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BCSV;

// Thanks to https://nintendo-formats.com/games/acnh/bcsv.html
// The information about HasExtendedHeader and padding helped a lot =D

public class BinaryCSV
{
    private static readonly string[] _uniqueFieldNames =
        [
            "UniqueID u16", // Most files use this

            // Specific files
            "Label string8", // NmlNpcRaceParam
            "ItemUniqueID u16", // WherearenItemKind
            "StateName string48", // PlayerStateParam
            "NumberingId u16", // AmiiboData
            "ResourceName string33", // TVProgram
            "StageName string32", // FieldCreateParam
            "ItemFrom string32", // ShopItemRouteFlags
            "TVProgramName.hshCstringRef", // TVProgramMonday
            "GroundAttributeUniqueID u16", // FieldLandMakingRoadKindParam
            "NpcRoleSetID u16", // WherearenRollSet

           // Don't have anything we can do, need a specific way of handling
                // FishAppearRiverParam.bcsv,
                // NpcLife.bcsv,
                // SeafoodAppearParam.bcsv,
                // FishAppearSeaParam.bcsv,
                // FgFlowerHeredity.bcsv,
                // NpcCastLabelData.bcsv,
                // NpcInterest.bcsv,
                // NpcCastScheduleData.bcsv,
                // SeasonCalendar.bcsv,
                // FieldLandMakingActionParam.bcsv,
                // NpcMoveRoomTemplate.bcsv
        ];

    private static readonly uint[] _unknownUniqueHashes = [
        0x37571146, // MessageCardSelectDesign, MessageCardSelectDesignSp, MessageCardSelectPresent and MessageCardSelectPresentSp
    ];

    private static uint[]? _uniqueHashes;
    public static uint[] UniqueHashes
    {
        get
        {
            _uniqueHashes ??= [.. _uniqueFieldNames.Select(x => x.ToCRC32()), .. _unknownUniqueHashes];
            return _uniqueHashes;
        }
    }

    /// <summary>
    /// The size of each entry
    /// </summary>
    internal int EntrySize { get; set; }

    /// <summary>
    /// Table of <seealso cref="Field"/> (columns)
    /// </summary>
    public Field[] Fields { get; set; } = [];

    /// <summary>
    /// List of <see cref="BCSVEntry"/>
    /// </summary>
    public List<object[]> Entries { get; set; }

    private bool _uniqueFieldSearched = false;
    private Field? _uniqueField = null;

    /// <summary>
    /// Get the Unique Field for the current file
    /// </summary>
    public Field? UniqueField
    {
        get
        {
            if (_uniqueField == null && !_uniqueFieldSearched)
            {
                _uniqueFieldSearched = true;
                var uniqueHeader = UniqueHashes.FirstOrNull(x => Fields.Any(f => f.Hash == x));

                foreach (var f in Fields)
                    if (f.Hash == uniqueHeader)
                    {
                        _uniqueField = f;
                        break;
                    }
            }

            return _uniqueField;
        }
    }

    public int Length => Entries.Count;

    internal byte HasExtendedHeader { get; private set; }
    internal byte UnknownField { get; private set; }

    internal int HeaderVersion { get; private set; }

    private static ReadOnlySpan<byte> MAGIC => "VSCB"u8;

    public object this[int row, int column]
    {
        get => Entries[row][column];
        set => Entries[row][column] = value;
    }

    public object this[int row, Field field]
    {
        get => Entries[row][Array.IndexOf(Fields, field)];
        set => Entries[row][Array.IndexOf(Fields, field)] = value;
    }

    public BinaryCSV(int entrySize, Field[] fields, List<object[]> entries, byte hasExtendedHeader, byte unknownField, int version)
    {
        EntrySize = entrySize;
        Fields = fields;
        Entries = [.. entries];

        HasExtendedHeader = hasExtendedHeader;
        UnknownField = unknownField;

        HeaderVersion = version;
    }

    private static readonly (string Suffix, DataType Type, Func<int, bool>? Condition)[] suffixRules =
    [
        (" s8",  DataType.S8,  size => size == 1),
        (" u8",  DataType.U8,  size => size == 1),
        (" s16", DataType.Int16, size => size == 2),
        (" u16", DataType.UInt16, size => size == 2),
        (" s32", DataType.Int32, size => size == 4),
        (" u32", DataType.UInt32, size => size == 4),
        (" f32", DataType.Float32, size => size == 4),
        (".hshCstringRef", DataType.CRC32, size => size == 4),

        // Can be either a BitField or a u8 that end up as last column which is aligned to 4
        // HashManager.KnownTypes should be able to care of that
        (" u8", DataType.BitField, size => size > 1),
    ];

    private static readonly Dictionary<int, DataType> baseTypeBySize = new()
    {
        { 1, DataType.U8 },
        { 2, DataType.UInt16 },
        { 4, DataType.UInt32 }
    };

    private static DataType ApplySuffixRules(string? name, int size, DataType current)
    {
        if (name == null) return current;

        if (current == DataType.BitField && name.Contains(" string", StringComparison.Ordinal))
            return DataType.String;

        foreach (var rule in suffixRules)
            if (name.EndsWith(rule.Suffix) && (rule.Condition == null || rule.Condition(size)))
                return rule.Type;

        return current;
    }

    public static BinaryCSV CopyFileWithoutEntries(BinaryCSV fileToCopy) => new(fileToCopy.EntrySize, fileToCopy.Fields, [], fileToCopy.HasExtendedHeader, fileToCopy.UnknownField, fileToCopy.HeaderVersion);

    public BinaryCSV(Stream stream)
    {
        HashManager.InitializeHashes();

        if (!stream.CanSeek)
        {
            var newStream = new MemoryStream();
            stream.CopyTo(newStream);
            newStream.Position = 0;
            stream = newStream;
        }

        using var reader = new BinaryReader(stream);

        if (reader.BaseStream.Length < 12)
            throw new InvalidDataException("The BCSV header is incomplete.");

        reader.Position = 0;
        var entryCount = reader.ReadUInt32();
        EntrySize = reader.ReadInt32();
        var fieldCount = reader.ReadUInt16();

        HasExtendedHeader = reader.ReadByte();
        UnknownField = reader.ReadByte(); // maybe is japanese flag, for japanese enums

        if (HasExtendedHeader == 1)
        {
            var magic = reader.ReadByteArray(4);

            if (!MAGIC.SequenceEqual(magic))
                throw new Exception("File is not a BCSV!");

            HeaderVersion = reader.ReadInt32();

            // 8 byte padding
            reader.Skip(8);
        }

        long dataOffset = reader.Position + fieldCount * 8L;
        if (EntrySize < 0 || (EntrySize == 0 && entryCount != 0) || entryCount > int.MaxValue
            || dataOffset + EntrySize * entryCount > reader.BaseStream.Length)
            throw new InvalidDataException("The BCSV row count or row size exceeds the file length.");

        // Read Fields
        Fields = new Field[fieldCount];
        for (ushort i = 0; i < fieldCount; i++)
        {
            Fields[i] = new Field()
            {
                Hash = reader.ReadUInt32(),
                Offset = reader.ReadInt32(),
            };
        }

        for (ushort i = 0; i < Fields.Length; i++)
        {
            var currentField = Fields[i];

            // If it's not the last one is the next offset to calculate the current size, otherwise use EntrySize
            currentField.Size = (i < Fields.Length - 1 ? Fields[i + 1].Offset : EntrySize) - currentField.Offset;


            var translatedName = currentField.GetTranslatedNameOrNull();
            // An unknown field may contain opaque data, not UTF-8 text.
            var type = baseTypeBySize.TryGetValue(currentField.Size, out var baseType) ? baseType : DataType.BitField;

            // Consider the translated name to get the correct type.
            type = ApplySuffixRules(translatedName, currentField.Size, type);

            // Enforce known types by hash, this is specially important for the fields that end up as BitFields because of alignment
            if (HashManager.KnownTypes.TryGetValue(currentField.HEX, out var value))
                type = value;

            currentField.DataType = type;
        }

        Entries = new List<object[]>(checked((int)entryCount));

        foreach (var field in Fields)
        {
            int size = field.DataType switch
            {
                DataType.S8 or DataType.U8 => 1,
                DataType.Int16 or DataType.UInt16 => 2,
                DataType.Float32 or DataType.Int32 or DataType.UInt32 or DataType.CRC32 or DataType.MMH3 => 4,
                DataType.String or DataType.BitField => field.Size,
                _ => 0,
            };
            if (field.Offset < 0 || size < 0 || field.Offset > EntrySize - size)
                throw new InvalidDataException($"Field {field.HEX} extends beyond the declared row size.");
        }
        var rowBuffer = new byte[EntrySize];

        for (var i = 0; i < entryCount; i++)
        {
            var newEntry = new object[Fields.Length];

            // Read entire row into rowBuffer
            reader.ReadByteSpan(rowBuffer);

            for (ushort fieldId = 0; fieldId < Fields.Length; fieldId++)
            {
                var currentField = Fields[fieldId];

                ReadOnlySpan<byte> data = rowBuffer.AsSpan(currentField.Offset);

                newEntry[fieldId] = currentField.DataType switch
                {
                    DataType.BitField => data[..currentField.Size].ToArray(),
                    DataType.S8 => unchecked((sbyte)data[0]),
                    DataType.U8 => data[0],
                    DataType.Float32 => BinaryPrimitives.ReadSingleLittleEndian(data),
                    DataType.Int16 => BinaryPrimitives.ReadInt16LittleEndian(data),
                    DataType.UInt16 => BinaryPrimitives.ReadUInt16LittleEndian(data),
                    DataType.Int32 => BinaryPrimitives.ReadInt32LittleEndian(data),
                    DataType.UInt32 or DataType.CRC32 or DataType.MMH3 => BinaryPrimitives.ReadUInt32LittleEndian(data),
                    DataType.String => ReadFixedString(data[..currentField.Size]),
                    _ => 0,
                };
            }

            Entries.Add(newEntry);
        }
        return;
    }

    private static string ReadFixedString(ReadOnlySpan<byte> data)
    {
        int terminator = data.IndexOf<byte>(0);
        if (terminator >= 0)
            data = data[..terminator];
        return Encoding.UTF8.GetString(data);
    }

    public byte[] Save()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write(Entries.Count);
        writer.Write(EntrySize);
        writer.Write((ushort)Fields.Length);

        writer.Write(HasExtendedHeader);
        writer.Write(UnknownField);

        if (HasExtendedHeader == 1)
        {
            writer.Write(MAGIC);
            writer.Write(HeaderVersion);

            // Padding
            writer.Pad(8);
        }


        foreach (var field in Fields)
        {
            writer.Write(field.Hash);
            writer.Write(field.Offset);
        }

        for (var currentEntry = 0; currentEntry < Entries.Count; currentEntry++)
        {
            var startPosition = writer.Position;
            writer.Write((uint)startPosition);

            var entry = Entries[currentEntry];
            for (var fieldId = 0; fieldId < Fields.Length; fieldId++)
            {
                var cellPosition = writer.Position;
                var field = Fields[fieldId];
                var entryValue = entry[fieldId];
                switch (field.DataType)
                {
                    case DataType.BitField:
                        writer.Write((byte[])entryValue);
                        break;

                    case DataType.S8:
                        writer.Write((sbyte)entryValue);
                        break;

                    case DataType.U8:
                        writer.Write((byte)entryValue);
                        break;

                    case DataType.Float32:
                        writer.Write((float)entryValue);
                        break;

                    case DataType.Int16:
                        writer.Write((short)entryValue);
                        break;

                    case DataType.UInt16:
                        writer.Write((ushort)entryValue);
                        break;

                    case DataType.Int32:
                        writer.Write((int)entryValue);
                        break;

                    case DataType.UInt32:
                    case DataType.CRC32:
                    case DataType.MMH3:
                        writer.Write((uint)entryValue);
                        break;


                    case DataType.String:
                        {
                            try
                            {
                                var stringValue = entryValue?.ToString() ?? "";
                                var bytes = Encoding.UTF8.GetBytes(stringValue);
                                Array.Resize(ref bytes, field.Size);
                                bytes[^1] = 0;
                                writer.Write(bytes);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Failed to save string\n{ex}");
                            }
                        }
                        break;
                }

                var padding = field.Size - (writer.Position - cellPosition);
                if (padding > 0) writer.Pad(padding);
                else if (padding < 0) throw new Exception($"Cell value \"{entryValue}\" exceeds size of {field.Size} bytes.");
            }

            writer.Align(4);
        }

        writer.Align(4);

        return stream.ToArray();
    }

}
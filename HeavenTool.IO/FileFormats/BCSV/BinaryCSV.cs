using System.Text;

namespace HeavenTool.IO.FileFormats.BCSV;

// Thanks to https://nintendo-formats.com/games/acnh/bcsv.html
// The information about HasExtendedHeader and padding helped a lot =D

public class BinaryCSV : IDisposable
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

    private static uint[] _uniqueHashes;
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
    public Field[] Fields { get; set; }

    /// <summary>
    /// List of <see cref="BCSVEntry"/>
    /// </summary>
    public List<object[]> Entries { get; set; }

    private bool _uniqueFieldSearched = false;
    private Field _uniqueField = null;

    /// <summary>
    /// Get the Unique Field for the current file
    /// </summary>
    public Field UniqueField { 
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

    internal readonly byte[] MAGIC = "VSCB"u8.ToArray();

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

    public static BinaryCSV CopyFileWithoutEntries(BinaryCSV fileToCopy) => new(fileToCopy.EntrySize, fileToCopy.Fields, [], fileToCopy.HasExtendedHeader, fileToCopy.UnknownField, fileToCopy.HeaderVersion);


    public BinaryCSV(byte[] bytes)
    {
        HashManager.InitializeHashes();

        using var fileStream = new MemoryStream(bytes);
        using var reader = new BinaryFileReader(fileStream);

        var entryCount = reader.ReadUInt32();
        EntrySize = reader.ReadInt32();
        var fieldCount = reader.ReadUInt16();

        HasExtendedHeader = reader.ReadByte();
        UnknownField = reader.ReadByte(); // maybe is japanese flag, for japanese enums

        if (HasExtendedHeader == 1)
        {
            var magic = reader.ReadBytes(4); 

            if (!MAGIC.SequenceEqual(magic))
                throw new Exception("File is not a BCSV!");

            HeaderVersion = reader.ReadInt32();

            // 8 byte padding
            reader.Position += 8;
        }

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

            // TODO: Make it less hard-coded LOL
            DataType type = DataType.String;
            var translatedName = currentField.GetTranslatedNameOrNull();
            switch (currentField.Size)
            {
                case 1:
                    {
                        type = DataType.U8;

                        if (translatedName != null && translatedName.EndsWith(" s8"))
                            type = DataType.S8;

                    }
                    break;

                case 2:
                    {
                        if (translatedName != null && translatedName.EndsWith(" s16"))
                            type = DataType.Int16;
                        else
                            type = DataType.UInt16;
                    }
                    break;

                case 4:
                    {
                        type = DataType.UInt32;

                        if (translatedName != null)
                        {
                            if (translatedName.EndsWith(".hshCstringRef"))
                                type = DataType.CRC32;

                            else if (translatedName.EndsWith(" s32"))
                                type = DataType.Int32;

                            else if (translatedName.EndsWith(" f32"))
                                type = DataType.Float32;
                        }
                    }
                    break;
            }

            // Assign "trusted types", by using translated hash
            if (translatedName != null)
            {
                if (translatedName.EndsWith(" u8") && currentField.Size > 1)
                    type = DataType.BitField;
                else if (translatedName.EndsWith(" s8") && currentField.Size > 1)
                    type = DataType.S8;
                
            }

            if (HashManager.KnownTypes.TryGetValue(currentField.HEX, out DataType value))
                type = value;
            

            currentField.DataType = type;
        }

        // Get Type for each field
        Entries = [];

        for (int i = 0; i < entryCount; i++)
        {
            var newEntry = new object[Fields.Length];
            var entryPosition = reader.ReadUInt32(); // should be the same as reader.Position if we did everything correctly

            for (ushort fieldId = 0; fieldId < Fields.Length; fieldId++)
            {
                var currentField = Fields[fieldId];

                reader.Position = entryPosition + currentField.Offset;
                //reader.SeekBegin(entryPosition + currentField.Offset);

                object value = 0;
                switch (currentField.DataType)
                {
                    case DataType.BitField:
                        value = reader.ReadBytes(currentField.Size);
                        break;

                    //case DataType.S8Array:
                    //    value = (sbyte[])(Array)reader.ReadBytes(currentField.Size);
                    //    break;

                    case DataType.S8:
                        value = reader.ReadSByte();
                        break;

                    case DataType.U8:
                        value = reader.ReadByte();
                        break;

                    case DataType.Float32:
                        value = reader.ReadSingle();
                        break;

                    case DataType.Int16:
                        value = reader.ReadInt16();
                        break;

                    case DataType.UInt16:
                        value = reader.ReadUInt16();
                        break;

                    case DataType.Int32:
                        value = reader.ReadInt32();
                        break;

                    case DataType.Float64:
                        value = reader.ReadDouble();
                        break;

                    case DataType.UInt32:
                    case DataType.CRC32:
                    case DataType.MMH3:
                        value = reader.ReadUInt32();
                        break;

                    case DataType.String:
                        value = reader.ReadString(currentField.Size, Encoding.UTF8);
                        break;
                }

                newEntry[fieldId] = value;
            }

            Entries.Add(newEntry);
            // Go to the next entry, important if we don't know the header
            reader.Position = entryPosition + EntrySize;
        }

        reader.Close();
        fileStream.Close();
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
            writer.Seek(8, SeekOrigin.Current);
        }


        foreach (var field in Fields)
        {
            writer.Write(field.Hash);
            writer.Write(field.Offset);
        }

        for (int currentEntry = 0; currentEntry < Entries.Count; currentEntry++)
        {

            int pos = (int) writer.BaseStream.Position;
            writer.Write(pos);

            for (int fieldId = 0; fieldId < Fields.Length; fieldId++)
            {
                var field = Fields[fieldId];
                writer.Seek(pos + field.Offset, SeekOrigin.Begin);

                var entryValue = Entries[currentEntry][fieldId];
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

                    case DataType.Float64:
                        writer.Write((double)entryValue);
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
                                string stringValue = entryValue?.ToString()?.Trim() ?? "";
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

            }

            writer.Seek(pos + EntrySize, SeekOrigin.Begin);
        }

        return stream.ToArray();
    }

    private bool disposed = false;
    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                Fields = null;
                Entries.Clear();
                Entries = null;
            }

            // Indicate that the instance has been disposed.
            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);

        GC.SuppressFinalize(this);
    }
}

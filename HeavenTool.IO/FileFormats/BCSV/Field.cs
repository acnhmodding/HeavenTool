namespace HeavenTool.IO.FileFormats.BCSV;

public class Field
{
    public DataType DataType { get; set; }
    public uint Hash { get; set; }
    public int Offset { get; set; }
    public int Size { get; set; }

    public object GetFieldDefaultValue()
    {
        return DataType switch
        {

            DataType.BitField => new byte[Size],
            DataType.U8 => (byte)0,
            DataType.S8 => (sbyte)0,
            DataType.Int16 => (short)0,
            DataType.UInt16 => (ushort)0,
            DataType.Int32 => 0,
            DataType.UInt32 or DataType.CRC32 or DataType.MMH3 => (uint)0,
            DataType.Float32 => (float)0,
            DataType.Float64 => (double)0,
            DataType.String => "",
            _ => null,
        };
    }

    public string HEX => Hash.ToString("x");

    public bool IsMissingHash => !HashManager.CRC32_Hashes.ContainsKey(Hash);
    

    private string _displayName;
    public string DisplayName { 
        get
        {
            if (_displayName == null)
            {
                // get translated name
                var translatedName = GetTranslatedNameOrNull();
                if (translatedName != null)
                {
                    // format to display name
                    if (translatedName.Contains('.'))
                        translatedName = translatedName.Split('.')[0];
                    else if (translatedName.Contains(' '))
                        translatedName = translatedName.Split(' ')[0];
                }

                _displayName = translatedName ?? HEX;
            }

            return _displayName;
        }
    }


    public string GetTranslatedNameOrHash()
    {
        return GetTranslatedNameOrNull() ?? HEX;
    }

    public string GetTranslatedNameOrNull()
    {
        return HashManager.GetHashTranslationOrNull(Hash);
    }

    public override bool Equals(object obj)
    {
        return obj is Field field && Hash == field.Hash;
    }

    public override int GetHashCode()
    {
        return Hash.GetHashCode();
    }
}

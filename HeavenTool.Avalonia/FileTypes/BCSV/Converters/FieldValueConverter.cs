using Avalonia.Data.Converters;
using HeavenTool.IO.FileFormats.BCSV;
using System;
using System.Globalization;
using System.Text;

namespace HeavenTool.Avalonia.FileTypes.BCSV.Converters;

/// <summary>Array indexers are object-typed; explicitly retain the binary field's CLR type on edit.</summary>
public sealed class FieldValueConverter(DataType type, int size) : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is byte[] bits ? System.Convert.ToHexString(bits) : value;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) throw new FormatException("A value is required.");
        switch (type)
        {
            case DataType.String:
                var text = value.ToString()!;
                if (text.Contains('\0') || Encoding.UTF8.GetByteCount(text) >= size)
                    throw new FormatException($"Use at most {size - 1} UTF-8 bytes.");
                return text;
            case DataType.Float32:
                var number = System.Convert.ToSingle(value, culture);
                if (!float.IsFinite(number)) throw new FormatException("Enter a finite number.");
                return number;
            default:
                var integer = System.Convert.ToDecimal(value, culture);
                if (integer != decimal.Truncate(integer)) throw new FormatException("Enter a whole number.");
                return type switch
                {
                    DataType.S8 => checked((sbyte)integer),
                    DataType.U8 => checked((byte)integer),
                    DataType.Int16 => checked((short)integer),
                    DataType.UInt16 => checked((ushort)integer),
                    DataType.Int32 => checked((int)integer),
                    DataType.UInt32 or DataType.CRC32 or DataType.MMH3 => checked((uint)integer),
                    _ => throw new NotSupportedException($"Cannot edit {type}.")
                };
        }
    }
}

/// <summary>A no-op edit must not re-encode legacy strings or reinterpret a hash label.</summary>
internal sealed class EditValueConverter(IValueConverter converter, object original) : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        converter.Convert(value, targetType, parameter, culture);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string text && text == System.Convert.ToString(
            converter.Convert(original, typeof(string), parameter, culture), culture))
            return original;
        return converter.ConvertBack(value, targetType, parameter, culture);
    }
}
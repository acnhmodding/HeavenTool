using Avalonia.Data.Converters;
using HeavenTool.IO;
using System;
using System.Globalization;

namespace HeavenTool.Avalonia.FileTypes.BCSV.Converters;

public class MMH3Converter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return "";

        if (value is uint uintValue)
        {
            if (HashManager.MMH3_Hashes.TryGetValue(uintValue, out string? result) && result != null)
                return result;

            return $"0x{uintValue:X8}";
        }

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.Parse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return s.ToMurmur();
        }

        return value;
    }
}
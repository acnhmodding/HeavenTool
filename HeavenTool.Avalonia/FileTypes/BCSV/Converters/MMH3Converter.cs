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

            return uintValue.ToString("x");
        }

        return value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
            return s.ToMurmur();

        return value;
    }
}

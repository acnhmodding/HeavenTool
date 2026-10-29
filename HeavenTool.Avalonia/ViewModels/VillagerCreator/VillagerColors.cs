using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>
/// The colors a villager can like, in the order of the game's color enum (Color1 and Color2 in NmlNpcParam).
/// The game only names them, so each swatch is a typical shade of its color.
/// </summary>
public static class VillagerColors
{
    private static readonly (string Name, IBrush? Swatch)[] Colors =
    [
        ("Orange", Solid(0xF0, 0x8C, 0x28)),
        ("Red", Solid(0xD9, 0x3A, 0x3A)),
        ("Pink", Solid(0xF4, 0x8F, 0xB1)),
        ("Purple", Solid(0x9C, 0x6A, 0xDE)),
        ("Blue", Solid(0x3B, 0x6F, 0xD8)),
        ("Light blue", Solid(0x6E, 0xC6, 0xF0)),
        ("Green", Solid(0x4C, 0xAF, 0x50)),
        ("Yellow", Solid(0xF5, 0xD3, 0x3B)),
        ("Beige", Solid(0xE8, 0xD5, 0xB0)),
        ("Brown", Solid(0x8B, 0x5A, 0x2B)),
        ("Black", Solid(0x1E, 0x1E, 0x1E)),
        ("Gray", Solid(0x9E, 0x9E, 0x9E)),
        ("White", Solid(0xF5, 0xF5, 0xF5)),
        ("Colorful", Rainbow()),
        ("None", null),
    ];

    /// <summary>The option for a color's NookKit value.</summary>
    public static (string Text, IBrush? Swatch) Describe(string json) =>
        int.TryParse(json, out var index) && index >= 0 && index < Colors.Length ? Colors[index] : ($"Unknown ({json})", null);

    /// <summary>A brush for one of the game's RGBA colors, with channels from 0 to 1.</summary>
    public static IBrush Brush(Vector4 rgba)
    {
        static byte Channel(float value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
        return new ImmutableSolidColorBrush(Color.FromArgb(Channel(rgba.W), Channel(rgba.X), Channel(rgba.Y), Channel(rgba.Z)));
    }

    private static IBrush Solid(byte r, byte g, byte b) => new ImmutableSolidColorBrush(Color.FromRgb(r, g, b));

    private static IBrush Rainbow() => new ImmutableLinearGradientBrush(
        new List<ImmutableGradientStop>
        {
            new(0, Color.FromRgb(0xD9, 0x3A, 0x3A)),
            new(.33, Color.FromRgb(0xF5, 0xD3, 0x3B)),
            new(.66, Color.FromRgb(0x4C, 0xAF, 0x50)),
            new(1, Color.FromRgb(0x3B, 0x6F, 0xD8)),
        },
        startPoint: new RelativePoint(0, 0, RelativeUnit.Relative),
        endPoint: new RelativePoint(1, 1, RelativeUnit.Relative));
}

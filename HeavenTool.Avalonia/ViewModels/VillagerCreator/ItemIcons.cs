using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using HeavenTool.IO.FileFormats.BFRES;
using HeavenTool.ModManager.Villagers;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary>Item icons from the romfs' layout icon archives, decoded once each.</summary>
public static class ItemIcons
{
    // Icons show at most this many pixels wide, so a smaller mip saves memory when the picker lists many.
    private const int Size = 64;

    private static readonly ConcurrentDictionary<string, Task<Bitmap?>> Icons = new();

    /// <summary>An item's icon, or null when the romfs has none or it can't be read.</summary>
    public static Task<Bitmap?> Load(VillagerGameData data, int id) =>
        data.ItemIconPath(id) is { } path ? Icons.GetOrAdd(path, file => Task.Run(() => Read(file))) : Task.FromResult<Bitmap?>(null);

    private static Bitmap? Read(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            // Icons are decoded to RGBA8; anything else is a texture this preview can't show.
            if (BfresFileReader.LoadTextures(file).FirstOrDefault() is not { } texture || texture.Format >> 8 != 0x0b) return null;
            int level = 0;
            while (level + 1 < texture.Mips.Count && texture.Width >> (level + 1) >= Size) level++;
            int width = Math.Max(1, texture.Width >> level), height = Math.Max(1, texture.Height >> level);
            var pixels = GCHandle.Alloc(texture.Mips[level].ToArray(), GCHandleType.Pinned);
            try
            {
                return new Bitmap(PixelFormat.Rgba8888, AlphaFormat.Unpremul, pixels.AddrOfPinnedObject(),
                    new PixelSize(width, height), new Vector(96, 96), width * 4);
            }
            finally { pixels.Free(); }
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException)
        {
            return null;
        }
    }
}

using System.Text;

namespace HeavenTool.IO.FileFormats.ResourceSizeTable;

/// <summary>
/// The single runtime pool that BFRES setup (0x71038FB8C0) requests for a resource's
/// models, materials, shapes, meshes, BNTX texture references and animations.
/// </summary>
internal static class BfresRuntimePool
{
    public static long Bytes(int models, int materials, int shapes, int meshes, int textures, long animations) =>
        checked(models * 0x430L + materials * 0x198L + shapes * (0x1120L + 0xb0) + meshes * 0x260L + textures * 0x10L + animations);

    /// <summary>
    /// Counts the textures of the external files, which must all be BNTX v4.1 archives
    /// stored before the relocation table.
    /// </summary>
    public static int TextureCount(ResourceReader bfres)
    {
        int files = bfres.U16(0xec), array = bfres.Pointer(0xb8);
        if ((files == 0) != (array == 0) || (files == 0 && bfres.U64(0xc0) != 0))
            throw new InvalidDataException("External file arrays do not match their count.");

        uint relocation = bfres.U32(24);
        int textures = 0;
        for (int i = 0; i < files; i++)
        {
            int at = bfres.Pointer(array + i * 16L), length = bfres.Pointer(array + i * 16L + 8);
            if ((long)at + length > relocation) throw new InvalidDataException("External file overlaps the relocation table.");
            var bntx = bfres.Slice(at, length);
            if (bntx.Length < 0x58 || !bntx.Magic(0, "BNTX\0\0\0\0"u8) || bntx.U32(8) != 0x00040100 || bntx.U16(12) != 0xfeff ||
                bntx.U16(20) != 0 || bntx.U32(28) != length || !bntx.Magic(32, "NX  "u8))
                throw new NotSupportedException("External files must be BNTX v4.1 textures.");

            uint count = bntx.U32(36);
            int infos = bntx.Pointer(40);
            if (count == 0) throw new NotSupportedException("BNTX has no textures.");
            bntx.Range(infos, count * 8L);
            for (uint texture = 0; texture < count; texture++)
            {
                int info = bntx.Pointer(infos + texture * 8L);
                bntx.Range(info, 0xa0);
                bntx.RequireMagic(info, "BRTI"u8);
            }
            textures = checked(textures + (int)count);
        }
        return textures;
    }

    /// <summary>
    /// 0x18 bytes per skeletal, material, bone-visibility and shape animation, plus the
    /// 8-aligned normalized names of material and bone-visibility animations. Scene
    /// animations are not counted.
    /// </summary>
    public static long AnimationBytes(ResourceReader bfres, out long nameBytes)
    {
        long animations = 0;
        for (int kind = 0; kind < 4; kind++)
        {
            int count = bfres.U16(0xe2 + kind * 2), array = 0x58 + kind * 0x10;
            if ((count == 0) != (bfres.U64(array) == 0) || (count == 0) != (bfres.U64(array + 8) == 0))
                throw new InvalidDataException("Animation arrays do not match their count.");
            animations += count;
        }

        nameBytes = 0;
        foreach (var (array, stride, count) in new[] { (0x68, 0x70, bfres.U16(0xe4)), (0x78, 0x60, bfres.U16(0xe6)) })
        {
            int start = bfres.Pointer(array);
            bfres.Range(start, (long)count * stride);
            for (int i = 0; i < count; i++)
            {
                int name = bfres.Pointer(start + i * stride + 8);
                if (name == 0) throw new InvalidDataException("Animation has no name.");
                nameBytes += NormalizedAnimationNameBytes(bfres.Name(name));
            }
        }
        nameBytes = ExpHeapPlanner.AlignUp(nameBytes, 8);
        return animations * 0x18 + nameBytes;
    }

    // 0x71038B0F60 recognizes these three-byte suffixes, removes an optional
    // separator, and copies into a 1024-byte temporary. 0x71038FB8C0 reserves
    // the normalized name plus NUL even when its copy-name flag is disabled.
    internal static int NormalizedAnimationNameBytes(string name)
    {
        if (name.Length < 3 || name[^3..] is not ("fsp" or "fcl" or "fts" or "ftp" or "fvb" or "fvm")) return 0;
        int end = name.Length - 3;
        if (end > 0 && name[end - 1] is '_' or '.') end--;
        return Math.Min(Encoding.UTF8.GetByteCount(name.AsSpan(0, end)), 1023) + 1;
    }
}

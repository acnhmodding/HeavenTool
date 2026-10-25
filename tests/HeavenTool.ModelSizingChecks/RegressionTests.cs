using System.Buffers.Binary;
using System.Text;
using AeonSake.NintendoTools.Compression.Zstd;
using HeavenTool.IO.FileFormats.ResourceSizeTable;

internal static class RegressionTests
{
    // The model archive chain (8360) plus the BFRES resource (808), its two-entry shader
    // archive list (48) and the runtime pool's block header (32).
    private const int ModelBfresOverhead = 8360 + 808 + 48 + 32;

    public static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
        var archive = TextureArchive(3);
        Check(ModelResourceSizeCalculator.TryCalculate(archive, out uint size) && size == 0x5000 + ModelBfresOverhead + 3 * 16, "Texture runtime size");
        var single = TextureArchive(1);
        Check(ModelResourceSizeCalculator.TryCalculate(single, out size) && size % 32 == 16, "Do not round away the exact allocation");
        var exporter = (byte[])archive.Clone();
        W64(exporter, 0x2000 + 0xb8, 0x140); W64(exporter, 0x2000 + 0x140, 0x1000); W64(exporter, 0x2000 + 0x148, 0x1800);
        W64(exporter, 0x2000 + 0xa8, 0x200); W64(exporter, 0x2000 + 0xb0, 0xf0);
        W32(exporter, 0x2000 + 0xf0, 0x24); W32(exporter, 0x2000 + 0xf4, 0); W64(exporter, 0x2000 + 0xf8, 0x200); exporter[0x2000 + 0xef] = 1;
        Check(ModelResourceSizeCalculator.TryCalculate(exporter, out uint exporterSize) && exporterSize == size + 32, "Empty exporter geometry section has no geometry allocation");
        W32(exporter, 0x2000 + 0xf4, 4096);
        Check(!ModelResourceSizeCalculator.TryCalculate(exporter, out _), "Nonempty geometry buffer falls back");
        W32(exporter, 0x2000 + 0xf4, 0); exporter[0x2000 + 0x200] = 1;
        Check(!ModelResourceSizeCalculator.TryCalculate(exporter, out _), "Initialized geometry pool falls back");

        // Unsupported layouts must never acquire a disk-size estimate, including in Debug.
        void Reject(string name, Action<byte[]> mutate) {
            byte[] changed = (byte[])archive.Clone(); mutate(changed);
            Check(!ModelResourceSizeCalculator.TryCalculate(changed, out _), name);
        }
        Reject("Geometry falls back", b => W16(b, 0x2000 + 0xdc, 1));
        Reject("Animation count without its array falls back", b => W16(b, 0x2000 + 0xe4, 1));
        Reject("Scene animations fall back", b => { W16(b, 0x2000 + 0xea, 1); W64(b, 0x2000 + 0x98, 0x400); W64(b, 0x2000 + 0xa0, 0x500); });

        // Two material animations: 0x18 each, and "Spin_fts" normalizes to "Spin" (5 bytes, 8 aligned).
        var animated = WithMaterialAnimations((byte[])archive.Clone(), "Spin_fts", "Idle");
        Check(ModelResourceSizeCalculator.TryCalculate(animated, out uint animatedSize) && animatedSize == 0x5000 + ModelBfresOverhead + 3 * 16 + 2 * 24 + 8,
            "Texture and animation runtime size");
        W16(animated, 0x2000 + 0xec, 0); W64(animated, 0x2000 + 0xb8, 0);
        Check(ModelResourceSizeCalculator.TryCalculate(animated, out animatedSize) && animatedSize == 0x5000 + ModelBfresOverhead + 2 * 24 + 8,
            "Animation-only runtime size");
        W64(animated, 0x2000 + 0x400 + 8, 0x4f00);
        Check(!ModelResourceSizeCalculator.TryCalculate(animated, out _), "Out-of-range animation name falls back");
        byte[] empty = (byte[])archive.Clone(); W16(empty, 0x2000 + 0xec, 0); W64(empty, 0x2000 + 0xb8, 0);
        Check(!ModelResourceSizeCalculator.TryCalculate(empty, out _), "Empty BFRES falls back");
        Reject("Version 10 falls back", b => W32(b, 0x2000 + 8, 0x000a0000));
        Reject("External GPU flags fall back", b => b[0x2000 + 0xee] = 8);
        Reject("Multiple SARC members fall back", b => W16(b, 26, 2));
        Reject("Mixed external resources fall back", b => W16(b, 0x2000 + 0xec, 2));
        Reject("Unexpected archive alignment falls back", b => W32(b, 12, 0x1000));
        Reject("Wrong endianness falls back", b => W16(b, 0x2000 + 12, 0xfffe));
        Reject("File length mismatch falls back", b => W32(b, 0x2000 + 28, 1));
        Reject("Invalid relocation boundary falls back", b => W32(b, 0x2000 + 24, uint.MaxValue));
        Reject("Missing relocation table falls back", b => W32(b, 0x2000 + 0x2800, 0));
        Reject("Out-of-range external pointer falls back", b => W64(b, 0x2000 + 0xb8, ulong.MaxValue));
        Reject("Oversized embedded resource falls back", b => W64(b, 0x2000 + 0xf8, ulong.MaxValue));
        Reject("Missing BNTX falls back", b => b[0x3000] = 0);
        Reject("New BNTX version falls back", b => W32(b, 0x3000 + 8, 0x00050000));
        Reject("No textures falls back", b => W32(b, 0x3000 + 36, 0));
        Reject("Invalid texture count falls back", b => W32(b, 0x3000 + 36, uint.MaxValue));
        Reject("Invalid texture-array pointer falls back", b => W64(b, 0x3000 + 40, ulong.MaxValue));
        Reject("Invalid texture-info pointer falls back", b => W64(b, 0x3000 + 0x80, ulong.MaxValue));
        Reject("Invalid texture-info magic falls back", b => W32(b, 0x3000 + 0x100, 0));
        for (int length = 0; length < archive.Length; length += 97)
            Check(!ModelResourceSizeCalculator.TryCalculate(archive.AsSpan(0, length), out _), "Truncated archive");
        var random = new Random(4281);
        for (int i = 0; i < 1000; i++) {
            byte[] b = new byte[random.Next(0, 1024)]; random.NextBytes(b);
            Check(!ModelResourceSizeCalculator.TryCalculate(b, out _), "Random invalid archive");
        }

        string temp = Path.Combine(Path.GetTempPath(), "HeavenTool.ModelSizingChecks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        string raw = Path.Combine(temp, "texture.Nin_NX_NVN"), compressed = raw + ".zs", geometry = Path.Combine(temp, "geometry.Nin_NX_NVN.zs");
        try {
            File.WriteAllBytes(raw, archive);
            using (var source = new MemoryStream(archive))
            using (var target = File.Create(compressed)) new ZstdCompressor().Compress(source, target);
            Check(ResourceSizeTable.GetFileSize(raw, "Model/Texture.Nin_NX_NVN") == 0x5000 + ModelBfresOverhead + 48, "Raw integration");
            Check(ResourceSizeTable.GetFileSize(compressed, "Model/Texture.Nin_NX_NVN") == 0x5000 + ModelBfresOverhead + 48, "Zstd integration");
            // Plain loads: 32-rounded data plus the overhead of each traced allocation chain.
            byte[] plain = new byte[100];
            string bars = Path.Combine(temp, "plain.bars"), flow = Path.Combine(temp, "plain.bfevfl"), sequence = Path.Combine(temp, "plain.root.asb");
            string packed = Path.Combine(temp, "plain.zs");
            File.WriteAllBytes(bars, plain); File.WriteAllBytes(flow, plain); File.WriteAllBytes(sequence, plain);
            using (var source = new MemoryStream(plain))
            using (var target = File.Create(packed)) new ZstdCompressor().Compress(source, target);
            Check(ResourceSizeTable.GetFileSize(packed, "Message/Plain.sarc") == 128 + 512, "Message archive chain");
            Check(ResourceSizeTable.GetFileSize(packed, "Layout/Plain.blarc") == 128 + 8192, "Layout archive chain");
            Check(ResourceSizeTable.GetFileSize(bars, "Sound/Resource/Plain.bars") == 128 + 712, "Sound chain");
            Check(ResourceSizeTable.GetFileSize(flow, "EventFlow/Plain.bfevfl") == 128 + 416, "EventFlow chain");
            Check(ResourceSizeTable.GetFileSize(packed, "Font/Plain.sarc") == 128 + 8192, "Font archive chain");
            Check(ResourceSizeTable.GetFileSize(sequence, "AnimationSequence/Plain.root.asb") == 128 + 416, "Animation sequence budget");
            string collision = Path.Combine(temp, "plain_pbc.Nin_NX_NVN.zs");
            using (var source = new MemoryStream(Archive("colBase.pbc", CollisionGrid())))
            using (var target = File.Create(collision)) new ZstdCompressor().Compress(source, target);
            Check(ResourceSizeTable.GetFileSize(collision, "Model/Plain_pbc.Nin_NX_NVN") == 0x100 + 8360, "Collision archive chain");
            // An empty phive still allocates its 0x28-byte holder (72) and its own archive resource (120).
            string physics = Path.Combine(temp, "physics.Nin_NX_NVN.zs");
            using (var source = new MemoryStream(Archive("output.phive", Archive(null, []))))
            using (var target = File.Create(physics)) new ZstdCompressor().Compress(source, target);
            Check(ResourceSizeTable.GetFileSize(physics, "Model/Physics.Nin_NX_NVN") == 0x180 + 8360 + 72 + 120, "Physics archive chain");
            // Fish bones add the 0x3B8 object (984), the 0x30 list (80) and three 0x28 bones (160).
            using (var source = new MemoryStream(Archive("output.phive", Archive("output.bphfbc", FishBones(3)))))
            using (var target = File.Create(physics)) new ZstdCompressor().Compress(source, target);
            Check(ResourceSizeTable.GetFileSize(physics, "Model/Physics.Nin_NX_NVN") == 0x180 + 8360 + 72 + 120 + 984 + 80 + 160, "Fish-bone physics chain");
            using (var source = new MemoryStream(Archive("output.phive", Archive("output.bphcw", []))))
            using (var target = File.Create(physics)) new ZstdCompressor().Compress(source, target);
            Check(ResourceSizeTable.GetFileSize(physics, "Model/Physics.Nin_NX_NVN") == -1, "Unmodelled physics keeps the fallback");
            byte[] unsupported = (byte[])archive.Clone(); W16(unsupported, 0x2000 + 0xdc, 1);
            using (var source = new MemoryStream(unsupported))
            using (var target = File.Create(geometry)) new ZstdCompressor().Compress(source, target);
            Check(ResourceSizeTable.GetFileSize(geometry, "Model/Geometry.Nin_NX_NVN") == -1, "No Debug disk-size regression");
            // Also exercise the common updater through a real RomFS-shaped directory.
            string models = Path.Combine(temp, "Model"); Directory.CreateDirectory(models);
            File.Copy(compressed, Path.Combine(models, "Texture.Nin_NX_NVN.zs"));
            File.Copy(geometry, Path.Combine(models, "Geometry.Nin_NX_NVN.zs"));
            byte[] emptyTable = new byte[12]; "RSTC"u8.CopyTo(emptyTable);
            using var table = new ResourceSizeTable(emptyTable);
            table.AddEntry(new ResourceSizeTable.ResourceTableEntry("Model/Geometry.Nin_NX_NVN", 16, 0, false));
            ResourceSizeTablePatcher.CalculatePatch(temp).ApplyTo(table);
            Check(!table.Dictionary.ContainsKey("Model/Geometry.Nin_NX_NVN"), "Updater removes unsupported allocation");
            Check(table.Dictionary["Model/Texture.Nin_NX_NVN"].FileSize == 0x5000 + ModelBfresOverhead + 48, "Updater adds calculated allocation");
        }
        finally {
            // Every file is a disposable fixture under the unique directory created above.
            if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(temp)), Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Refusing to clean fixtures outside the temporary directory");
            foreach (string path in Directory.EnumerateFiles(temp, "*", SearchOption.AllDirectories)) File.Delete(path);
            if (Directory.Exists(Path.Combine(temp, "Model"))) Directory.Delete(Path.Combine(temp, "Model"));
            Directory.Delete(temp);
        }
        Console.WriteLine($"Passed {checks} model sizing regression checks.");
    }

    private static byte[] TextureArchive(uint textures)
    {
        byte[] b = new byte[0x5000];
        "SARC"u8.CopyTo(b); W16(b, 4, 0x14); W16(b, 6, 0xfeff); W32(b, 8, (uint)b.Length);
        W32(b, 12, 0x2000); W16(b, 16, 0x100); "SFAT"u8.CopyTo(b.AsSpan(20)); W16(b, 24, 12); W16(b, 26, 1); W32(b, 44, 0x3000);
        int f = 0x2000; "FRES    "u8.CopyTo(b.AsSpan(f)); W32(b, f + 8, 0x00090000); W16(b, f + 12, 0xfeff);
        W32(b, f + 24, 0x2800); W32(b, f + 28, 0x3000); W64(b, f + 0xb8, 0xf0); W16(b, f + 0xec, 1);
        W64(b, f + 0xf0, 0x1000); W64(b, f + 0xf8, 0x1800); "_RLT"u8.CopyTo(b.AsSpan(f + 0x2800));
        int t = f + 0x1000; "BNTX\0\0\0\0"u8.CopyTo(b.AsSpan(t)); W32(b, t + 8, 0x00040100); W16(b, t + 12, 0xfeff);
        W32(b, t + 28, 0x1800); "NX  "u8.CopyTo(b.AsSpan(t + 32)); W32(b, t + 36, textures); W64(b, t + 40, 0x80);
        for (uint i = 0; i < textures; i++) { W64(b, t + 0x80 + (int)i * 8, 0x100 + i * 0x100); "BRTI"u8.CopyTo(b.AsSpan(t + 0x100 + (int)i * 0x100)); }
        return b;
    }
    // Adds material animations (0x70-byte entries at BFRES 0x400, names from 0x600) to a texture archive.
    private static byte[] WithMaterialAnimations(byte[] b, params string[] names)
    {
        int f = 0x2000, name = f + 0x600;
        W16(b, f + 0xe4, (ushort)names.Length); W64(b, f + 0x68, 0x400); W64(b, f + 0x70, 0x380);
        for (int i = 0; i < names.Length; i++)
        {
            W64(b, f + 0x400 + i * 0x70 + 8, (ulong)(name - f));
            W16(b, name, (ushort)names[i].Length); Encoding.ASCII.GetBytes(names[i]).CopyTo(b.AsSpan(name + 2));
            name += names[i].Length + 3 + 7 & ~7;
        }
        return b;
    }
    // A SARC holding one named member at data offset 0x80, or no members when name is null.
    private static byte[] Archive(string? name, byte[] content)
    {
        byte[] b = new byte[0x80 + Math.Max(0x80, content.Length + 0x7f & ~0x7f)];
        "SARC"u8.CopyTo(b); W16(b, 4, 0x14); W16(b, 6, 0xfeff); W32(b, 8, (uint)b.Length);
        W32(b, 12, 0x80); W16(b, 16, 0x100); "SFAT"u8.CopyTo(b.AsSpan(20)); W16(b, 24, 12);
        "SFNT"u8.CopyTo(b.AsSpan(32)); W16(b, 36, 8);
        if (name == null) return b;
        W16(b, 26, 1); W32(b, 36, 0x01000000); W32(b, 40, 0); W32(b, 44, (uint)content.Length);
        "SFNT"u8.CopyTo(b.AsSpan(48)); W16(b, 52, 8); Encoding.ASCII.GetBytes(name).CopyTo(b.AsSpan(56));
        content.CopyTo(b.AsSpan(0x80));
        return b;
    }
    // An AAMP v2 file whose root list has three objects; the first sets mBoneNum.
    private static byte[] FishBones(int bones)
    {
        byte[] b = new byte[0x64];
        "AAMP"u8.CopyTo(b); W32(b, 4, 2); W32(b, 8, 3); W32(b, 12, (uint)b.Length); W32(b, 0x14, 4); "xml"u8.CopyTo(b.AsSpan(0x30));
        W16(b, 0x34 + 8, 3); W16(b, 0x34 + 10, 3);
        W16(b, 0x40 + 4, 6); W16(b, 0x40 + 6, 1);
        W32(b, 0x58, 0x354f4d5d); W32(b, 0x5c, 2u << 24 | 2); W32(b, 0x60, (uint)bones);
        return b;
    }
    // A 1x0 PBC grid.
    private static byte[] CollisionGrid()
    {
        byte[] b = new byte[20]; W32(b, 4, 1);
        return b;
    }
    private static void W16(byte[] b, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(offset), value);
    private static void W32(byte[] b, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(offset), value);
    private static void W64(byte[] b, int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(offset), value);
}

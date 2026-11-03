#nullable enable
using AeonSake.NintendoTools.Compression.Yaz0;
using AeonSake.NintendoTools.FileFormats.Sarc;
using HeavenTool.IO.FileFormats.BFRES;
using System.Buffers.Binary;

namespace HeavenTool.IO.AcnhEnvironment;

/// <summary>
/// agl::pfx::ColorCorrection settings (.baglccr) packed the way 710382b4a0 uploads them for the
/// color_correction_map shader, which bakes them into the 8³ grading volume ToneMapping samples.
/// </summary>
public sealed class AcnhColorCorrection
{
    private const int Slots = 12, Rows = 40, CurveCount = 4, HeaderRows = 12;
    /// <summary>12 (type, first row) headers, 40 data rows, then the filter count; 0x344 bytes rounded up to a row.</summary>
    public const int FilterBlockSize = (HeaderRows + Rows + 1) * 16;

    /// <summary>The packed filter block, or null when the settings leave color correction off.</summary>
    public byte[]? Filters { get; }

    private AcnhColorCorrection(byte[]? filters) => Filters = filters;

    public static SwitchProcessShaderProgram LoadMapProgram(string romfs)
    {
        var resources = OpenArchive(File.ReadAllBytes(Path.Combine(romfs, "System", "Agl", "agl_resource.Nin_NX_NVN.release.szs")));
        using var technique = new MemoryStream(Entry(resources, "agl_technique_pfx.sharcb"), false);
        return SwitchProcessShaderArchive.Read(technique).Programs.SingleOrDefault(p => p.Name == "color_correction_map")
            ?? throw new InvalidDataException("agl_technique_pfx is missing color_correction_map.");
    }

    /// <summary>Reads one GameEnv setting by its lower-case name, such as envroom.</summary>
    public static AcnhColorCorrection Load(string romfs, string name)
    {
        var gameEnv = OpenArchive(File.ReadAllBytes(Path.Combine(romfs, "Env", "GameEnv.szs")));
        return Read(Entry(OpenArchive(Entry(gameEnv, "GameEnv.genvb")), $"{name}.baglccr"));
    }

    public static AcnhColorCorrection Read(byte[] data)
    {
        var file = new Aamp(data);
        int root = 0x30 + file.Int(0x14);
        var objects = file.Objects(root);
        var main = file.Parameters(objects.GetValueOrDefault(Crc("color_correction"), -1));
        if (file.Bool(main, "enable") == false) return new(null);

        var filters = new byte[FilterBlockSize];
        int count = 0, row = 0;
        for (int slot = 0; slot < Slots; slot++)
        {
            int index = file.Int(main, $"data_index{slot}");
            if (index == -1) continue;
            if (index < 0 || index >= Slots) throw new InvalidDataException("Invalid color-correction filter index.");
            var filter = file.Parameters(objects.GetValueOrDefault(Crc($"data{index}"), -1));
            int parameter = file.Int(filter, "param_index");
            if (parameter == -1) continue;
            int type = file.Int(filter, "type");

            Header(filters, count++, type, row);
            switch (type)
            {
                // A single parameter vector.
                case <= 5 or 9:
                    Row(filters, row++, file.Vector4(main, $"param{(parameter < Rows ? parameter : 0)}"));
                    break;
                // Four curves sampled at eight even steps, with the last step repeated for interpolation.
                case 6:
                    var curves = file.Curves(main, $"curve{(parameter < CurveCount ? parameter : 0)}");
                    for (int step = 0; step < 8; step++)
                    {
                        float t = step / 7f;
                        Row(filters, row + step, new(curves[0].Evaluate(t), curves[1].Evaluate(t), curves[2].Evaluate(t), curves[3].Evaluate(t)));
                    }
                    filters.AsSpan((HeaderRows + row + 7) * 16, 16).CopyTo(filters.AsSpan((HeaderRows + row + 8) * 16));
                    row += 9;
                    break;
                default:
                    throw new NotSupportedException($"Color-correction filter type {type} is not recovered.");
            }
            if (row > Rows) throw new InvalidDataException("Color-correction filters exceed their data rows.");
        }
        BinaryPrimitives.WriteInt32LittleEndian(filters.AsSpan((HeaderRows + Rows) * 16), count);
        return new(filters);
    }

    private static void Header(byte[] filters, int index, int type, int row)
    {
        BinaryPrimitives.WriteInt32LittleEndian(filters.AsSpan(index * 16), type);
        BinaryPrimitives.WriteInt32LittleEndian(filters.AsSpan(index * 16 + 4), row);
    }

    private static void Row(byte[] filters, int row, System.Numerics.Vector4 value)
    {
        var span = filters.AsSpan((HeaderRows + row) * 16);
        BinaryPrimitives.WriteSingleLittleEndian(span, value.X);
        BinaryPrimitives.WriteSingleLittleEndian(span[4..], value.Y);
        BinaryPrimitives.WriteSingleLittleEndian(span[8..], value.Z);
        BinaryPrimitives.WriteSingleLittleEndian(span[12..], value.W);
    }

    private static uint Crc(string name) => name.ToCRC32();

    private static SarcFile OpenArchive(byte[] data)
    {
        if (data.AsSpan().StartsWith("Yaz0"u8))
        {
            using var input = new MemoryStream(data, false);
            using var output = new MemoryStream();
            new Yaz0Decompressor().Decompress(input, output);
            data = output.ToArray();
        }
        using var stream = new MemoryStream(data, false);
        return new SarcFileReader().Read(stream);
    }

    private static byte[] Entry(SarcFile archive, string name) =>
        archive.Files.SingleOrDefault(file => file.Name == name)?.Data ?? throw new InvalidDataException($"Missing {name}.");

    /// <summary>A sead curve; only the Hermit2D (x, y, tangent) kind is recovered.</summary>
    private sealed class Curve(int used, int type, float[] values)
    {
        // sead's Hermit2D (71037e8910): flat outside the keys, unscaled tangents within a segment.
        public float Evaluate(float x)
        {
            if (type != 7) throw new NotSupportedException($"Color-correction curve type {type} is not recovered.");
            int keys = used / 3;
            if (keys < 1) throw new InvalidDataException("Empty color-correction curve.");
            if (x <= values[0]) return values[1];
            if (x >= values[(keys - 1) * 3]) return values[(keys - 1) * 3 + 1];
            for (int k = 0; k + 1 < keys; k++)
            {
                int a = k * 3, b = a + 3;
                if (x >= values[b]) continue;
                float t = (x - values[a]) / (values[b] - values[a]), t2 = t * t, t3 = t2 * t;
                return (2 * t3 - 3 * t2 + 1) * values[a + 1] + (t3 - 2 * t2 + t) * values[a + 2]
                    + (3 * t2 - 2 * t3) * values[b + 1] + (t3 - t2) * values[b + 2];
            }
            return 0;
        }
    }

    // AAMP v2: offsets count 4-byte words from the entry that holds them; parameters are looked up by CRC32 name.
    private sealed class Aamp(byte[] data)
    {
        public int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Span(offset, 4));

        public Dictionary<uint, int> Objects(int list)
        {
            int first = list + BinaryPrimitives.ReadUInt16LittleEndian(Span(list + 8, 2)) * 4, count = BinaryPrimitives.ReadUInt16LittleEndian(Span(list + 10, 2));
            var objects = new Dictionary<uint, int>();
            for (int i = 0; i < count; i++) objects[(uint)Int(first + i * 8)] = first + i * 8;
            return objects;
        }

        public Dictionary<uint, (int Type, int Data)> Parameters(int obj)
        {
            if (obj < 0) throw new InvalidDataException("Missing color-correction object.");
            int first = obj + BinaryPrimitives.ReadUInt16LittleEndian(Span(obj + 4, 2)) * 4, count = BinaryPrimitives.ReadUInt16LittleEndian(Span(obj + 6, 2));
            var parameters = new Dictionary<uint, (int, int)>();
            for (int i = 0; i < count; i++)
            {
                int entry = first + i * 8;
                uint packed = (uint)Int(entry + 4);
                parameters[(uint)Int(entry)] = ((int)(packed >> 24), entry + (int)(packed & 0xFFFFFF) * 4);
            }
            return parameters;
        }

        private int Data(Dictionary<uint, (int Type, int Data)> p, string name, int type)
        {
            if (!p.TryGetValue(Crc(name), out var value)) throw new InvalidDataException($"Missing color-correction parameter {name}.");
            if (value.Type != type) throw new InvalidDataException($"Unexpected type for color-correction parameter {name}.");
            return value.Data;
        }

        public bool Bool(Dictionary<uint, (int Type, int Data)> p, string name) => Span(Data(p, name, 0), 1)[0] != 0;
        public int Int(Dictionary<uint, (int Type, int Data)> p, string name) => Int(Data(p, name, 2));

        public System.Numerics.Vector4 Vector4(Dictionary<uint, (int Type, int Data)> p, string name)
        {
            int at = Data(p, name, 5);
            return new(Float(at), Float(at + 4), Float(at + 8), Float(at + 12));
        }

        // A curve4 parameter: four curves of (u32 used, u32 type, 30 floats).
        public Curve[] Curves(Dictionary<uint, (int Type, int Data)> p, string name)
        {
            int at = Data(p, name, 12);
            return [.. Enumerable.Range(0, CurveCount).Select(c =>
            {
                int curve = at + c * 0x80, used = Int(curve);
                if (used < 0 || used > 30) throw new InvalidDataException("Invalid color-correction curve size.");
                return new Curve(used, Int(curve + 4), [.. Enumerable.Range(0, 30).Select(i => Float(curve + 8 + i * 4))]);
            })];
        }

        private float Float(int offset) => BinaryPrimitives.ReadSingleLittleEndian(Span(offset, 4));

        private ReadOnlySpan<byte> Span(int offset, int length) =>
            offset >= 0 && length <= data.Length - offset ? data.AsSpan(offset, length) : throw new InvalidDataException("Invalid AAMP offset.");
    }
}

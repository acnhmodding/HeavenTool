using System.Globalization;
using System.Text;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.IO.FileFormats.BFRES;

public sealed record SwitchShaderOption(string Name, IReadOnlyList<string> Choices, int DefaultChoice, int Word, int Shift, uint Mask);
public sealed record SwitchShaderStage(ReadOnlyMemory<byte> Control, ReadOnlyMemory<byte> Binary)
{
    public ReadOnlyMemory<byte> MaxwellCode => Binary.Length >= 128 ? Binary[48..] : throw new InvalidDataException("Truncated NVN shader code.");
}
public sealed record ShaderResourceLocation(string Name, int Vertex, int Fragment);
public sealed record ShaderAttributeLocation(string Name, int Location);
public sealed record ShaderUniform(string Name, int Offset);
public sealed record ShaderUniformBlock(string Name, byte Type, int Size, IReadOnlyList<ShaderUniform> Uniforms, ReadOnlyMemory<byte> Defaults);
public sealed record SwitchShaderProgram(int Index, SwitchShaderStage? Vertex, SwitchShaderStage? Fragment,
    IReadOnlyList<ShaderResourceLocation> Buffers, IReadOnlyList<ShaderResourceLocation> Samplers);

// A shading model of an ACNH Switch BFSHA v8 archive. Everything except the compiled programs is read up front.
public sealed partial class SwitchShadingModel
{
    private readonly SwitchShaderArchive owner;
    private readonly long programArray, bnshBase;
    private readonly int keyWords, staticKeyWords, staticOptionCount, materialBlock;
    // Program keys, keyWords per program: the static option words first, then the dynamic ones.
    private readonly uint[] keys;
    private readonly bool staticOptionBindings;
    private readonly string[] samplerNames, storageNames;
    public string Name { get; }
    public int ProgramCount { get; }
    public IReadOnlyList<SwitchShaderOption> Options { get; }
    public IReadOnlyList<ShaderAttributeLocation> Attributes { get; }
    public IReadOnlyList<ShaderUniformBlock> UniformBlocks { get; }

    internal SwitchShadingModel(SwitchShaderArchive owner, BinaryReader reader, long at)
    {
        this.owner = owner;
        Name = reader.ReadNameAt(at);
        programArray = reader.ReadPointerAt(at + 136);
        bnshBase = reader.ReadPointerAt(at + 168);
        if (reader.ReadStringAt(bnshBase, 4) != "BNSH") throw new InvalidDataException("Expected a BNSH shader container.");
        ProgramCount = reader.ReadUInt16At(at + 232);
        staticKeyWords = reader.ReadByteAt(at + 234);
        keyWords = staticKeyWords + reader.ReadByteAt(at + 235);
        keys = reader.ReadUInt32ArrayAt(reader.ReadPointerAt(at + 144), ProgramCount * keyWords);
        staticOptionCount = reader.ReadUInt16At(at + 228);
        materialBlock = reader.ReadByteAt(at + 240);

        long staticOptions = reader.ReadPointerAt(at + 8);
        staticOptionBindings = Enumerable.Range(0, staticOptionCount).Any(i => (reader.ReadByteAt(staticOptions + i * 40 + 28) & 1) != 0);
        Options = [.. ReadOptions(reader, staticOptions, reader.ReadPointerAt(at + 16), 0),
            .. ReadOptions(reader, reader.ReadPointerAt(at + 24), reader.ReadPointerAt(at + 32), staticKeyWords)];

        long attributeArray = reader.ReadPointerAt(at + 40);
        Attributes = [.. reader.ReadNamesAt(reader.ReadPointerAt(at + 48))
            .Select((name, i) => new ShaderAttributeLocation(name, reader.ReadSByteAt(attributeArray + i * 2 + 1)))];
        samplerNames = reader.ReadNamesAt(reader.ReadPointerAt(at + 64));
        storageNames = reader.ReadNamesAt(reader.ReadPointerAt(at + 120));

        long blockArray = reader.ReadPointerAt(at + 88);
        UniformBlocks = [.. reader.ReadNamesAt(reader.ReadPointerAt(at + 96)).Select((name, i) =>
        {
            long block = blockArray + i * 32, uniformArray = reader.ReadPointerAt(block), defaults = reader.ReadPointerAt(block + 16);
            int size = reader.ReadUInt16At(block + 26);
            var uniforms = reader.ReadNamesAt(reader.ReadPointerAt(block + 8))
                .Select((key, j) => new ShaderUniform(key, reader.ReadUInt16At(uniformArray + j * 16 + 12) - 1)).ToArray();
            return new ShaderUniformBlock(name, reader.ReadByteAt(block + 25), size, uniforms, defaults == 0 ? ReadOnlyMemory<byte>.Empty : owner.Memory(defaults, size));
        })];
    }

    // Dynamic option words are stored relative to the dynamic key region; dynamicBase moves them after the static words.
    private static IEnumerable<SwitchShaderOption> ReadOptions(BinaryReader reader, long array, long dictionary, int dynamicBase) =>
        reader.ReadNamesAt(dictionary).Select((name, i) =>
        {
            long at = array + i * 40;
            int word = reader.ReadByteAt(at + 30);
            if (dynamicBase > 0) word = word - reader.ReadByteAt(at + 29) + dynamicBase;
            return new SwitchShaderOption(name, reader.ReadNamesAt(reader.ReadPointerAt(at + 8)), reader.ReadByteAt(at + 25),
                word, reader.ReadByteAt(at + 31), reader.ReadUInt32At(at + 32));
        }).ToArray();

    // The compiled program for a mesh's material in the default render pass, or -1 when the archive has none.
    public int FindMaterialProgram(BfresMesh mesh)
    {
        var material = mesh.Material;
        var choices = new Dictionary<string, string>(material.ShaderOptions)
        {
            ["gsys_weight"] = mesh.VertexSkinCount.ToString(CultureInfo.InvariantCulture),
            ["gsys_assign_type"] = "gsys_assign_material", ["gsys_index_stream_format"] = "0", ["system_id"] = "0",
            ["gsys_renderstate"] = material.Info("gsys_render_state_mode", "opaque") switch
                { "mask" => "1", "translucent" => "2", "custom" => "3", _ => "0" }
        };
        if (material.Info("gsys_alpha_test_enable", "false") == "true") choices["gsys_alpha_test_enable"] = "1";

        var words = new uint[keyWords];
        foreach (var option in Options)
        {
            int index = option.DefaultChoice;
            if (choices.TryGetValue(option.Name, out string? value) && (index = ChoiceIndex(option, value)) < 0) return -1;
            words[option.Word] = (words[option.Word] & ~option.Mask) | ((uint)index << option.Shift);
        }
        for (int i = 0; i < ProgramCount; i++)
            if (keys.AsSpan(i * keyWords, keyWords).SequenceEqual(words)) return i;
        return -1;
    }

    private static int ChoiceIndex(SwitchShaderOption option, string value)
    {
        for (int i = 0; i < option.Choices.Count; i++) if (option.Choices[i] == value) return i;
        return -1;
    }

    public SwitchShaderProgram ReadProgram(int index)
    {
        using var reader = owner.Open();
        long entry = programArray + index * 64;
        long binary = Native(reader, reader.ReadPointerAt(entry + 32) + 16);
        if (binary == bnshBase) throw new InvalidDataException("Shader variation has no native binary program.");
        return new(index, ReadStage(reader, Native(reader, binary + 8)), ReadStage(reader, Native(reader, binary + 40)),
            Locations(reader, reader.ReadPointerAt(entry + 16), [.. UniformBlocks.Select(x => x.Name)]),
            Locations(reader, reader.ReadPointerAt(entry), samplerNames));
    }

    // Offsets inside the BNSH container are relative to its start.
    private long Native(BinaryReader reader, long at) => bnshBase + reader.ReadPointerAt(at);

    private SwitchShaderStage? ReadStage(BinaryReader reader, long at)
    {
        if (at == bnshBase) return null;
        long control = Native(reader, at + 8), binary = Native(reader, at + 16);
        return new(owner.Memory(control, reader.ReadInt32At(at + 28)), owner.Memory(binary, reader.ReadInt32At(at + 24)));
    }

    private static ShaderResourceLocation[] Locations(BinaryReader reader, long at, IReadOnlyList<string> names) => at == 0 ? []
        : [.. names.Select((name, i) => new ShaderResourceLocation(name, reader.ReadInt32At(at + i * 24), reader.ReadInt32At(at + i * 24 + 8)))];
}

public sealed class SwitchShaderArchive
{
    private readonly byte[] data;
    public string Name { get; }
    public IReadOnlyList<SwitchShadingModel> Models { get; }

    private SwitchShaderArchive(byte[] data)
    {
        this.data = data;
        using var reader = Open();
        if (reader.ReadUInt32At(8) != 0x00080000 || reader.ReadUInt16At(12) != 0xfeff)
            throw new NotSupportedException("Only the ACNH Switch BFSHA v8 layout is supported.");
        Name = reader.ReadNameAt(56);
        long array = reader.ReadPointerAt(72);
        Models = [.. Enumerable.Range(0, reader.ReadUInt16At(120)).Select(i => new SwitchShadingModel(this, reader, array + i * 256))];
    }

    // Loads a BFSHA, optionally Yaz0 or Zstd compressed, or the single BFSHA inside a SARC.
    public static SwitchShaderArchive Load(string path)
    {
        using var input = File.OpenRead(path);
        var files = BfresFileReader.Unpack(input, "FSHA"u8, "BFSHA");
        return files.Count == 1 ? new(files[0].Data) : throw new InvalidDataException("Expected one BFSHA file in the shader archive.");
    }

    // Readers seek a shared position, and the RSTB sizer uses one archive from several threads, so every lazy read opens its own.
    internal BinaryReader Open() => new(new MemoryStream(data, false), false) { Encoding = Encoding.UTF8 };

    internal ReadOnlyMemory<byte> Memory(long at, int length) => data.AsMemory(checked((int)at), length);
}

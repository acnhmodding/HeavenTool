using AeonSake.NintendoTools.FileFormats.Sarc;
using HeavenTool.IO.Compression;
using System.Buffers.Binary;
using System.Text;

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
public sealed record SwitchShaderProgram(int Index, SwitchShaderStage? Vertex, SwitchShaderStage? Fragment)
{
    public IReadOnlyList<ShaderResourceLocation> Buffers { get; internal set; } = [];
    public IReadOnlyList<ShaderResourceLocation> Samplers { get; internal set; } = [];
}

public sealed partial class SwitchShadingModel
{
    private readonly SwitchShaderArchive owner;
    private readonly int programArray, keyTable, bnshBase, bnshSize, keyWords;
    private readonly int modelOffset, staticKeyWords;
    public string Name { get; }
    public int ProgramCount { get; }
    public IReadOnlyList<SwitchShaderOption> Options { get; }
    public IReadOnlyList<ShaderAttributeLocation> Attributes { get; }
    public IReadOnlyList<ShaderUniformBlock> UniformBlocks { get; }
    private readonly IReadOnlyList<string> samplerNames;

    internal SwitchShadingModel(SwitchShaderArchive owner, int at)
    {
        this.owner = owner;
        modelOffset = at;
        Name = owner.String(owner.Pointer(at));
        programArray = owner.Pointer(at + 136); keyTable = owner.Pointer(at + 144); bnshBase = owner.Pointer(at + 168);
        ProgramCount = owner.U16(at + 232);
        int staticWords = owner.Byte(at + 234), dynamicWords = owner.Byte(at + 235);
        staticKeyWords = staticWords;
        keyWords = staticWords + dynamicWords;
        owner.Range(programArray, checked(ProgramCount * 64)); owner.Range(keyTable, checked(ProgramCount * keyWords * 4));
        owner.Signature(bnshBase, "BNSH");
        bnshSize = checked((int)owner.U32(bnshBase + 28));
        owner.Range(bnshBase, bnshSize);
        if (bnshSize < 32) throw new InvalidDataException("Invalid BNSH file size.");
        var options = ReadOptions(owner.Pointer(at + 8), owner.Pointer(at + 16), 0);
        options.AddRange(ReadOptions(owner.Pointer(at + 24), owner.Pointer(at + 32), staticWords));
        Options = options.AsReadOnly();
        int attributeArray = owner.Pointer(at + 40);
        Attributes = owner.Dictionary(owner.Pointer(at + 48)).Select((name, i) => new ShaderAttributeLocation(name, (sbyte)owner.Byte(attributeArray + i * 2 + 1))).ToArray();
        samplerNames = owner.Dictionary(owner.Pointer(at + 64));
        int blockArray = owner.Pointer(at + 88);
        var blocks = new List<ShaderUniformBlock>();
        foreach (string name in owner.Dictionary(owner.Pointer(at + 96)))
        {
            int b = blockArray + blocks.Count * 32, size = owner.U16(b + 26), defaults = owner.Pointer(b + 16);
            int uniformArray = owner.Pointer(b);
            var uniforms = owner.Dictionary(owner.Pointer(b + 8)).Select((key, i) => new ShaderUniform(key, owner.U16(uniformArray + i * 16 + 12) - 1)).ToArray();
            blocks.Add(new(name, owner.Byte(b + 25), size, uniforms, defaults == 0 ? ReadOnlyMemory<byte>.Empty : owner.Memory(defaults, size)));
        }
        UniformBlocks = blocks.AsReadOnly();
    }

    private List<SwitchShaderOption> ReadOptions(int array, int dictionary, int dynamicBase)
    {
        var names = owner.Dictionary(dictionary);
        owner.Range(array, checked(names.Count * 40));
        var options = new List<SwitchShaderOption>(names.Count);
        for (int i = 0; i < names.Count; i++)
        {
            int at = array + i * 40;
            var choices = owner.Dictionary(owner.Pointer(at + 8));
            int defaultChoice = owner.Byte(at + 25), word = owner.Byte(at + 30), shift = owner.Byte(at + 31);
            if (dynamicBase > 0) word = word - owner.Byte(at + 29) + dynamicBase;
            uint mask = owner.U32(at + 32);
            if (choices.Count != owner.Byte(at + 24) || defaultChoice >= choices.Count || word < 0 || word >= keyWords || shift > 31 || mask == 0)
                throw new InvalidDataException("Invalid shader option bit packing.");
            options.Add(new(names[i], choices.AsReadOnly(), defaultChoice, word, shift, mask));
        }
        return options;
    }
    public int FindProgram(IReadOnlyDictionary<string, string> choices)
    {
        var words = new uint[keyWords];
        foreach (var option in Options)
        {
            int index = option.DefaultChoice;
            if (choices.TryGetValue(option.Name, out string? value))
            {
                index = -1;
                for (int i = 0; i < option.Choices.Count; i++) if (option.Choices[i] == value) { index = i; break; }
                if (index < 0) return -1;
            }
            uint bits = (uint)index << option.Shift;
            if ((bits & ~option.Mask) != 0) throw new InvalidDataException("Shader choice exceeds its bit mask.");
            words[option.Word] = (words[option.Word] & ~option.Mask) | bits;
        }
        for (int i = 0; i < ProgramCount; i++)
        {
            bool match = true;
            for (int j = 0; j < words.Length; j++)
                if (words[j] != owner.U32(keyTable + (i * keyWords + j) * 4)) { match = false; break; }
            if (match) return i;
        }
        return -1;
    }

    public SwitchShaderProgram ReadProgram(int index)
    {
        if ((uint)index >= ProgramCount) throw new ArgumentOutOfRangeException(nameof(index));
        int variation = owner.Pointer(programArray + index * 64 + 32);
        NativeMemory(variation, 64);
        int binary = RelativePointer(variation + 16);
        if (binary == bnshBase) throw new InvalidDataException("Shader variation has no native binary program.");
        int entry = programArray + index * 64;
        return new(index, ReadStage(RelativePointer(binary + 8)), ReadStage(RelativePointer(binary + 40)))
        {
            Buffers = Locations(owner.Pointer(entry + 16), UniformBlocks.Select(x => x.Name).ToArray()),
            Samplers = Locations(owner.Pointer(entry), samplerNames)
        };
    }
    private ShaderResourceLocation[] Locations(int at, IReadOnlyList<string> names)
    {
        if (at == 0) return [];
        owner.Range(at, checked(names.Count * 24));
        return names.Select((name, i) => new ShaderResourceLocation(name, unchecked((int)owner.U32(at + i * 24)), unchecked((int)owner.U32(at + i * 24 + 8)))).ToArray();
    }
    private int RelativePointer(int at)
    {
        NativeMemory(at, 8);
        int offset = owner.Pointer(at);
        if (offset > bnshSize) throw new InvalidDataException("Shader pointer exceeds its BNSH container.");
        return checked(bnshBase + offset);
    }
    private SwitchShaderStage? ReadStage(int at)
    {
        if (at == bnshBase) return null;
        int control = RelativePointer(at + 8), binary = RelativePointer(at + 16);
        int binarySize = checked((int)owner.U32(at + 24)), controlSize = checked((int)owner.U32(at + 28));
        return new(NativeMemory(control, controlSize), NativeMemory(binary, binarySize));
    }
    private ReadOnlyMemory<byte> NativeMemory(int at, int length)
    {
        if (at < bnshBase || length < 0 || at - bnshBase > bnshSize - length) throw new InvalidDataException("Shader range exceeds its BNSH container.");
        return owner.Memory(at, length);
    }
}
public sealed class SwitchShaderArchive
{
    private readonly byte[] data;
    public string Name { get; }
    public IReadOnlyList<SwitchShadingModel> Models { get; }
    private SwitchShaderArchive(byte[] data)
    {
        this.data = data;
        Signature(0, "FSHA");
        if (U32(8) != 0x00080000 || U16(12) != 0xfeff)
            throw new NotSupportedException("Only the ACNH Switch BFSHA v8 layout is supported.");
        if (U32(28) != data.Length) throw new InvalidDataException("Shader archive size mismatch.");
        Name = String(Pointer(56));
        int array = Pointer(72), count = U16(120);
        Range(array, checked(count * 256));
        var models = new List<SwitchShadingModel>(count);
        for (int i = 0; i < count; i++) models.Add(new(this, array + i * 256));
        Models = models.AsReadOnly();
    }

    public static SwitchShaderArchive Read(ReadOnlySpan<byte> bytes) => new(bytes.ToArray());
    public static SwitchShaderArchive Load(string path)
    {
        using var input = File.OpenRead(path);
        byte[] data = new byte[4]; input.ReadExactly(data); input.Position = 0;
        using var content = new MemoryStream();
        if (data.AsSpan().SequenceEqual("Yaz0"u8))
        {
            using var decoded = Yaz0CompressionAlgorithm.Decompress(input) ?? throw new InvalidDataException("Shader archive decompression failed.");
            decoded.Position = 0; decoded.CopyTo(content);
        }
        else input.CopyTo(content);
        if (content.GetBuffer().AsSpan(0, checked((int)content.Length)).StartsWith("FSHA"u8)) return Read(content.ToArray());
        content.Position = 0;
        var archive = new SarcFileReader().Read(content);
        var files = archive.Files.Where(x => x.Data.AsSpan().StartsWith("FSHA"u8)).ToArray();
        if (files.Length != 1) throw new InvalidDataException("Expected one BFSHA file in the shader archive.");
        return Read(files[0].Data);
    }

    internal byte Byte(int at) => Range(at, 1)[0];
    internal ushort U16(int at) => BinaryPrimitives.ReadUInt16LittleEndian(Range(at, 2));
    internal uint U32(int at) => BinaryPrimitives.ReadUInt32LittleEndian(Range(at, 4));
    internal int Pointer(int at)
    {
        ulong p = BinaryPrimitives.ReadUInt64LittleEndian(Range(at, 8));
        if (p > (ulong)data.Length) throw new InvalidDataException("Shader pointer exceeds the archive.");
        return (int)p;
    }
    internal string String(int at)
    {
        if (at == 0) return "";
        int length = U16(at);
        if (Byte(at + 2 + length) != 0) throw new InvalidDataException("Unterminated shader string.");
        return Encoding.UTF8.GetString(Range(at + 2, length));
    }
    internal List<string> Dictionary(int at)
    {
        var names = new List<string>();
        if (at == 0) return names;
        int count = checked((int)U32(at + 4));
        if (count > 65535) throw new InvalidDataException("Shader dictionary is too large.");
        Range(at + 8, checked((count + 1) * 16));
        for (int i = 0; i < count; i++) names.Add(String(Pointer(at + 32 + i * 16)));
        return names;
    }
    internal void Signature(int at, string expected)
    {
        if (!Range(at, 4).SequenceEqual(Encoding.ASCII.GetBytes(expected))) throw new InvalidDataException($"Expected {expected} shader section.");
    }
    internal ReadOnlyMemory<byte> Memory(int at, int length) { Range(at, length); return data.AsMemory(at, length); }
    internal ReadOnlySpan<byte> Range(int at, int length)
    {
        if (at < 0 || length < 0 || at > data.Length - length) throw new InvalidDataException("Shader data range exceeds the archive.");
        return data.AsSpan(at, length);
    }
}

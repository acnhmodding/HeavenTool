using System.Text;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.IO.FileFormats.BFRES;

public sealed record SwitchProcessShaderOption(string Name, IReadOnlyList<string> Choices);

// NVN compute binaries begin with instructions, whereas graphics binaries include a header before the Maxwell header.
public sealed record SwitchComputeShaderStage(ReadOnlyMemory<byte> Control, ReadOnlyMemory<byte> Binary);

public sealed record SwitchProcessShaderVariation(SwitchShaderStage? Vertex, SwitchShaderStage? Fragment, SwitchComputeShaderStage? Compute);

internal readonly record struct SwitchProcessShaderStage(int Type, ReadOnlyMemory<byte> Control, ReadOnlyMemory<byte> Binary);

public sealed class SwitchProcessShaderProgram
{
    internal const int VertexStage = 0, FragmentStage = 1, ComputeStage = 3;
    private const int StageTypeCount = 4;

    private readonly IReadOnlyList<SwitchProcessShaderStage> stages;
    private readonly int firstStage;
    private readonly int[] stageTypes;

    public string Name { get; }
    public IReadOnlyList<SwitchProcessShaderOption> Options { get; }
    public int VariationCount { get; }

    internal SwitchProcessShaderProgram(string name, uint stageMask, int firstStage,
        IReadOnlyList<SwitchProcessShaderOption> options, IReadOnlyList<SwitchProcessShaderStage> stages)
    {
        Name = name;
        Options = options;
        this.firstStage = firstStage;
        this.stages = stages;
        stageTypes = [.. Enumerable.Range(0, StageTypeCount).Where(type => (stageMask & (1u << type)) != 0)];

        // The game's loader uses each option's product of later choice counts as its variation stride (71038057b0).
        VariationCount = 1;
        foreach (var option in options)
            VariationCount = checked(VariationCount * option.Choices.Count);

        if (firstStage < 0 || firstStage > stages.Count || VariationCount > (stages.Count - firstStage) / stageTypes.Length)
            throw new InvalidDataException($"Invalid stage range for {name}.");

        int index = firstStage;
        for (int variation = 0; variation < VariationCount; variation++)
            foreach (int type in stageTypes)
                if (stages[index++].Type != type)
                    throw new InvalidDataException($"Unexpected stage type for {name}.");
    }

    /// <summary>All declared option choices are required; an unknown option or choice is an error.</summary>
    public int FindVariation(IReadOnlyDictionary<string, string> choices)
    {
        if (choices.Count != Options.Count)
            throw new ArgumentException("Supply every declared process-shader option.", nameof(choices));

        int index = 0;
        foreach (var option in Options)
        {
            if (!choices.TryGetValue(option.Name, out string? value))
                throw new ArgumentException($"Missing option {option.Name}.", nameof(choices));

            int choice = option.Choices.TakeWhile(candidate => candidate != value).Count();
            if (choice == option.Choices.Count)
                throw new ArgumentException($"Invalid choice for {option.Name}: {value}.", nameof(choices));

            index = index * option.Choices.Count + choice;
        }

        return index;
    }

    public SwitchProcessShaderVariation ReadVariation(int index = 0)
    {
        if ((uint)index >= (uint)VariationCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        SwitchShaderStage? vertex = null, fragment = null;
        SwitchComputeShaderStage? compute = null;

        int next = firstStage + index * stageTypes.Length;
        foreach (int type in stageTypes)
        {
            var stage = stages[next++];
            switch (type)
            {
                case VertexStage: vertex = new(stage.Control, stage.Binary); break;
                case FragmentStage: fragment = new(stage.Control, stage.Binary); break;
                case ComputeStage: compute = new(stage.Control, stage.Binary); break;
            }
        }

        return new(vertex, fragment, compute);
    }
}

/// <summary>ACNH's NVN BAHS v9 process archive, distinct from the BFSHA material archive.</summary>
public sealed class SwitchProcessShaderArchive
{
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    public string Name { get; }
    public IReadOnlyList<SwitchProcessShaderProgram> Programs { get; }

    private SwitchProcessShaderArchive(string name, IReadOnlyList<SwitchProcessShaderProgram> programs)
    {
        Name = name;
        Programs = programs;
    }

    public static SwitchProcessShaderArchive Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    public static SwitchProcessShaderArchive Read(Stream input)
    {
        var files = BfresFileReader.Unpack(input, "BAHS"u8, "SHARCB");
        if (files.Count != 1)
            throw new InvalidDataException("Expected one BAHS process shader archive.");

        try
        {
            using var parser = new Parser(files[0].Data);
            return parser.Parse();
        }
        catch (OverflowException e) { throw new InvalidDataException("Invalid process-shader archive range.", e); }
        catch (DecoderFallbackException e) { throw new InvalidDataException("Invalid process-shader text.", e); }
    }

    private sealed class Parser(byte[] data) : IDisposable
    {
        private const uint SupportedVersion = 9, NvnPlatform = 5;
        private const uint ControlMagic = 0x98761234, GraphicsBinaryMagic = 0x12345678;

        private const uint ComputeStageMask = 1u << SwitchProcessShaderProgram.ComputeStage;
        private const uint SupportedStageMask = 1u << SwitchProcessShaderProgram.VertexStage | 1u << SwitchProcessShaderProgram.FragmentStage | ComputeStageMask;

        // The sizes of the file's fixed structures, in bytes.
        private const int HeaderSize = 0x150, TableHeaderSize = 8;
        private const int StageRecordSize = 32, StageRecordHeaderSize = 16, ProgramRecordSize = 40, OptionRecordSize = 16;
        private const int ControlBlockSize = 1800, MinimumGraphicsBinarySize = 128, MinimumComputeBinarySize = 8;

        // Where the constant buffer's size and offset, then the binary's size, sit in a control block.
        private const int ControlSizesOffset = 1788;

        private readonly BinaryReader reader = new(new MemoryStream(data, false), false);

        // A (size, count) header followed by the records, each of which starts with its own size.
        private readonly record struct Table(int First, int Count, int End, int MinimumRecordSize);

        public void Dispose() => reader.Dispose();

        public SwitchProcessShaderArchive Parse()
        {
            Within(0, HeaderSize, data.Length);

            // 7103808960 locates the variable-length stage list at header +0x14 +0x150, then the program list after it.
            reader.Position = 4;
            uint version = reader.ReadUInt32();
            uint fileSize = reader.ReadUInt32();
            uint platform = reader.ReadUInt32();
            reader.Skip(4);
            uint nameSize = reader.ReadUInt32();
            uint gpuSizeField = reader.ReadUInt32();
            uint gpuBaseField = reader.ReadUInt32();

            if (version != SupportedVersion || platform != NvnPlatform)
                throw new NotSupportedException("Only the ACNH NVN BAHS v9 layout is supported.");
            if (fileSize != data.Length)
                throw new InvalidDataException("Invalid process-shader archive size.");

            int stageList = checked(HeaderSize + (int)nameSize);
            int gpuBase = checked((int)gpuBaseField), gpuSize = checked((int)gpuSizeField);
            Within(gpuBase, gpuSize, data.Length);
            if (stageList < HeaderSize || stageList >= gpuBase)
                throw new InvalidDataException("Invalid process-shader metadata range.");

            string name = ReadString(HeaderSize, stageList).Value;

            var stageTable = ReadTable(stageList, StageRecordSize, gpuBase);
            var stages = ReadRecords(stageTable, "stage", (start, end) => ReadStage(start, end, stageTable, gpuBase, gpuSize));

            var programNames = new HashSet<string>(StringComparer.Ordinal);
            var programTable = ReadTable(stageTable.End, ProgramRecordSize, gpuBase);
            var programs = ReadRecords(programTable, "program", (_, end) => ReadProgram(end, stages, programNames));

            return new(name, programs.AsReadOnly());
        }

        private SwitchProcessShaderStage ReadStage(int start, int end, Table table, int gpuBase, int gpuSize)
        {
            int type = ReadSize();
            reader.Skip(4);
            if (reader.ReadUInt32() != end - start - StageRecordHeaderSize)
                throw new InvalidDataException("Invalid process-shader stage record size.");
            if (type is not (SwitchProcessShaderProgram.VertexStage or SwitchProcessShaderProgram.FragmentStage or SwitchProcessShaderProgram.ComputeStage))
                throw new NotSupportedException($"Unsupported process-shader stage type {type}.");

            int nativeStart = (int)reader.Position;
            int binaryOffset = ReadSize();
            reader.Skip(4);
            int controlSize = ReadSize();
            int control = checked(nativeStart + reader.ReadInt32());

            Within(control, controlSize, table.End);
            if (control < table.First || controlSize < ControlBlockSize)
                throw new InvalidDataException("Invalid NVN process-shader control block.");

            reader.Position = control;
            if (reader.ReadUInt32() != ControlMagic)
                throw new InvalidDataException("Invalid NVN process-shader control block.");

            reader.Position = control + ControlSizesOffset;
            int constantsSize = ReadSize();
            int constantsOffset = ReadSize();
            int binarySize = ReadSize();

            Within(binaryOffset, binarySize, gpuSize);
            int binary = gpuBase + binaryOffset;
            if (!IsValidBinary(type, binary, binarySize))
                throw new InvalidDataException("Invalid NVN process-shader binary.");
            Within(constantsOffset, constantsSize, binarySize);

            return new(type, data.AsMemory(control, controlSize), data.AsMemory(binary, binarySize));
        }

        private bool IsValidBinary(int type, int binary, int size)
        {
            if (type == SwitchProcessShaderProgram.ComputeStage)
                return size >= MinimumComputeBinarySize;
            if (size < MinimumGraphicsBinarySize)
                return false;

            reader.Position = binary;
            return reader.ReadUInt32() == GraphicsBinaryMagic;
        }

        private SwitchProcessShaderProgram ReadProgram(int end, List<SwitchProcessShaderStage> stages, HashSet<string> names)
        {
            int nameSize = ReadSize();
            uint stageMask = reader.ReadUInt32();
            if (stageMask == 0 || (stageMask & ~SupportedStageMask) != 0 || ((stageMask & ComputeStageMask) != 0 && stageMask != ComputeStageMask))
                throw new NotSupportedException($"Unsupported process-shader stage mask {stageMask:x}.");
            int firstStage = ReadSize();

            int nameStart = (int)reader.Position;
            string name = ReadName(nameStart, nameSize, end, "program");
            if (!names.Add(name))
                throw new InvalidDataException("Duplicate process-shader program name.");

            var optionNames = new HashSet<string>(StringComparer.Ordinal);
            var optionTable = ReadTable(checked(nameStart + nameSize), OptionRecordSize, end);
            var options = ReadRecords(optionTable, "option", (_, optionEnd) => ReadOption(optionEnd, optionNames));

            return new(name, stageMask, firstStage, options.AsReadOnly(), stages);
        }

        private SwitchProcessShaderOption ReadOption(int end, HashSet<string> names)
        {
            int nameSize = ReadSize();
            int choiceCount = ReadSize();
            uint suffixSize = reader.ReadUInt32();

            int nameStart = (int)reader.Position;
            string name = ReadName(nameStart, nameSize, end, "option");

            int strings = checked(nameStart + nameSize);
            if (!names.Add(name) || choiceCount < 1 || choiceCount > end - strings)
                throw new InvalidDataException("Invalid process-shader option choices.");

            var choices = new List<string>(choiceCount);
            for (int i = 0; i < choiceCount; i++)
            {
                var (v, n) = ReadString(strings, end);
                strings = n;
                choices.Add(v);
            }

            // The additional string is not used by the game's variation-stride calculation.
            var (_, next) = ReadString(strings, end);
            if (next != end || next - strings != suffixSize || choices.Distinct(StringComparer.Ordinal).Count() != choiceCount)
                throw new InvalidDataException("Invalid process-shader option strings.");

            return new(name, choices.AsReadOnly());
        }

        private Table ReadTable(int at, int minimumRecordSize, int limit)
        {
            Within(at, TableHeaderSize, limit);

            reader.Position = at;
            int size = ReadSize();
            int count = ReadSize();

            Within(at, size, limit);
            if (size < TableHeaderSize || count > (size - TableHeaderSize) / minimumRecordSize)
                throw new InvalidDataException("Invalid process-shader table.");

            return new(at + TableHeaderSize, count, checked(at + size), minimumRecordSize);
        }

        // Calls readRecord with each record's bounds and the reader just past the record's own size.
        private List<T> ReadRecords<T>(Table table, string kind, Func<int, int, T> readRecord)
        {
            var records = new List<T>(table.Count);
            int start = table.First;
            for (int i = 0; i < table.Count; i++)
            {
                Within(start, table.MinimumRecordSize, table.End);

                reader.Position = start;
                int size = ReadSize();
                Within(start, size, table.End);
                if (size < table.MinimumRecordSize)
                    throw new InvalidDataException($"Truncated process-shader {kind} record.");

                int end = checked(start + size);
                records.Add(readRecord(start, end));
                start = end;
            }

            if (start != table.End)
                throw new InvalidDataException($"Process-shader {kind} table size differs from its records.");
            return records;
        }

        private string ReadName(int at, int size, int limit, string kind)
        {
            Within(at, size, limit);
            if (size < 1)
                throw new InvalidDataException($"Missing process-shader {kind} name.");

            var (name, next) = ReadString(at, checked(at + size));
            if (next != at + size || name.Length == 0)
                throw new InvalidDataException($"Invalid process-shader {kind} name.");
            return name;
        }

        private (string Value, int Next) ReadString(int at, int end)
        {
            Within(at, 1, end);

            int terminator = Array.IndexOf(data, (byte)0, at, end - at);
            if (terminator < 0)
                throw new InvalidDataException("Unterminated process-shader string.");

            return (Utf8.GetString(data, at, terminator - at), terminator + 1);
        }

        private int ReadSize() => checked((int)reader.ReadUInt32());

        private static void Within(int at, int length, int end)
        {
            if (at < 0 || length < 0 || at > end || length > end - at)
                throw new InvalidDataException("Process-shader range exceeds its container.");
        }
    }
}

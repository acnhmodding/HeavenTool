using HeavenTool.IO.FileFormats.BARS.MINF.Sections;
using System.ComponentModel;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.MINF;

public class MusicInfo
{
    public const string MAGIC = "MINF";
    private readonly byte[] originalData;
    private readonly int section2Offset;
    private readonly byte[]? originalModeledData;

    [Browsable(false)]
    public TempoMap? TempoMap { get; set; }
    [Browsable(false)]
    public ChordTrack? ChordTrack { get; set; }
    [Browsable(false)]
    public BeatMap? BeatMap { get; set; }
    [Browsable(false)]
    public MeasureMap? MeasureMap { get; set; }
    [Browsable(false)]
    public NamedMarkerTracks? NamedMarkerTracks { get; set; }
    [Browsable(false)]
    public NamedEventTracks? NamedEventTracks { get; set; }
    [Browsable(false)]
    public PerformanceSequence? PerformanceSequence { get; set; }
    /// <summary>Unresolved section 2 storage. Its original location and backing resource are retained when writing.</summary>
    [Browsable(false)]
    public ReadOnlyMemory<byte> RawSection2 { get; }

    public MusicInfo(BinaryReader reader, long location, PerformanceSequenceFormat performanceFormat = PerformanceSequenceFormat.Unknown)
    {
        ArgumentNullException.ThrowIfNull(reader);
        bool previousEndian = reader.BigEndian;
        using var scope = reader.CreateScopeAt(location);
        try
        {
            string magic = reader.ReadString(4);
            if (magic != MAGIC) 
                throw new InvalidDataException($"Expected MINF magic, but got {magic}.");

            ushort endian = reader.ReadUInt16();
            if (endian is not 0xFEFF and not 0xFFFE)
                throw new InvalidDataException("Invalid MINF byte order.");

            reader.BigEndian = BigEndian = endian == (previousEndian ? 0xFEFF : 0xFFFE);

            ushort version = reader.ReadUInt16();
            VersionMajor = (byte)(version >> 8);
            VersionMinor = (byte)version;

            uint size = reader.ReadUInt32();
            if (size < 0x4c || size > reader.Length - location)
                throw new InvalidDataException("MINF size is outside the resource.");

            originalData = reader.ReadByteArrayAt(location, checked((int)size));
            using var input = new BinaryReader(new MemoryStream(originalData)) { BigEndian = BigEndian };
            input.Position = 12;
            long NameOffset()
            {
                using var nameScope = input.CreateScope();
                long field = input.Position;
                uint relative = input.ReadUInt32();
                long target = field + relative;
                if (relative == 0 || target < 0x4c || target >= size)
                    throw new InvalidDataException("MINF name pointer is outside its payload.");
                return target;
            }
            long englishNameOffset = NameOffset();
            EnglishName = input.ReadStringPointer();
            long japaneseNameOffset = NameOffset();
            JapaneseName = input.ReadStringPointer();
            SampleRate = input.ReadUInt32();
            LoopStart = input.ReadUInt32();
            LoopEnd = input.ReadUInt32();
            TempoSamplePosition = input.ReadUInt32();
            BeatsPerMinute = input.ReadSingle();
            TimeSignatureNumerator = input.ReadUInt16();
            TimeSignatureDenominator = input.ReadUInt16();

            var sections = new long[8];
            for (int i = 0; i < sections.Length; i++)
            {
                long field = input.Position;
                uint relative = input.ReadUInt32();
                if (relative == 0) continue;
                sections[i] = field + relative;
                if (sections[i] < 0x4c || sections[i] >= size)
                    throw new InvalidDataException("MINF section pointer is outside its payload.");
            }
            long EndOf(long start) => sections.Concat(NamedMarkerTracks?.NameOffsets ?? [])
                .Concat(NamedEventTracks?.NameOffsets ?? []).Append(englishNameOffset).Append(japaneseNameOffset).Append(size)
                .Where(p => p > start).Min();
            if (sections[0] != 0) TempoMap = new TempoMap(input, sections[0]);
            if (sections[1] != 0) ChordTrack = new ChordTrack(input, sections[1]);
            if (sections[3] != 0) BeatMap = new BeatMap(input, sections[3]);
            if (sections[4] != 0) MeasureMap = new MeasureMap(input, sections[4]);
            if (sections[5] != 0) NamedMarkerTracks = new NamedMarkerTracks(input, sections[5]);
            if (sections[6] != 0) NamedEventTracks = new NamedEventTracks(input, sections[6]);
            if (sections[7] != 0)
                PerformanceSequence = new PerformanceSequence(input, sections[7], performanceFormat, EndOf(sections[7]));
            section2Offset = checked((int)sections[2]);
            if (section2Offset != 0)
            {
                RawSection2 = originalData.AsMemory(section2Offset, checked((int)(EndOf(section2Offset) - section2Offset)));
                originalModeledData = Build(false);
            }
        }
        finally { reader.BigEndian = previousEndian; }
    }

    public byte[] ToBytes()
    {
        byte[] modeled = Build(false);
        if (section2Offset == 0) return modeled;
        if (modeled.AsSpan().SequenceEqual(originalModeledData)) return [.. originalData];
        return Build(true);
    }

    private byte[] Build(bool preserveSection2)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream) { BigEndian = BigEndian };
        if (preserveSection2)
        {
            writer.Write(originalData);
            writer.Position = 0;
        }
        var strings = new StringPool();
        writer.Write(MAGIC);
        writer.Write((ushort)0xFEFF);
        writer.Write((ushort)((VersionMajor << 8) | VersionMinor));
        var size = writer.CreatePointer();
        strings.AddString(writer, EnglishName);
        strings.AddString(writer, JapaneseName);
        writer.Write(SampleRate);
        writer.Write(LoopStart);
        writer.Write(LoopEnd);
        writer.Write(TempoSamplePosition);
        writer.Write(BeatsPerMinute);
        writer.Write(TimeSignatureNumerator);
        writer.Write(TimeSignatureDenominator);
        var sections = Enumerable.Range(0, 8).Select(_ => writer.CreatePointer()).ToArray();
        if (preserveSection2)
        {
            sections[2].Resolve(checked((uint)(section2Offset - sections[2].Position)));
            writer.Position = writer.Length;
        }
        void Section(int index, Action<BinaryWriter>? write)
        {
            if (write == null) return;
            writer.Align(4);
            sections[index].Resolve(write);
        }
        Section(0, TempoMap is { } tempo ? tempo.Write : null);
        Section(1, ChordTrack is { } chords ? chords.Write : null);
        Section(3, BeatMap is { } beats ? beats.Write : null);
        Section(4, MeasureMap is { } measures ? measures.Write : null);
        Section(5, NamedMarkerTracks is { } markers ? w => markers.Write(w, strings) : null);
        Section(6, NamedEventTracks is { } events ? w => events.Write(w, strings) : null);
        Section(7, PerformanceSequence is { } performance ? performance.Write : null);
        writer.Align(4);
        strings.Write(writer);
        writer.Align(4);
        size.Resolve(checked((uint)writer.Length));
        return stream.ToArray();
    }

    [Browsable(false)]
    public bool BigEndian { get; }
    [Category("Header")]
    public byte VersionMajor { get; set; }
    [Category("Header")]
    public byte VersionMinor { get; set; }
    [Category("Header")]
    public string EnglishName { get; set; }
    [Category("Header")]
    public string JapaneseName { get; set; }
    [Category("Audio")]
    public uint SampleRate { get; set; }
    [Category("Audio")]
    public uint LoopStart { get; set; }
    [Category("Audio")]
    public uint LoopEnd { get; set; }
    [Category("Audio")]
    public uint TempoSamplePosition { get; set; }
    [Category("Audio")]
    public float BeatsPerMinute { get; set; }
    [Category("Audio")]
    public ushort TimeSignatureNumerator { get; set; }
    [Category("Audio")]
    public ushort TimeSignatureDenominator { get; set; }
}

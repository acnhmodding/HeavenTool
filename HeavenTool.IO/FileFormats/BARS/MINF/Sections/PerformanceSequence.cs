using HeavenTool.IO.FileFormats.BARS.MINF.Sections.PerformanceTracks;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections;

/// <summary>A raw subsection container interpreted through a caller-supplied game context.</summary>
public class PerformanceSequence
{
    private static readonly InstrumentType[] instrumentTypes = Enum.GetValues<InstrumentType>();
    private readonly bool bigEndian;
    private readonly uint[] subsectionOffsets;
    private readonly Dictionary<int, byte[]?> originalTrackData = [];

    public PerformanceSequenceFormat Format { get; }
    public ReadOnlyMemory<byte> RawData { get; }

    public VocalTrack? Vocals { get; set; }
    public PitchValueTrack?[] PitchBends { get; } = [];
    public PitchValueTrack?[] Vibrato { get; } = [];
    public Dictionary<InstrumentType, InstrumentTrack> Instruments { get; } = [];

    public PerformanceSequence(BinaryReader reader, long offset,
        PerformanceSequenceFormat format = PerformanceSequenceFormat.Unknown, long? endOffset = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (!Enum.IsDefined(format)) throw new ArgumentOutOfRangeException(nameof(format));
        long end = endOffset ?? reader.Length;
        if (offset < 0 || end > reader.Length || end - offset < 4)
            throw new InvalidDataException("Performance container is outside the resource.");

        using var scope = reader.CreateScopeAt(offset);
        bigEndian = reader.BigEndian;
        Format = format;
        subsectionOffsets = ReadSubsectionOffsets(reader, offset, end);
        RawData = reader.ReadByteArrayAt(offset, checked((int)(end - offset)));
        if (format == PerformanceSequenceFormat.Unknown) return;

        using var input = new BinaryReader(new MemoryStream(RawData.ToArray())) { BigEndian = bigEndian };
        Vocals = ReadTrack(0, input, (r, p) => new VocalTrack(r, p));
        if (format == PerformanceSequenceFormat.AcnhDj)
        {
            foreach (var instrument in instrumentTypes)
                if (ReadTrack((int)instrument, input,
                    (r, p) => new InstrumentTrack(r, p, tagged: instrument == InstrumentType.Guitar)) is { } track)
                    Instruments.Add(instrument, track);
        }
        else
        {
            int groups = format == PerformanceSequenceFormat.AcnhKappn ? 6 : 11;
            PitchBends = ReadModulationTracks(2, input, groups);
            Vibrato = ReadModulationTracks(3, input, groups);
        }
        foreach (var (slot, write) in GetTrackWriters())
            originalTrackData[slot] = SerializeTrack(write);
    }

    private static uint[] ReadSubsectionOffsets(BinaryReader reader, long start, long end)
    {
        uint count = reader.ReadUInt32();
        if (count > (end - reader.Position) / 4)
            throw new InvalidDataException("Performance subsection table is truncated.");

        var offsets = new uint[checked((int)count)];
        long headerEnd = reader.Position + offsets.Length * 4L;
        for (int i = 0; i < offsets.Length; i++)
        {
            long field = reader.Position;
            uint relative = reader.ReadUInt32();
            if (relative == 0) continue;
            long target = field + relative;
            if (target < headerEnd || target >= end)
                throw new InvalidDataException("Performance subsection pointer is outside its payload.");
            offsets[i] = checked((uint)(target - start));
        }
        return offsets;
    }

    private bool HasSubsection(int slot) => slot < subsectionOffsets.Length && subsectionOffsets[slot] != 0;

    private T? ReadTrack<T>(int slot, BinaryReader reader, Func<BinaryReader, uint, T> read) where T : class =>
        HasSubsection(slot) ? read(reader, subsectionOffsets[slot]) : null;

    private PitchValueTrack?[] ReadModulationTracks(int slot, BinaryReader reader, int groups) =>
        HasSubsection(slot) ? ReadModulationTable(reader, subsectionOffsets[slot], groups) : new PitchValueTrack?[groups];

    public void Write(BinaryWriter writer)
    {
        if (writer.BigEndian != bigEndian)
            throw new NotSupportedException("Opaque performance data cannot be converted to a different byte order.");
        writer.Write(ToBytes());
    }

    public byte[] ToBytes()
    {
        ValidateTypedTracks();
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream) { BigEndian = bigEndian };
        writer.Write(RawData.ToArray());
        foreach (var (slot, write) in GetTrackWriters())
        {
            byte[]? data = SerializeTrack(write);
            originalTrackData.TryGetValue(slot, out var original);
            if (data.AsSpan().SequenceEqual(original)) continue;
            WriteChangedTrack(writer, slot, data);
        }
        return stream.ToArray();
    }

    private void ValidateTypedTracks()
    {
        if (Format == PerformanceSequenceFormat.Unknown && Vocals != null)
            throw new InvalidOperationException("Select a performance format before editing typed tracks.");
        if (Format != PerformanceSequenceFormat.AcnhDj && Instruments.Count != 0)
            throw new InvalidOperationException("Instrument tracks require the DJ performance format.");
        foreach (var (instrument, track) in Instruments)
        {
            if (!Enum.IsDefined(instrument))
                throw new InvalidOperationException("Unknown instrument slot.");
            if (track == null || track.Tagged != (instrument == InstrumentType.Guitar))
                throw new InvalidOperationException("Instrument layout does not match its slot.");
        }
    }

    private void WriteChangedTrack(BinaryWriter writer, int slot, byte[]? data)
    {
        if (slot >= subsectionOffsets.Length)
            throw new InvalidOperationException("Track slot is absent from the performance pointer table.");
        long field = 4 + slot * 4L;
        uint relative = 0;
        if (data != null)
        {
            writer.Align(4);
            relative = checked((uint)(writer.Position - field));
            writer.Write(data);
        }
        using (writer.CreateScope()) writer.WriteAt(field, relative);
    }

    private byte[]? SerializeTrack(Action<BinaryWriter>? write)
    {
        if (write == null) return null;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream) { BigEndian = bigEndian };
        write(writer);
        return stream.ToArray();
    }

    private IEnumerable<(int Slot, Action<BinaryWriter>? Write)> GetTrackWriters()
    {
        if (Format == PerformanceSequenceFormat.Unknown) yield break;
        yield return (0, Vocals is { } vocals ? vocals.Write : null);
        if (Format == PerformanceSequenceFormat.AcnhDj)
        {
            foreach (var instrument in instrumentTypes)
                yield return ((int)instrument, Instruments.TryGetValue(instrument, out var track) ? track.Write : null);
        }
        else
        {
            yield return (2, ModulationWriter(2, PitchBends));
            yield return (3, ModulationWriter(3, Vibrato));
        }
    }

    private Action<BinaryWriter>? ModulationWriter(int slot, PitchValueTrack?[] tracks) =>
        HasSubsection(slot) || tracks.Any(t => t != null) ? w => WriteModulationTable(w, tracks) : null;

    private static void WriteModulationTable(BinaryWriter writer, PitchValueTrack?[] tracks)
    {
        var pointers = tracks.Select(_ => writer.CreatePointer()).ToArray();
        for (int i = 0; i < tracks.Length; i++)
            if (tracks[i] is { } track) pointers[i].Resolve(track.Write);
    }

    private static PitchValueTrack?[] ReadModulationTable(BinaryReader reader, long table, int groups)
    {
        var tracks = new PitchValueTrack?[groups];
        int tableSize = groups * 4;
        if (reader.Length - table < tableSize)
            throw new InvalidDataException("Modulation pointer table is truncated.");
        using var scope = reader.CreateScopeAt(table);
        var offsets = new long[groups];
        for (int i = 0; i < groups; i++)
        {
            uint relative = reader.ReadUInt32();
            if (relative == 0) continue;
            long target = table + i * 4L + relative;
            if (target < table + tableSize || target > reader.Length - 8)
                throw new InvalidDataException("Modulation pointer is outside its track payload.");
            offsets[i] = target;
        }
        for (int i = 0; i < groups; i++)
            if (offsets[i] != 0)
                tracks[i] = new PitchValueTrack(reader, checked((uint)offsets[i]));
        return tracks;
    }

    public PitchValueTrack? PitchBendFor(VocalTrack.VoiceType voiceType) => TrackFor(PitchBends, voiceType);
    public PitchValueTrack? VibratoFor(VocalTrack.VoiceType voiceType) => TrackFor(Vibrato, voiceType);

    private static PitchValueTrack? TrackFor(PitchValueTrack?[] tracks, VocalTrack.VoiceType voiceType)
    {
        int index = (byte)voiceType - 2;
        return index >= 0 && index < tracks.Length ? tracks[index] : null;
    }
}

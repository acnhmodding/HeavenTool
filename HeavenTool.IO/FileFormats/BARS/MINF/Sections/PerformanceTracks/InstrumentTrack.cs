using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections.PerformanceTracks;

public sealed class InstrumentTrack
{
    public record Event(uint? Tag, int SamplePosition, int Duration,
        byte Pitch, byte Velocity, byte Unknown0, byte Unknown1)
    {
        public float Volume => Velocity / 127f;
    }

    public Event[] Events { get; set; }
    public bool Tagged { get; }

    public InstrumentTrack(BinaryReader reader, uint offset, bool tagged = false)
    {
        Tagged = tagged;
        using var scope = reader.CreateScopeAt(offset);
        var count = reader.ReadUInt32();
        if (count > (reader.Length - reader.Position) / (tagged ? 16 : 12))
            throw new InvalidDataException("Instrument events exceed their subsection.");
        Events = new Event[checked((int)count)];
        for (var i = 0; i < Events.Length; i++)
            Events[i] = new Event(tagged ? reader.ReadUInt32() : null,
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadByte(),
                reader.ReadByte(), reader.ReadByte(), reader.ReadByte());
    }

    public void Write(BinaryWriter writer)
    {
        writer.WriteUInt32(checked((uint)Events.Length));
        foreach (var item in Events)
        {
            if (Tagged) writer.WriteUInt32(item.Tag ?? throw new InvalidDataException("Tagged instrument event is missing its tag."));
            writer.WriteInt32(item.SamplePosition);
            writer.WriteInt32(item.Duration);
            writer.WriteByte(item.Pitch);
            writer.WriteByte(item.Velocity);
            writer.WriteByte(item.Unknown0);
            writer.WriteByte(item.Unknown1);
        }
    }
}

using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections.PerformanceTracks;

public class VocalTrack
{
    public enum VoiceType : byte
    {
        None = 0,
        Unknown = 1,
        A = 2,
        I = 3,
        U = 4,
        E = 5,
        O = 6,
        ILow = 7,
        OLow = 8,
        Unknown2 = 9,
        Whistle = 10
    }
    public class Event
    {
        public int SamplePosition { get; set; }
        public int Duration { get; set; }
        public byte Pitch { get; set; }
        public VoiceType VoiceType { get; set; }
        public byte Velocity { get; set; }
        public float Volume => Velocity / 127f;
        public byte UnknownByte { get; }

        public Event(BinaryReader br)
        {
            SamplePosition = br.ReadInt32();
            Duration = br.ReadInt32();
            VoiceType = (VoiceType)br.ReadByte();
            Pitch = br.ReadByte();
            Velocity = br.ReadByte();
            UnknownByte = br.ReadByte();
        }

        public override string ToString() =>
            $"{VoiceType} ({PitchUtilities.ByteToChord(Pitch)}) | {SamplePosition} | Duration: {Duration} | Velocity: {Velocity}";
    }
    public List<Event> Events { get; set; } = [];
    public int LoopIndex { get; set; } = -1;

    public VocalTrack(BinaryReader reader, uint offset)
    {
        using (reader.CreateScopeAt(offset))
        {
            var notesCount = reader.ReadUInt32();
            LoopIndex = reader.ReadInt32();
            if (notesCount > (reader.Length - reader.Position) / 12)
                throw new InvalidDataException("Vocal events exceed their subsection.");

            for (var i = 0; i < notesCount; i++)
                Events.Add(new Event(reader));

        }
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(checked((uint)Events.Count));
        writer.Write(LoopIndex);
        foreach (var item in Events)
        {
            writer.Write(item.SamplePosition);
            writer.Write(item.Duration);
            writer.Write((byte)item.VoiceType);
            writer.Write(item.Pitch);
            writer.Write(item.Velocity);
            writer.Write(item.UnknownByte);
        }
    }
}

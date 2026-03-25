using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections;

public class SectionA(BinaryReader reader, long location) : SectionBase<SectionA.Entry>(reader, location)
{
    public class Entry
    {
        public uint SamplePos { get; set; }
        public float BPM_Tempo { get; set; }
        public uint Time1 { get; set; }
        public uint Time2 { get; set; }
    }

    protected override Entry ReadEntry(BinaryReader reader)
    {
        return new Entry
        {
            SamplePos = reader.ReadUInt32(),
            BPM_Tempo = reader.ReadSingle(),
            Time1 = reader.ReadUInt32(),
            Time2 = reader.ReadUInt32()
        };
    }

    public override void Write(BinaryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.Write(Count);
        writer.Write(LoopEntry);

        foreach(var entry in Entries)
        {
            writer.Write(entry.SamplePos);
            writer.Write(entry.BPM_Tempo);
            writer.Write(entry.Time1);
            writer.Write(entry.Time2);
        }
    }
}

using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections;

public class SectionD(BinaryReader reader, long offset) : SectionBase<SectionD.Beat>(reader, offset)
{
    public override void Write(BinaryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.Write(Count);
        writer.Write(LoopEntry);

        foreach(var entry in Entries)
        {
            writer.Write(entry.Time);
            writer.Write(entry.Type);
        }
    }

    protected override Beat ReadEntry(BinaryReader reader) => new()
    {
        Time = reader.ReadUInt32(),
        Type = reader.ReadInt32()
    };

    public class Beat
    {
        public uint Time {  get; set; }  
        public int Type { get; set; }
    }
}

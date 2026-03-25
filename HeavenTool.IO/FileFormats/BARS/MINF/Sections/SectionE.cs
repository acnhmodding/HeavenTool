using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections;

public class SectionE(BinaryReader reader, long offset) : SectionBase<uint>(reader, offset)
{
    public override void Write(BinaryWriter writer)
    {
        writer.Write(Count);
        writer.Write(LoopEntry);

        foreach (var entry in Entries)
            writer.Write(entry);
    }

    protected override uint ReadEntry(BinaryReader reader) => reader.ReadUInt32();
}

using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections;

/// <summary>Measure start positions in samples.</summary>
public class MeasureMap(BinaryReader reader, long offset) : SectionBase<uint>(reader, offset)
{
    public override void Write(BinaryWriter writer)
    {
        writer.WriteUInt16(Count);
        writer.WriteInt16(LoopIndex);

        foreach (var entry in Entries)
            writer.WriteUInt32(entry);
    }

    protected override uint ReadEntry(BinaryReader reader) => reader.ReadUInt32();
}
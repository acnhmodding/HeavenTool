using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections;

/// <summary>Beat sample positions and signed beat numbers within each measure.</summary>
public class BeatMap(BinaryReader reader, long offset) : SectionBase<BeatMap.Beat>(reader, offset)
{
    public override void Write(BinaryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteUInt16(Count);
        writer.WriteInt16(LoopIndex);

        foreach (var entry in Entries)
        {
            writer.WriteUInt32(entry.SamplePosition);
            writer.WriteInt32(entry.BeatNumber);
        }
    }

    protected override Beat ReadEntry(BinaryReader reader) => new()
    {
        SamplePosition = reader.ReadUInt32(),
        BeatNumber = reader.ReadInt32()
    };

    public class Beat
    {
        public uint SamplePosition { get; set; }
        public int BeatNumber { get; set; }
    }
}
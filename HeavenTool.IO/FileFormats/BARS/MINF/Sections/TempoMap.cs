using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections;

public class TempoMap(BinaryReader reader, long location) : SectionBase<TempoMap.Entry>(reader, location)
{
    public class Entry
    {
        public uint SamplePosition { get; set; }
        public float BeatsPerMinute { get; set; }
        public ushort TimeSignatureNumerator { get; set; }
        public ushort TimeSignatureDenominator { get; set; }
    }

    protected override Entry ReadEntry(BinaryReader reader)
    {
        return new Entry
        {
            SamplePosition = reader.ReadUInt32(),
            BeatsPerMinute = reader.ReadSingle(),
            TimeSignatureNumerator = reader.ReadUInt16(),
            TimeSignatureDenominator = reader.ReadUInt16()
        };
    }

    public override void Write(BinaryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.Write(Count);
        writer.Write(LoopIndex);

        foreach (var entry in Entries)
        {
            writer.Write(entry.SamplePosition);
            writer.Write(entry.BeatsPerMinute);
            writer.Write(entry.TimeSignatureNumerator);
            writer.Write(entry.TimeSignatureDenominator);
        }
    }
}

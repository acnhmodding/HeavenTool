using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;
using HeavenTool.IO.Audio.Singing;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections.PerformanceTracks;

/// <summary>ACNH step events: sample position and signed modulation value.</summary>
public sealed class PitchValueTrack
{
    public readonly record struct Point(uint SamplePosition, int Value);
    public int LoopIndex { get; set; }
    public Point[] Points { get; set; }

    public PitchValueTrack(BinaryReader reader, uint offset)
    {
        using (reader.CreateScopeAt(offset))
        {
            uint count = reader.ReadUInt32();
            LoopIndex = reader.ReadInt32();
            if (count > (reader.Length - reader.Position) / 8)
                throw new InvalidDataException("Modulation events exceed their track.");
            Points = new Point[checked((int)count)];
            for (int i = 0; i < Points.Length; i++)
            {
                Points[i] = new Point(reader.ReadUInt32(), reader.ReadInt32());
                if (i > 0 && Points[i].SamplePosition < Points[i - 1].SamplePosition)
                    throw new InvalidDataException("ACNH modulation events must be sorted by sample position.");
            }
        }
    }

    public int ValueAt(MusicEventTimeline timeline, MusicEventTimeline.Position position)
    {
        var selected = timeline.Select(position, Points.Length, LoopIndex,
            i => Points[i].SamplePosition, _ => 0);
        // Modulation readers return their current record regardless of the
        // activity flag, so values persist between point events.
        return selected.Current < 0 ? 0 : Points[selected.Current].Value;
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(checked((uint)Points.Length));
        writer.Write(LoopIndex);
        foreach (var point in Points)
        {
            writer.Write(point.SamplePosition);
            writer.Write(point.Value);
        }
    }
}

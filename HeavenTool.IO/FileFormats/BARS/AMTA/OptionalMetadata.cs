using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.AMTA;

public sealed class OptionalMetadata
{
    public record Point(uint SamplePosition, float Value);

    public float? MaxAmplitude { get; set; }
    public float? Unknown01 { get; set; }
    public float? MaxMomentaryLufs { get; set; }
    public float? IntegratedLufs { get; set; }
    public uint? TailLength { get; set; }
    public Point[]? Points { get; set; }
    public ushort PointUnknown { get; set; }
    public float? Unknown06 { get; set; }
    public uint? Unknown07 { get; set; }

    public OptionalDataFlags Flags =>
        (MaxAmplitude.HasValue ? OptionalDataFlags.MaxAmplitude : 0) |
        (Unknown01.HasValue ? OptionalDataFlags.Unknown01 : 0) |
        (MaxMomentaryLufs.HasValue ? OptionalDataFlags.MaxMomentaryLufs : 0) |
        (IntegratedLufs.HasValue ? OptionalDataFlags.IntegratedLufs : 0) |
        (TailLength.HasValue ? OptionalDataFlags.TailLength : 0) |
        (Points != null ? OptionalDataFlags.Points : 0) |
        (Unknown06.HasValue ? OptionalDataFlags.Unknown06 : 0) |
        (Unknown07.HasValue ? OptionalDataFlags.Unknown07 : 0);

    internal static OptionalMetadata Read(BinaryReader reader)
    {
        var flags = (OptionalDataFlags)reader.ReadUInt32();
        if (((uint)flags & ~0xFFu) != 0)
            throw new NotSupportedException($"Unsupported AMTA optional flags: {flags:X}");

        var data = new OptionalMetadata();
        if (flags.HasFlag(OptionalDataFlags.MaxAmplitude)) data.MaxAmplitude = reader.ReadSingle();
        if (flags.HasFlag(OptionalDataFlags.Unknown01)) data.Unknown01 = reader.ReadSingle();
        if (flags.HasFlag(OptionalDataFlags.MaxMomentaryLufs)) data.MaxMomentaryLufs = reader.ReadSingle();
        if (flags.HasFlag(OptionalDataFlags.IntegratedLufs)) data.IntegratedLufs = reader.ReadSingle();
        if (flags.HasFlag(OptionalDataFlags.TailLength)) data.TailLength = reader.ReadUInt32();
        if (flags.HasFlag(OptionalDataFlags.Points))
        {
            data.Points = new Point[reader.ReadUInt16()];
            data.PointUnknown = reader.ReadUInt16();
        }
        if (flags.HasFlag(OptionalDataFlags.Unknown06)) data.Unknown06 = reader.ReadSingle();
        if (flags.HasFlag(OptionalDataFlags.Unknown07)) data.Unknown07 = reader.ReadUInt32();
        if (data.Points != null)
            for (var i = 0; i < data.Points.Length; i++)
                data.Points[i] = new Point(reader.ReadUInt32(), reader.ReadSingle());
        return data;
    }

    internal void Write(BinaryWriter writer)
    {
        writer.Write((uint)Flags);
        if (MaxAmplitude.HasValue) writer.Write(MaxAmplitude.Value);
        if (Unknown01.HasValue) writer.Write(Unknown01.Value);
        if (MaxMomentaryLufs.HasValue) writer.Write(MaxMomentaryLufs.Value);
        if (IntegratedLufs.HasValue) writer.Write(IntegratedLufs.Value);
        if (TailLength.HasValue) writer.Write(TailLength.Value);
        if (Points != null)
        {
            writer.Write(checked((ushort)Points.Length));
            writer.Write(PointUnknown);
        }
        if (Unknown06.HasValue) writer.Write(Unknown06.Value);
        if (Unknown07.HasValue) writer.Write(Unknown07.Value);
        if (Points != null)
            foreach (var point in Points)
            {
                writer.Write(point.SamplePosition);
                writer.Write(point.Value);
            }
    }
}

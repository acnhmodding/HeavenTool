using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections;

public abstract class SectionBase<TEntry>
{
    public ushort Count => checked((ushort)Entries.Count);
    public short LoopIndex { get; protected set; }

    public List<TEntry> Entries { get; } = [];

    protected SectionBase(BinaryReader reader, long offset)
    {
        using (reader.CreateScopeAt(offset))
        {
            var count = reader.ReadUInt16();
            LoopIndex = reader.ReadInt16();

            for (int i = 0; i < count; i++)
                Entries.Add(ReadEntry(reader));
        }
    }

    protected abstract TEntry ReadEntry(BinaryReader reader);
    public abstract void Write(BinaryWriter writer);
}

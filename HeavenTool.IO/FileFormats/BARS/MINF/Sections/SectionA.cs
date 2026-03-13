using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections;

public class SectionA
{
    private short unk;

    public class Entry
    {
        public uint SamplePos { get; set; }
        public float BPM_Tempo { get; set; }
        public uint Time1 { get; set; }
        public uint Time2 { get; set; }
    }

    public List<Entry> Entries { get; private set; } = [];

    public SectionA(BinaryReader reader, long location)
    {
        ArgumentNullException.ThrowIfNull(reader);

        using (reader.CreateScopeAt(location))
        {
            var count = reader.ReadUInt16();
            unk = reader.ReadInt16();

            for (var i = 0; i < count; i++)
            {
                var entry = new Entry
                {
                    SamplePos = reader.ReadUInt32(),
                    BPM_Tempo = reader.ReadSingle(),
                    Time1 = reader.ReadUInt32(),
                    Time2 = reader.ReadUInt32()
                };

                Entries.Add(entry);
            }
        }
    }
}

using System.Collections;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS;

public class MarkerList : IEnumerable<MarkerList.MarkerEntry>
{
    public class MarkerEntry
    {
        public uint Id { get; set; }
        public required string Name { get; set; } // Name need to be registered into the string pool when writing
        public uint StartPosition { get; set; }
        public uint Length { get; set; }

        public void Write(BinaryWriter writer, StringPool stringPool)
        {
            writer.Write(Id);
            stringPool.AddString(writer, Name);
            writer.Write(StartPosition);
            writer.Write(Length);
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public List<MarkerEntry> Markers { get; private set; } = [];

    public MarkerList(BinaryReader reader, long location)
    {
        ArgumentNullException.ThrowIfNull(reader);

        using (reader.CreateScopeAt(location))
        {
            var entryCount = reader.ReadUInt32();

            for (int i = 0; i < entryCount; i++)
            {
                var entry = new MarkerEntry
                {
                    Id = reader.ReadUInt32(),
                    Name = reader.ReadStringPointer(),
                    StartPosition = reader.ReadUInt32(),
                    Length = reader.ReadUInt32()
                };

                Markers.Add(entry);
            }
        }
    }

    public void Write(BinaryWriter writer, StringPool stringPool)
    {
        writer.Write(Markers.Count);

        foreach (var entry in Markers)
            entry.Write(writer, stringPool);
    }

    public IEnumerator<MarkerEntry> GetEnumerator()
    {
        return Markers.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
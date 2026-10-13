using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections;

public sealed class NamedMarkerTracks
{
    public sealed class Track
    {
        private string name = "";
        public uint NameHash { get; set; }
        public string Name
        {
            get => name;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                if (name == value) return;
                name = value;
                NameHash = value.ToMurmur();
            }
        }
        public int LoopIndex { get; set; } = -1;
        public List<uint> SamplePositions { get; } = [];
        public override string ToString() => Name;
    }

    public List<Track> Tracks { get; } = [];
    internal List<long> NameOffsets { get; } = [];

    public NamedMarkerTracks() { }

    public NamedMarkerTracks(BinaryReader reader, long offset)
    {
        using var scope = reader.CreateScopeAt(offset);
        uint count = reader.ReadUInt32();
        if (count > (reader.Length - reader.Position) / 8)
            throw new InvalidDataException("Named marker table is truncated.");
        for (uint i = 0; i < count; i++)
        {
            uint nameHash = reader.ReadUInt32();
            var track = new Track();
            long field = reader.Position;
            long target = field + reader.ReadUInt32();
            if (target < offset + 4 + count * 8L || target > reader.Length - 8)
                throw new InvalidDataException("Named marker pointer is outside its payload.");
            using (reader.CreateScopeAt(target))
            {
                long name = reader.Position + reader.ReadUInt32();
                if (name < 0x4c || name >= reader.Length)
                    throw new InvalidDataException("Named marker name is outside MINF.");
                NameOffsets.Add(name);
                reader.Position = target;
                track.Name = reader.ReadStringPointer();
                track.NameHash = nameHash;
                ushort events = reader.ReadUInt16();
                ushort loop = reader.ReadUInt16();
                track.LoopIndex = loop == ushort.MaxValue ? -1 : loop;
                if (events > (reader.Length - reader.Position) / 4)
                    throw new InvalidDataException("Named marker events are truncated.");
                for (int j = 0; j < events; j++) track.SamplePositions.Add(reader.ReadUInt32());
            }
            Tracks.Add(track);
        }
    }

    public void Write(BinaryWriter writer, StringPool strings)
    {
        writer.Write(checked((uint)Tracks.Count));
        var pointers = new List<WriterScopePointer>();
        foreach (var track in Tracks)
        {
            writer.Write(track.NameHash);
            pointers.Add(writer.CreatePointer());
        }
        for (int i = 0; i < Tracks.Count; i++)
            pointers[i].Resolve(w =>
            {
                var track = Tracks[i];
                strings.AddString(w, track.Name);
                w.Write(checked((ushort)track.SamplePositions.Count));
                w.Write(checked((ushort)(track.LoopIndex == -1 ? ushort.MaxValue : track.LoopIndex)));
                foreach (uint position in track.SamplePositions) w.Write(position);
            });
    }
}

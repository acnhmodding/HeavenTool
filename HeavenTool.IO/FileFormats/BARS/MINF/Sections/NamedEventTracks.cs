using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections;

public sealed class NamedEventTracks
{
    public readonly record struct Point(uint SamplePosition, int Value);

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
        public int StartLoopIndex { get; set; } = -1;
        public int EndLoopIndex { get; set; } = -1;
        public List<Point> Starts { get; } = [];
        public List<Point> Ends { get; } = [];
        public override string ToString() => Name;
    }

    public List<Track> Tracks { get; } = [];
    internal List<long> NameOffsets { get; } = [];

    public NamedEventTracks() { }

    public NamedEventTracks(BinaryReader reader, long offset)
    {
        using var scope = reader.CreateScopeAt(offset);
        var count = reader.ReadUInt32();
        if (count > (reader.Length - reader.Position) / 8)
            throw new InvalidDataException("Named event table is truncated.");
        for (uint i = 0; i < count; i++)
        {
            var nameHash = reader.ReadUInt32();
            var track = new Track();
            var field = reader.Position;
            var target = field + reader.ReadUInt32();
            if (target < offset + 4 + count * 8L || target > reader.Length - 12)
                throw new InvalidDataException("Named event pointer is outside its payload.");
            using (reader.CreateScopeAt(target))
            {
                var name = reader.Position + reader.ReadUInt32();
                if (name < 0x4c || name >= reader.Length)
                    throw new InvalidDataException("Named event name is outside MINF.");
                NameOffsets.Add(name);
                reader.Position = target;
                track.Name = reader.ReadStringPointer();
                track.NameHash = nameHash;
                field = reader.Position;
                var ends = field + reader.ReadUInt32();
                track.StartLoopIndex = ReadPoints(reader, track.Starts);
                if (ends < target + 8 || ends > reader.Length - 4)
                    throw new InvalidDataException("Named event end pointer is outside its payload.");
                using var endScope = reader.CreateScopeAt(ends);
                track.EndLoopIndex = ReadPoints(reader, track.Ends);
            }
            Tracks.Add(track);
        }
    }

    private static int ReadPoints(BinaryReader reader, List<Point> points)
    {
        var count = reader.ReadUInt16();
        var loop = reader.ReadUInt16();
        if (count > (reader.Length - reader.Position) / 8)
            throw new InvalidDataException("Named event points are truncated.");
        for (var i = 0; i < count; i++) points.Add(new Point(reader.ReadUInt32(), reader.ReadInt32()));
        return loop == ushort.MaxValue ? -1 : loop;
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
        for (var i = 0; i < Tracks.Count; i++)
            pointers[i].Resolve(w =>
            {
                var track = Tracks[i];
                strings.AddString(w, track.Name);
                var ends = w.CreatePointer();
                WritePoints(w, track.StartLoopIndex, track.Starts);
                ends.Resolve(e => WritePoints(e, track.EndLoopIndex, track.Ends));
            });
    }

    private static void WritePoints(BinaryWriter writer, int loop, List<Point> points)
    {
        writer.Write(checked((ushort)points.Count));
        writer.Write(checked((ushort)(loop == -1 ? ushort.MaxValue : loop)));
        foreach (var point in points)
        {
            writer.Write(point.SamplePosition);
            writer.Write(point.Value);
        }
    }
}

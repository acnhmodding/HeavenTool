using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections;

public class ChordTrack(BinaryReader reader, long offset) : SectionBase<ChordTrack.Chord>(reader, offset)
{
    public class Chord
    {
        public uint SamplePosition { get; set; }
        public List<byte> Notes { get; set; } = [];
        public string[] Chords => [.. Notes.Select(PitchUtilities.ByteToChord)];

        public Chord(BinaryReader reader)
        {
            SamplePosition = reader.ReadUInt32();
            var chordOffset = reader.Position + reader.ReadUInt32();

            using (reader.CreateScopeAt(chordOffset))
            {
                var chordCount = reader.ReadUInt32();

                for (var i = 0; i < chordCount; i++)
                    Notes.Add(reader.ReadByte());


                reader.Align(4);
            }
        }

        private WriterScopePointer? chordPointer;

        public void Write(BinaryWriter writer)
        {
            writer.Write(SamplePosition);
            chordPointer = writer.CreatePointer();
        }

        public void WriteChord()
        {
            chordPointer?.Resolve(writer =>
            {
                writer.Write(Notes.Count);

                foreach (var note in Notes)
                    writer.Write(note);

                writer.Align(4);
            });
        }

        public override string ToString()
        {
            if (Notes.Count == 0)
                return "<< Empty >>";
            return string.Join(", ", Chords);
        }
    }

    protected override Chord ReadEntry(BinaryReader reader) => new(reader);

    public override void Write(BinaryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.Write(Count);
        writer.Write(LoopIndex);

        // Write all entry pointers before their chord data.
        foreach (var chord in Entries)
            chord.Write(writer);

        foreach (var chord in Entries)
            chord.WriteChord();
    }
}
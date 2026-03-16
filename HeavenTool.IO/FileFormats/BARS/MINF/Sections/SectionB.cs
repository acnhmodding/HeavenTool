using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections;

public class SectionB
{
    public class Chord
    {
        public uint SamplePos { get; set; }
        public List<string> Chords { get; set; } = [];

        public Chord(BinaryReader reader)
        {
            SamplePos = reader.ReadUInt32();
            var chordOffset = reader.Position + reader.ReadUInt32();

            using (reader.CreateScopeAt(chordOffset))
            {
                var chordCount = reader.ReadUInt32();

                for (var i = 0; i < chordCount; i++)
                {
                    var b = reader.ReadByte();
                    var chord = PitchUtilities.ByteToChord(b);

                    Chords.Add(chord);
                }

                reader.Align(4);
            }
        }

        private WriterScopePointer? chordPointer;

        public void Write(BinaryWriter writer)
        {
            writer.Write(SamplePos);
            chordPointer = writer.CreatePointer();

        }

        public void WriteChord()
        {
            chordPointer?.Resolve(writer =>
            {
                writer.Write(Chords.Count);

                foreach (var chord in Chords)
                {
                    var b = PitchUtilities.ChordToByte(chord);
                    writer.Write(b);
                }

                writer.Align(4);
            });
        }

        public override string ToString()
        {
            if (Chords.Count == 0)
                return "<< Empty >>";
            return string.Join(", ", Chords);
        }
    }

    public List<Chord> Chords { get; private set; } = [];

    public short Unknown { get; private set; }

    public SectionB(BinaryReader reader, long offset)
    {
        Chords = [];

        using (reader.CreateScopeAt(offset))
        {
            var count = reader.ReadUInt16();
            Unknown = reader.ReadInt16(); // i really want to know wtf this do

            for (var i = 0; i < count; i++)
                Chords.Add(new Chord(reader));

        }
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write((ushort)Chords.Count);
        writer.Write(Unknown);

        // First, we write the chord entries, which will create pointers for the chord data
        foreach (var chord in Chords)
            chord.Write(writer);

        // After writing all chords, we need to write the chord data
        foreach (var chord in Chords)
            chord.WriteChord();
    }
}
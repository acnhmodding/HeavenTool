using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections.SectionH_SubSections;

public class NotesSubSection
{
    public enum Vowel : byte
    {
        None = 0,
        Unknown = 1,
        A = 2,
        I = 3,
        U = 4,
        E = 5,
        O = 6,
        ILow = 7,
        OLow = 8,
        Unknown2 = 9,
        Whstle = 10
    }


    public class Note
    {
        public int Start { get; set; }
        public int Length { get; set; }
        public byte Pitch { get; set; }
        public Vowel Vowel { get; set; }
        public float Volume { get; set; }
        public byte End { get; }

        public Note(BinaryReader br)
        {
            Start = br.ReadInt32();
            Length = br.ReadInt32();
            Vowel = (Vowel)br.ReadByte();
            Pitch = br.ReadByte();
            int volume = br.ReadByte();
            End = br.ReadByte(); // unused like TS version

            var newVolume = volume / 127 * 100;
            Volume = newVolume / 100f;
        }

        public override string ToString()
        {
            return $"{Vowel} ({PitchUtilities.ByteToChord(Pitch)}) | {Start} | Length: {Length} | Volume: {Volume}";
        }
    }

    public List<Note> Notes { get; set; } = [];
    public int Unknown {  get; set; }

    public NotesSubSection(BinaryReader reader, uint offset)
    {
        using (reader.CreateScopeAt(offset))
        {
            var notesCount = reader.ReadUInt32();
            Unknown = reader.ReadInt32();

            for (var i = 0; i < notesCount; i++)
                Notes.Add(new Note(reader));
            
        }
    }
}

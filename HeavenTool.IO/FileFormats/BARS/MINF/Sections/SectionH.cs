using HeavenTool.IO.FileFormats.BARS.MINF.Sections.SectionH_SubSections;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.MINF.Sections;

/// <summary>
/// SectionH contains 4 sub-sections:
/// <see cref="NotesSubSection"/>, ...
/// </summary>
public class SectionH
{
    public uint Sections { get; set; }

    public NotesSubSection? Notes { get; set; }

    public SectionH(BinaryReader reader, long offset)
    {
        using (reader.CreateScopeAt(offset)) {
            Sections = reader.ReadUInt32();

            var offsets = new uint[Sections];

            for (uint i = 0; i < Sections; i++)
                offsets[i] = (uint) reader.Position + reader.ReadUInt32();
            

            if (Sections > 0)
                Notes = new NotesSubSection(reader, offsets[0]);
        }
    }

    public void Write(BinaryWriter writer)
    {
        // TODO: Finish write method
        writer.Write(Sections);
    }
}

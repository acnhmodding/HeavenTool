using HeavenTool.IO.FileFormats.BARS.MINF.Sections;
using System.ComponentModel;

using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS.MINF;

public class MINFReader
{
    public const string MAGIC = "MINF";

    [Browsable(false)]
    public SectionA? SectionA { get; private set; }

    [Browsable(false)]
    public SectionB? SectionB { get; private set; } 

    [Browsable(false)]
    public SectionH? SectionH { get; private set; } 

    public MINFReader(BinaryReader reader, long location)
    {
        ArgumentNullException.ThrowIfNull(reader);

        using (reader.CreateScopeAt(location))
        {
            var magic = reader.ReadString(4);
            if (magic != MAGIC)
                throw new Exception($"Expected \"{MAGIC}\" magic, but got \"{magic}\".");

            var endian = reader.ReadUInt16();

            reader.BigEndian = BigEndian = endian != 0xFEFF;
            VersionMajor = reader.ReadByte();
            VersionMinor = reader.ReadByte();
            var minfSize = reader.ReadUInt32();

            EnglishName = reader.ReadStringPointer();
            JapaneseName = reader.ReadStringPointer();
            SampleRate = reader.ReadUInt32();

            LoopStart = reader.ReadUInt32();
            LoopEnd = reader.ReadUInt32();

            Unk1 = reader.ReadUInt32();

            BPM = reader.ReadSingle();

            Unk2 = reader.ReadUInt16();
            Unk3 = reader.ReadUInt16();

            var sectionA = reader.ReadUInt32();
            if (sectionA != 0) SectionA = new SectionA(reader, reader.Position + sectionA - 4);

            var sectionB = reader.ReadUInt32();
            if (sectionB != 0) SectionB = new SectionB(reader, reader.Position + sectionB - 4);

            _ = reader.ReadUInt32(); // Section C is never used in ACNH

            var sectionD = reader.ReadUInt32();
            var sectionE = reader.ReadUInt32();
            var sectionF = reader.ReadUInt32();
            var sectionG = reader.ReadUInt32();
            var sectionH = reader.ReadUInt32();
            if (sectionH != 0) SectionH = new SectionH(reader, reader.Position + sectionH - 4);
        }

    }

    public void Write(BinaryWriter writer, StringPool stringPool)
    {
        var start = (uint) writer.Position;
        writer.Write(MAGIC);
        writer.Write(0xFEFF);
        writer.Write(VersionMajor);
        writer.Write(VersionMinor);

        var size = writer.CreatePointer();

        stringPool.AddString(writer, EnglishName);
        stringPool.AddString(writer, JapaneseName);

        writer.Write(SampleRate);
        writer.Write(LoopStart);
        writer.Write(LoopEnd);
        writer.Write(Unk1);
        writer.Write(BPM);
        writer.Write(Unk2);
        writer.Write(Unk3);

        var sectionA = writer.CreatePointer();
        var sectionB = writer.CreatePointer();
        var sectionC = writer.CreatePointer();
        var sectionD = writer.CreatePointer();
        var sectionE = writer.CreatePointer();
        var sectionF = writer.CreatePointer();
        var sectionG = writer.CreatePointer();
        var sectionH = writer.CreatePointer();

        if (SectionA != null)
            sectionA.Resolve(w =>
            {

            });

        if (SectionB != null)
            sectionB.Resolve(SectionB.Write);

        if (SectionH != null)
            sectionH.Resolve(SectionH.Write);

        size.Resolve((uint)writer.Position - start);
    }

#if DEBUG
    [Category("Offsets")]
#else
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
#endif
    public bool BigEndian { get; }

    [Category("Header")]

    public byte VersionMajor { get; }
    [Category("Header")]

    public byte VersionMinor { get; }
    [Category("Header")]

    public string EnglishName { get; }
    [Category("Header")]
    public string JapaneseName { get; }

    [Category("Audio")]
    public uint SampleRate { get; }

    [Category("Audio")]
    public uint LoopStart { get; }

    [Category("Audio")]
    public uint LoopEnd { get; }

    [Category("Unknown")]
    public uint Unk1 { get; private set; }

    [Category("Audio")]
    public float BPM { get; }

    [Category("Unknown")]
    public ushort Unk2 { get; }

    [Category("Unknown")]
    public ushort Unk3 { get; }
}

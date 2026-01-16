using HeavenTool.IO;

namespace HeavenTool.ModManager.FileTypes;

public class GenericFile(Stream stream, string name) : ModFile(stream, name)
{
    public byte[] Bytes { get; internal set; } = stream.ToArray();

    public override void DoDiff(ModFile file)
    {
        if (file is not GenericFile otherFile)
            throw new ArgumentException("File type mismatch", nameof(file));

        Console.WriteLine("Attempt to merge two generic (or not supported) files.");

        Bytes = otherFile.Bytes;
    }

    public override byte[] SaveFile()
    {
        return Bytes;
    }
}

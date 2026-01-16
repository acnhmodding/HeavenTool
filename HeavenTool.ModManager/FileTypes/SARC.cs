using HeavenTool.IO;
using AeonSake.NintendoTools.FileFormats.Msbt;
using AeonSake.NintendoTools.FileFormats.Sarc;

namespace HeavenTool.ModManager.FileTypes;

public sealed class SARC : ModFile
{
    internal SarcFile SarcFile { get; set; }

    public Dictionary<string, object> Files { get; set; } = [];

    public SARC(Stream stream, string name) : base(stream, name)
    {
        if (Content != null && SarcFileReader.CanReadStatic(Content))
        {
            var loadedFile = new SarcFileReader().Read(Content);
          
            // we are not using Content ever again in this file, lets free some memory
            Content.Dispose();

            SarcFile = new SarcFile()
            {
                BigEndian = loadedFile.BigEndian,
                HashKey = loadedFile.HashKey,
                Version = loadedFile.Version,
                HasFileNames = loadedFile.HasFileNames,
            };

            for (int i = 0; i < loadedFile.Files.Count; i++)
            {
                SarcContent file = loadedFile.Files[i];
                var fileData = new MemoryStream(file.Data);

                var fileName = loadedFile.HasFileNames ? file.Name : i.ToString();

                if (MsbtFileReader.CanReadStatic(fileData))
                    Files.TryAdd(fileName, new MSBT(fileData, file.Name));

                else // if its not a mod-manager compatible, just add as SarcContent
                    Files.Add(fileName, file);
            }
        }
    }

    public override void DoDiff(ModFile otherFile)
    {
        if (otherFile is not SARC otherSarc)
            throw new ArgumentException("File type mismatch", nameof(otherFile));

        if (Name != otherSarc.Name) return;

        foreach (var (fileName, file) in otherSarc.Files)
        {
            if (file is SarcContent sarcContent)
            {
                // if its a SarcContent means that we don't support merging, so lets just replace or add as new
                if (Files.ContainsKey(fileName))
                    Files[fileName] = sarcContent;
                else Files.Add(fileName, sarcContent);
            }
            else if (file is MSBT msbtFile)
            {
                if (Files.TryGetValue(fileName, out object v))
                {
                    if (v is MSBT currentMSBT) currentMSBT.DoDiff(msbtFile);
                }
                else Files.Add(fileName, msbtFile);
            }
        }
    }

    public override byte[] SaveFile()
    {
        if (SarcFile == null) throw new Exception("SarcFile is not loaded");

        foreach (var (fileName, file) in Files)
        {
            // if its a mod file, convert to SarcContent
            if (file is ModFile modFile)
            {
                var content = new SarcContent()
                {
                    Name = fileName,
                    Data = modFile.SaveFile()
                };

                SarcFile.Files.Add(content);
            } // otherwise just add it to the list
            else if (file is SarcContent sarcContent)
            {
                SarcFile.Files.Add(sarcContent);
            }
            else throw new Exception($"FileType {file.GetType()} is not compatible."); // should never occur but who knows

        }

        var alignmentTable = new AeonSake.NintendoTools.FileFormats.AlignmentTable()
        {
            Default = 0x08,
        };

        (string, int)[] extensionAlignments = [
            (".bgenv", 0x04),
            (".bfcpx", 0x10),
            (".bflan", 0x10),
            (".bflyt", 0x10),
            (".bushvt", 0x10),
            (".glsl", 0x10),
            (".byml", 0x20),
            (".pbc", 0x80),
            (".belnk", 0x100),
            (".msbt", 0x100),
            (".barslist", 0x100),
            (".bnsh", 0x1000),
            (".bntx", 0x1000),
            (".sharcb", 0x1000),
            (".arc", 0x2000),
            (".baglmf", 0x2000),
            (".bffnt", 0x2000),
            (".bfotf", 0x2000),
            (".bfres", 0x2000),
            (".bfsha", 0x2000),
            (".bfttf", 0x2000),
            (".bphcw", 0x2000),
            (".bphlik", 0x2000),
            (".genvb", 0x2000),
            (".genvres", 0x2000),
            (".phive", 0x2000),
            (".ptcl", 0x4000)
        ];

        foreach(var (extension, alignment) in extensionAlignments)
            alignmentTable.Add(extension, alignment);

        var sarcFileCompiler = new SarcFileWriter()
        {
            Alignment = alignmentTable
        };
        var memoryStream = new MemoryStream();

        sarcFileCompiler.Write(SarcFile, memoryStream);

        return memoryStream.ToArray();
    }

    public override void ExportToJson(string outputPath)
    {
        foreach (var (_, file) in Files) 
        { 
            if (file is MSBT msbtFile)
            {
                var path = Path.Combine(outputPath, Path.GetFileName(Name));
                msbtFile.ExportToJson(path);
            }
        }
    }
}

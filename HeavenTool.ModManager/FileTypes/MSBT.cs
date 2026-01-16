using HeavenTool.IO;
using Newtonsoft.Json;
using NintendoTools.FileFormats.Msbt;

namespace HeavenTool.ModManager.FileTypes;

public sealed class MSBT : ModFile
{
    [JsonIgnore]
    public MsbtFile MsbtFile { get; set; }

    public MSBT(Stream stream, string name) : base(stream, name)
    {
        if (Content != null && MsbtFileParser.CanParseStatic(Content))
        {
            MsbtFile = new MsbtFileParser().Parse(Content);
            Content.Dispose(); // dispose content to free some memory
        }
    }

    //public Dictionary<string, MsbtMessage> AddedEntries { get; set; } = [];
    public Dictionary<string, MsbtMessage> ChangedEntries { get; set; } = [];
    public List<string> RemovedEntries { get; set; } = [];

    public override void DoDiff(ModFile file)
    {
        if (file is not MSBT otherFile)
            throw new ArgumentException("File type mismatch", nameof(file));

        if (MsbtFile == null || otherFile == null || otherFile.MsbtFile == null) return;

        var otherMsbt = otherFile.MsbtFile;

        // ACNH uses LBL1 (label) to differentiate entries, so we test if target files uses LBL1
        if (!MsbtFile.HasLbl1 || !otherMsbt.HasLbl1) return;

        // Files have different encoding
        if (MsbtFile.Encoding != otherMsbt.Encoding ||
            MsbtFile.BigEndian != otherMsbt.BigEndian) return;

        // Dictionary based on Label
        var currentFileEntries = MsbtFile.Messages.DistinctBy(x => x.Label).ToDictionary(x => x.Label, y => y);
        var otherFileEntries = otherMsbt.Messages.DistinctBy(x => x.Label).ToDictionary(x => x.Label, y => y);

        // Check for changes or new entries
        foreach (var entry in otherFileEntries)
        {
            if (!currentFileEntries.ContainsKey(entry.Key) || 
                (currentFileEntries.TryGetValue(entry.Key, out MsbtMessage val) && entry.Value.Text != val.Text))
            {
                ChangedEntries[entry.Key] = entry.Value;
            }
        }

        // Check for removed entries
        foreach (var entry in currentFileEntries)
            if (!otherFileEntries.ContainsKey(entry.Key))
                RemovedEntries.Add(entry.Key);
    }

    /// <summary>
    /// Apply our changes (<see cref="ChangedEntries"/> and <see cref="RemovedEntries"/>) into <see cref="MsbtFile"/>
    /// </summary>
    public void BakeFile()
    {
        if (ChangedEntries.Count > 0 ||  RemovedEntries.Count > 0)
            ConsoleUtilities.WriteLine($"[MSBT] {Name} - {ChangedEntries.Count} changes | {RemovedEntries.Count} removed", ConsoleColor.Gray);

        // If some other mod changes a entry that is being removed, then this entry should not be removed
        RemovedEntries.RemoveAll(ChangedEntries.ContainsKey);

        // Changed entries
        foreach (var entry in ChangedEntries)
        {
            var entryIndex = MsbtFile.Messages.FindIndex(x => x.Label == entry.Key);
            if (entryIndex >= 0)
                // If we found a entry that have this label, we just replace it with the changed entry.
                MsbtFile.Messages[entryIndex] = entry.Value;

            else 
                // If we don't found a entry with this label, add a new one 
                MsbtFile.Messages.Add(entry.Value);
        }

        // Removed entries
        MsbtFile.Messages.RemoveAll(x => RemovedEntries.Contains(x.Label));
    }

    public override byte[] SaveFile()
    {
        BakeFile();

        // Save msbt file into bytes
        var compiler = new MsbtFileCompiler();
        using var memoryStream = new MemoryStream();

        compiler.Compile(MsbtFile, memoryStream);

        return memoryStream.ToArray();
    }
}

using HeavenTool.IO;
using HeavenTool.IO.FileFormats.BCSV;

namespace HeavenTool.ModManager.FileTypes;

public sealed class BCSV : ModFile
{
    public BinaryCSV LoadedFile { get; set; }
    public uint? UniqueHeader {  get; set; } = null;
    public int UniqueHeaderIndex { get; set; }

    public BCSV(Stream stream, string name) : base(stream, name)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (stream is MemoryStream memoryStream)
        {
            var bytes = memoryStream.ToArray();

            LoadedFile = new BinaryCSV(bytes);
            if (LoadedFile.Length == 0 || LoadedFile.Fields.Length == 0) return;

            UniqueHeader = BinaryCSV.UniqueHashes.FirstOrNullStruct(x => LoadedFile.Fields.Any(field => field.Hash == x));
            UniqueHeaderIndex = UniqueHeader.HasValue ? Array.FindIndex(LoadedFile.Fields, x => x.Hash == UniqueHeader) : -1;
        }
    }

    internal Dictionary<object, object[]> Changes = [];
    internal Dictionary<object, object[]> Additions = [];
    internal List<object> Removes = [];

    public override void DoDiff(ModFile otherFile)
    {
        if (otherFile == null) return;

        if (otherFile is not BCSV otherBCSV)
            throw new ArgumentException("File type mismatch", nameof(otherFile));

        if (LoadedFile == null ||  otherBCSV.LoadedFile == null || 
            Name != otherBCSV.Name ||
            !LoadedFile.Fields.SequenceEqual(otherBCSV.LoadedFile.Fields)) return;

        if (UniqueHeaderIndex == -1)
        {
            ConsoleUtilities.WriteLine($"[BCSV] File {Name} does not support merging. This file doesn't contain a Unique Header.", ConsoleColor.Red);
            return;
        }

        try
        {
            var currentFileEntries = LoadedFile.Entries.ToDictionary(x => x[UniqueHeaderIndex], y => y);
            var otherFileEntries = otherBCSV.LoadedFile.Entries.ToDictionary(x => x[UniqueHeaderIndex], y => y);

            foreach (var (uniqueValue, _) in currentFileEntries)
                if (!otherFileEntries.ContainsKey(uniqueValue) && !Removes.Contains(uniqueValue))
                    Removes.Add(uniqueValue);

            foreach (var (uniqueValue, otherValues) in otherFileEntries)
            {
                if (!currentFileEntries.TryGetValue(uniqueValue, out object[] currentValues))
                {
                    if (Additions.ContainsKey(uniqueValue))
                        ConsoleUtilities.WriteLine($"[BCSV] Conflict! Two mods tried to add a entry with same Unique Key in {Name} (UniqueHeader: {UniqueHeader:x} | Value {uniqueValue})", ConsoleColor.Red);
                    Additions[uniqueValue] = otherValues;
                } 
                else if (!otherValues.SequenceEqual(currentValues))
                {
                    if (Changes.ContainsKey(uniqueValue))
                        ConsoleUtilities.WriteLine($"[BCSV] Conflict in file {Name} (UniqueHeader: {UniqueHeader:x} | Value {uniqueValue})", ConsoleColor.Yellow);
                    Changes[uniqueValue] = otherValues;
                }
            }
        }
        catch (Exception ex) 
        {
            ConsoleUtilities.WriteLine($"[BCSV] Failed to merge {Name} (probably there is one or more entries using the same unique-key)\n{ex}\n", ConsoleColor.Red);
        }
    }

    public override byte[] SaveFile()
    {
        if (LoadedFile == null) 
            return null;

        BakeFile();

        return LoadedFile.Save();
    }

    private void BakeFile() 
    {
        if (UniqueHeaderIndex == -1) return;
        
        Removes.RemoveAll(Changes.ContainsKey);

        ConsoleUtilities.WriteLine($"Baking file {Name} | {Additions.Count} additions, {Changes.Count} changes and {Removes.Count} removes", ConsoleColor.Green);

        if (Changes.Count > 0)
        {
            for (int entryIndex = 0; entryIndex < LoadedFile.Length; entryIndex++)
            {
                if (LoadedFile[entryIndex, UniqueHeaderIndex] is object value && Changes.TryGetValue(value, out var values))
                {
                    if (values.Length != LoadedFile.Fields.Length) continue;

                    for (int i = 0; i < values.Length; i++)
                        LoadedFile[entryIndex, i] = values[i];
                    
                }
            }
        }

        foreach (var (uniqueId, values) in Additions)
            LoadedFile.Entries.Add(values);

        LoadedFile.Entries.RemoveAll(entryValues =>
        {
            var uniqueValue = entryValues[UniqueHeaderIndex];

            return Removes.Contains(uniqueValue);
        });
    }
}

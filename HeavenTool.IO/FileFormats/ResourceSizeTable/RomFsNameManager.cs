
namespace HeavenTool.IO.FileFormats.ResourceSizeTable;

public static class RomFsNameManager
{
    private static readonly Dictionary<uint, string> Hashes = [];

    private static readonly string ExtraFolder = "extra";
    private static readonly string FileLocation = Path.Combine(ExtraFolder, "romfs-files.txt");

    static RomFsNameManager()
    {
        Initialize();
    }

    private static void Initialize()
    {
        Directory.CreateDirectory(ExtraFolder);

        if (!File.Exists(FileLocation))
        {
            File.WriteAllText(FileLocation, string.Empty);
            return;
        }

        Hashes.Clear();

        foreach (var line in File.ReadLines(FileLocation))
        {
            AddHash(line);
        }
    }

    private static void AddHash(string value)
    {
        var hash = value.ToCRC32();

        if (!Hashes.TryAdd(hash, value))
            Hashes[hash] = "";  // Mark as duplicate
    }

    public static string GetValue(uint hash)
    {
        return Hashes.TryGetValue(hash, out var value) && !string.IsNullOrEmpty(value) ? value : $"0x{hash:x}";
    }

    public static void Update(string[] files)
    {
        Directory.CreateDirectory(ExtraFolder);
        File.WriteAllLines(FileLocation, files);

        Initialize();
    }
}

namespace HeavenTool.IO.FileFormats.ResourceSizeTable;

public static class RomFsNameManager
{
    private static Dictionary<uint, string> Hashes { get; set; }

    private static bool _isInitialized;

    private static void Initialize()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;

        string fileLocation = Path.Combine("extra", "romfs-files.txt");
        Hashes = [];

        if (File.Exists(fileLocation))
        {
            var lines = File.ReadAllLines(fileLocation);

            foreach (var line in lines)
            {
                var hash = line.ToCRC32();

                if (!Hashes.TryGetValue(hash, out string value))
                    Hashes.Add(hash, line);
                else if (value != "")
                    Hashes[hash] = "";

            }
        }
        else
        {
            // Create file if it doesn't exist
            File.WriteAllText(fileLocation, "");
        }
    }

    public static string GetValue(uint hash)
    {
        if (!_isInitialized) Initialize();

        var myString = Hashes.TryGetValue(hash, out string value) ? value : null;

        if (string.IsNullOrEmpty(myString)) myString = null;

        return myString ?? $"0x{hash:x}";
    }
}

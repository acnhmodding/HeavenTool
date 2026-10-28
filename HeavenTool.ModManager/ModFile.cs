

using HeavenTool.IO;
using Newtonsoft.Json;

namespace HeavenTool.ModManager
{
    public abstract class ModFile
    {
        public ModFile(Stream stream, string name)
        {
            Name = name;
            Content = stream;
        }

        public ModFile(string path)
        {
            var directoryFile = new FileInfo(path);

            Name = directoryFile.Name;
            //Extension = directoryFile.Extension;
            Content = File.OpenRead(path);
        }

        public string Name { get; set; }

        [JsonIgnore]
        public Stream Content { get; set; }
        public Compression Compression { get; set; }

        public abstract byte[] SaveFile();
        public abstract void DoDiff(ModFile otherFile);

        /// <summary>
        /// Whether <see cref="DoDiff"/> can take the changes in <paramref name="otherFile"/>, rather than skip them.
        /// </summary>
        public virtual bool CanMerge(ModFile otherFile) => otherFile?.GetType() == GetType();

        public virtual void ExportToJson(string outputPath)
        {
            ConsoleUtilities.WriteLine($"Exporting: {Name}", ConsoleColor.Gray);
            var path = Path.Combine(outputPath, Name);
            var json = JsonConvert.SerializeObject(this);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
        }
    }

    public enum Compression
    {
        None,
        Zstd
    }
}
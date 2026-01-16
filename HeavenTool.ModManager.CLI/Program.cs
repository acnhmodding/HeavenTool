using CommandLine;
using HeavenTool.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HeavenTool.ModManager.CLI
{
    public class Options
    {
        [Option('e', "export", Required = false, HelpText = "Export diff to a .json file instead of merging.")]
        public bool ExportDiffOnly { get; set; }

        [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
        public bool Verbose { get; set; }
    }


    internal class Program
    {
        public const string DEFAULT_OUTPUT_PATH = "output";
        public const string DEFAULT_MODS_FOLDER = "mods";

        static void Main(string[] args)
        {   
            Parser.Default.ParseArguments<Options>(args)
               .WithParsed(RunOptions)
               .WithNotParsed(HandleParseError);
        }

        private static void HandleParseError(IEnumerable<Error> enumerable)
        {
            ConsoleUtilities.WriteLine("Failed to parse your command.", ConsoleColor.Red);

            foreach (var item in enumerable)
            {
                ConsoleUtilities.WriteLine(item.Tag.ToString(), ConsoleColor.Red);

            }
        }

        private static void RunOptions(Options options)
        {
            ConsoleUtilities.WriteLine($"Is exporting: {options.ExportDiffOnly}", ConsoleColor.Gray);

            var outputPath = DEFAULT_OUTPUT_PATH;
            var modsFolder = DEFAULT_MODS_FOLDER;

            if (!Directory.Exists(modsFolder))
            {
                ConsoleUtilities.WriteLine("\"Mods\" folder doesn't exist, creating a new folder.", ConsoleColor.Cyan);
                Directory.CreateDirectory(modsFolder);
                ConsoleUtilities.WriteLine("Mods folder have been created, please put your mods (as zip files) inside it and run this program again.", ConsoleColor.Green);
                Console.ReadKey();
                return;
            }

            else if (Directory.GetFiles(modsFolder).Length == 0)
            {
                ConsoleUtilities.WriteLine("Your mods directory is empty. Please put your mods (.zip files) inside it and run this program again.", ConsoleColor.Cyan);
                Console.ReadKey();
                return;
            }

            if (Directory.Exists(outputPath) && Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories).Length > 0)
            {
                ConsoleUtilities.WriteLine("Output path is not empty, do you want to delete it? [y/n]", ConsoleColor.Gray);

                if (ConsoleUtilities.YesOrNo())
                {
                    ConsoleUtilities.WriteLine("Deleting output folder...", ConsoleColor.Magenta);
                    Directory.Delete(outputPath, true);
                }
                else
                    ConsoleUtilities.WriteLine("Proceding without deleting output folder. This can cause issues!", ConsoleColor.Red);
            }

            var modMerger = new FileMerger(modsFolder, outputPath);


            Console.WriteLine("Loading mods...");
            modMerger.SearchModsContentPaths();

            ConsoleUtilities.WriteLine("Do you want to merge these mods? [y/n]", ConsoleColor.Gray);

            if (!ConsoleUtilities.YesOrNo()) return;

            Console.WriteLine("Saving to output folder");
            var outputDirectory = Directory.CreateDirectory(outputPath);

            modMerger.FileChanged += (string modName, string modFile) =>
            {
                if (options.Verbose)
                    ConsoleUtilities.WriteLine($"Merging {modFile} from {modName}", ConsoleColor.Cyan);
            };
            //modMerger.PatchAndExport();
            modMerger.Patch();

            if (!options.ExportDiffOnly)
            {
                modMerger.SaveChanges();

                ConsoleUtilities.WriteLine("Files have been patched!", ConsoleColor.Green);

                ConsoleUtilities.WriteLine("Generating ResourceSizeTable...", ConsoleColor.Cyan);

                modMerger.CreateResourceSizeTable();
            } 
            else
            {
                modMerger.ExportChangesToJson();
            }

            ConsoleUtilities.WriteLine("#=-------------------------=#", ConsoleColor.Gray);
            ConsoleUtilities.WriteLine("      Process complete!", ConsoleColor.Green);
            ConsoleUtilities.WriteLine($"Saved at: {outputDirectory.FullName}", ConsoleColor.Gray);
            ConsoleUtilities.WriteLine("#=-------------------------=#", ConsoleColor.Gray);

            TryToOpenPath(outputDirectory.FullName);

            Console.ReadLine();
        }

        private static void TryToOpenPath(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using Process fileOpener = new();
                fileOpener.StartInfo.FileName = "explorer";
                fileOpener.StartInfo.Arguments = "/select," + path + "\"";
                fileOpener.Start();
                return;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                using Process fileOpener = new();
                fileOpener.StartInfo.FileName = "explorer";
                fileOpener.StartInfo.Arguments = "-R " + path;
                fileOpener.Start();
                return;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                using Process dbusShowItemsProcess = new()
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dbus-send",
                        Arguments = "--print-reply --dest=org.freedesktop.FileManager1 /org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems array:string:\"file://" + path + "\" string:\"\"",
                        UseShellExecute = true
                    }
                };
                dbusShowItemsProcess.Start();
                dbusShowItemsProcess.WaitForExit();

                if (dbusShowItemsProcess.ExitCode == 0)
                {
                    // The dbus invocation can fail for a variety of reasons:
                    // - dbus is not available
                    // - no programs implement the service,
                    // - ...
                    return;
                }
            }
        }
    }
}

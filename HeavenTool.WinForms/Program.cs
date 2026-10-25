using HeavenTool.Forms.RSTB;
using HeavenTool.IO;
using HeavenTool.IO.FileFormats.ResourceSizeTable;
using HeavenTool.Utility;
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace HeavenTool;

[SupportedOSPlatform("windows")]
internal static partial class Program
{
    public static string VERSION => $"v{Application.ProductVersion}";
    public static Form? TargetForm = null;

    [STAThread]
    static void Main(string[] args)
    {
        if (OperatingSystem.IsWindows())
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.SetColorMode(SystemColorMode.Dark);

#if DEBUG
            WinConsole.Initialize("Heaven Tool: Console");
            ConsoleUtilities.WriteLine("Heaven Tool initialized!", ConsoleColor.Gray);
#endif

            if (args.Length > 0)
                switch (args[0])
                {
                    case "--associate":
                        {
                            if (args.Length > 1)
                            {
                                var fileTypeToAssociate = args[1];
                                switch (fileTypeToAssociate)
                                {
                                    case "bcsv":
                                        ProgramAssociation.AssociateProgram(".bcsv", "BCSV", "BCSV File", Application.ExecutablePath);
                                        break;

                                    case "srsizetable":
                                        ProgramAssociation.AssociateProgram(".srsizetable", "srsizetable", "ResourceSizeTable File", Application.ExecutablePath);
                                        break;

                                    case "rsizetable":
                                        ProgramAssociation.AssociateProgram(".rsizetable", "rsizetable", "ResourceSizeTable File", Application.ExecutablePath);
                                        ProgramAssociation.AssociateProgram(".rsizetable.zs", "rsizetable", "ResourceSizeTable File", Application.ExecutablePath);
                                        break;
                                }
                            }
                            return;
                        }

                    case "--disassociate":
                        {
                            if (args.Length > 1)
                            {
                                var fileTypeToAssociate = args[1];
                                switch (fileTypeToAssociate)
                                {
                                    case "bcsv":
                                        ProgramAssociation.DisassociateProgram(".bcsv", "BCSV");
                                        break;

                                    case "srsizetable":
                                        ProgramAssociation.DisassociateProgram(".srsizetable", "srsizetable");
                                        break;

                                    case "rsizetable":
                                        ProgramAssociation.DisassociateProgram(".rsizetable", "rsizetable");
                                        ProgramAssociation.DisassociateProgram(".rsizetable.zs", "rsizetable");
                                        break;
                                }
                            }
                            return;
                        }

                    default:
                        {
                            TargetForm = HandleInput(args);
                        }
                        break;
                }

            // TargetForm is defined by the input provided by the system (user opened a file)
            // If no input is provided (user opened the .exe alone), will open the Main Window
            TargetForm ??= new HeavenMain();

            Application.Run(TargetForm);
        }
        else
        {
            Console.WriteLine("This is a Windows Application, please run it on Windows OS.");
        }
    }

    public static Form? HandleInput(string[] originalArguments)
    {
        var path = originalArguments[0];
        var extension = Path.GetExtension(path);

        switch (extension)
        {
            case ".bfres":
            case ".sbfres":
                var bfresViewer = new Forms.BFRES.BFRESViewer();
                bfresViewer.Shown += async (_, _) => await bfresViewer.LoadFileAsync(path);
                return bfresViewer;

            case ".bcsv":
                var bcsvEditor = new BCSVForm();
                bcsvEditor.LoadFile(path);
                return bcsvEditor;

            case ".srsizetable":
                if (originalArguments.Length >= 2)
                {
                    // TODO: This can be optimized to not use an window.
                    // See HeavenTools.ModManager FileMerger.CreateResourceSizeTable() for reference

                    var rstbEditor = new RSTBEditor();
                    rstbEditor.LoadFile(path);
                    if (rstbEditor.LoadedFile is ResourceSizeTable rstb)
                    {
                        ResourceSizeTablePatcher.CalculatePatch(originalArguments[1]).ApplyTo(rstb);
                        var outPath = path;
                        if (originalArguments.Length >= 3)
                            outPath = originalArguments[2];

                        var bytes = rstbEditor.LoadedFile.Save();
                        if (bytes != null)
                            File.WriteAllBytes(outPath, bytes);
                    }
                    Environment.Exit(0);
                    return null;
                }
                else
                {
                    var rstbEditor = new RSTBEditor();
                    rstbEditor.LoadFile(path);
                    return rstbEditor;
                }
            default:
                return null;
        }
    }

}
using HeavenTool.Forms.RSTB;
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
            WinConsole.Initialize();
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
                                        ProgramAssociation.AssociateProgram(".bcsv", "BCSV", "BCSV File");
                                        break;

                                    case "srsizetable":
                                        ProgramAssociation.AssociateProgram(".srsizetable", "srsizetable", "ResourceSizeTable File");
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
        } else
        {
            Console.WriteLine("This is a Windows Application, please run it on Windows OS.");
        }
    }

    /// <summary>
    /// Creates and returns a form instance for editing or processing the specified file based on its extension.
    /// </summary>
    /// <remarks>Supports files with ".bcsv" and ".srsizetable" extensions. For ".srsizetable" files,
    /// additional arguments may be required to perform certain operations. If the operation completes and no form is
    /// needed, the method returns null and may terminate the application.</remarks>
    /// <param name="originalArguments">An array of command-line arguments. The first element must be the file path to process. Additional elements may
    /// be required depending on the file type.</param>
    /// <returns>A form instance for editing or processing the specified file, or null if the file type is not supported or if
    /// the operation completes without displaying a form.</returns>
    public static Form? HandleInput(string[] originalArguments)
    {
        var path = originalArguments[0];
        var extension = Path.GetExtension(path);

        switch (extension)
        {
            case ".bcsv":
                var bcsvEditor = new BCSVForm();
                bcsvEditor.LoadBCSVFile(path);
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
                        rstbEditor.CreateUpdatedRSTBFromModdedRomFs(rstb, originalArguments[1], false);
                        string outPath = path;
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

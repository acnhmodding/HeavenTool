using HeavenTool.Forms.RSTB;
using HeavenTool.Utility;
using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace HeavenTool;

[SupportedOSPlatform("windows")]
internal static class Program
{
    public static string VERSION => $"v{Application.ProductVersion.Split("+")[0]}";
    public static Form TargetForm = null;

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
    /// Handle path as an input; e.g. User double-clicked a .bcsv file or opened it with the editor.
    /// </summary>
    /// <param name="path">File path</param>
    public static Form HandleInput(string[] originalArguments)
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
                    rstbEditor.CreateUpdatedRSTBFromModdedRomFs(rstbEditor.LoadedFile, originalArguments[1], false);
                    string outPath = path;
                    if (originalArguments.Length >= 3)
                        outPath = originalArguments[2];

                    var bytes = rstbEditor.LoadedFile.Save();
                    File.WriteAllBytes(outPath, bytes);
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

    static class WinConsole
    {
        static public void Initialize(bool alwaysCreateNewConsole = true)
        {
            bool consoleAttached = true;
            if (alwaysCreateNewConsole
                || (AttachConsole(ATTACH_PARRENT) == 0
                && Marshal.GetLastWin32Error() != ERROR_ACCESS_DENIED))
            {
                consoleAttached = AllocConsole() != 0;
            }

            if (consoleAttached)
            {
                InitializeOutStream();
                InitializeInStream();
            }
        }

        private static void InitializeOutStream()
        {
            var fs = CreateFileStream("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE, FileAccess.Write);
            if (fs != null)
            {
                var writer = new StreamWriter(fs) { AutoFlush = true };
                Console.SetOut(writer);
                Console.SetError(writer);
            }
        }

        private static void InitializeInStream()
        {
            var fs = CreateFileStream("CONIN$", GENERIC_READ, FILE_SHARE_READ, FileAccess.Read);
            if (fs != null)
            {
                Console.SetIn(new StreamReader(fs));
            }
        }

        private static FileStream CreateFileStream(string name, uint win32DesiredAccess, uint win32ShareMode,
                                FileAccess dotNetFileAccess)
        {
            var file = new SafeFileHandle(CreateFileW(name, win32DesiredAccess, win32ShareMode, IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero), true);
            if (!file.IsInvalid)
            {
                var fs = new FileStream(file, dotNetFileAccess);
                return fs;
            }
            return null;
        }

        #region Win API Functions and Constants
        [DllImport("kernel32.dll",
            EntryPoint = "AllocConsole",
            SetLastError = true,
            CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall)]
        private static extern int AllocConsole();

        [DllImport("kernel32.dll",
            EntryPoint = "AttachConsole",
            SetLastError = true,
            CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall)]
        private static extern UInt32 AttachConsole(UInt32 dwProcessId);

        [DllImport("kernel32.dll",
            EntryPoint = "CreateFileW",
            SetLastError = true,
            CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall)]
        private static extern IntPtr CreateFileW(
              string lpFileName,
              UInt32 dwDesiredAccess,
              UInt32 dwShareMode,
              IntPtr lpSecurityAttributes,
              UInt32 dwCreationDisposition,
              UInt32 dwFlagsAndAttributes,
              IntPtr hTemplateFile
            );

        private const UInt32 GENERIC_WRITE = 0x40000000;
        private const UInt32 GENERIC_READ = 0x80000000;
        private const UInt32 FILE_SHARE_READ = 0x00000001;
        private const UInt32 FILE_SHARE_WRITE = 0x00000002;
        private const UInt32 OPEN_EXISTING = 0x00000003;
        private const UInt32 FILE_ATTRIBUTE_NORMAL = 0x80;
        private const UInt32 ERROR_ACCESS_DENIED = 5;

        private const UInt32 ATTACH_PARRENT = 0xFFFFFFFF;

        #endregion
    }

}

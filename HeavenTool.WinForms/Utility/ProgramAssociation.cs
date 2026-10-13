using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace HeavenTool.Utility;

public partial class ProgramAssociation
{
    private const string UserClassesRoot = @"Software\Classes";

    private enum AssocStr
    {
        Executable = 2
    }

    [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint AssocQueryString(int flags, AssocStr str, string pszAssoc, string? pszExtra, StringBuilder? pszOut, ref uint pcchOut);

    [LibraryImport("shell32.dll", SetLastError = true)]
    private static partial void SHChangeNotify(uint wEventId, uint uFlags, nint dwItem1, nint dwItem2);

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_FLUSH = 0x00001000;

    public static string GetAssociatedProgram(string extension)
    {
        ValidateExtension(extension);

        uint length = 0;
        _ = AssocQueryString(0, AssocStr.Executable, extension, null, null, ref length);

        var sb = new StringBuilder((int)length);
        _ = AssocQueryString(0, AssocStr.Executable, extension, null, sb, ref length);

        return sb.ToString();
    }

    public static void AssociateProgram(string extension, string progId, string description, string applicationPath)
    {
        ValidateExtension(extension);

        if (string.IsNullOrWhiteSpace(progId))
            throw new ArgumentException("ProgId cannot be null or empty.");

        if (string.IsNullOrWhiteSpace(applicationPath))
            throw new ArgumentException("Application path cannot be null or empty.");

        using var baseKey = Registry.CurrentUser.CreateSubKey($@"{UserClassesRoot}\{extension}")
            ?? throw new InvalidOperationException("Failed to create extension key.");

        baseKey.SetValue("", progId);

        using var progKey = Registry.CurrentUser.CreateSubKey($@"{UserClassesRoot}\{progId}")
            ?? throw new InvalidOperationException("Failed to create ProgId key.");

        progKey.SetValue("", description);

        using var iconKey = progKey.CreateSubKey("DefaultIcon");
        iconKey?.SetValue("", $"\"{applicationPath}\",0");

        using var shell = progKey.CreateSubKey("Shell")
            ?? throw new InvalidOperationException("Failed to create Shell key.");

        using var commandKey = shell.CreateSubKey(@"open\command");
        commandKey?.SetValue("", $"\"{applicationPath}\" \"%1\"");

        NotifyShell();
    }

    public static void DisassociateProgram(string extension, string progId)
    {
        ValidateExtension(extension);

        Registry.CurrentUser.DeleteSubKeyTree($@"{UserClassesRoot}\{extension}", false);
        Registry.CurrentUser.DeleteSubKeyTree($@"{UserClassesRoot}\{progId}", false);

        NotifyShell();
    }

    private static void ValidateExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("Extension cannot be null or empty.");

        if (!extension.StartsWith('.'))
            throw new ArgumentException("Extension must start with .");
    }

    private static void NotifyShell()
    {
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, nint.Zero, nint.Zero);
    }
}
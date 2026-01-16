using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HeavenTool.Forms.Components;

public static partial class DarkModeHelper
{
    [LibraryImport("uxtheme.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

    internal const string DarkModeIdentifier = "DarkMode";
    internal const string ExplorerThemeIdentifier = "Explorer";

    public static void EnableDarkModeScrollbar(this DataGridView dataGridView)
    {
        ArgumentNullException.ThrowIfNull(dataGridView);

        // Get the private _vertScrollBar field
        var verticalScrollField = typeof(DataGridView).GetField("_vertScrollBar", BindingFlags.NonPublic | BindingFlags.Instance);
        var horizontalScrollField = typeof(DataGridView).GetField("_horizScrollBar", BindingFlags.NonPublic | BindingFlags.Instance);

        // Make sure the field was found
        if (verticalScrollField != null && verticalScrollField.GetValue(dataGridView) is VScrollBar vScrollBar)
            _ = SetWindowTheme(vScrollBar.Handle, $"{DarkModeIdentifier}_{ExplorerThemeIdentifier}", null);

        if (horizontalScrollField != null && horizontalScrollField.GetValue(dataGridView) is HScrollBar hScrollBar)
            _ = SetWindowTheme(hScrollBar.Handle, $"{DarkModeIdentifier}_{ExplorerThemeIdentifier}", null);
    }
}

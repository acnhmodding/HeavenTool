using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace HeavenTool.Forms.Editor;

/// <summary>
/// Factory class responsible for creating editors based on file extensions.
/// </summary>
public static class EditorFactory
{
    /// <summary>
    /// Attempts to create an editor for the specified <paramref name="path"/> based on its file extension.
    /// </summary>
    /// <param name="path">The file path to determine the editor for.</param>
    /// <param name="result">The editor instance created if a compatible editor is found.</param>
    /// <returns><strong>true</strong> if a compatible editor is found; otherwise, <strong>false</strong>.</returns>
    public static bool TryCreateEditor(string path, [MaybeNullWhen(false)] out BaseEditor result)
    {
        result = null;

        var extension = Path.GetExtension(path).ToLower();

        if (extension == ".bcsv")
        {
            result = new BCSVForm(true);
            return true;
        }

        return false;
    }
}

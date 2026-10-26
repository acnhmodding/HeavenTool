using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels;

/// <summary>What the BCSV view model asks of its window: file pickers, the clipboard and dialogs.</summary>
public interface IBCSVView
{
    IStorageProvider StorageProvider { get; }
    IClipboard? Clipboard { get; }

    /// <summary>Asks whether to save first: true to save, false to discard, null to cancel.</summary>
    Task<bool?> ConfirmSaveChangesAsync();

    /// <summary>Shows an editor with OK and Cancel; true when the user chose OK.</summary>
    Task<bool> ShowEditorAsync(string title, Control editor, double width, double height);

    Task ShowMessageAsync(string title, string message);

    /// <summary>Shows a dialog over the window and returns its result.</summary>
    Task<bool> ShowDialogAsync(Window dialog);

    /// <summary>Moves focus to the search box, selecting its text.</summary>
    void FocusSearch();
}

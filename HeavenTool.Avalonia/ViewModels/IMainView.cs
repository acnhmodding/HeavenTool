using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels;

/// <summary>One option in a choice dialog.</summary>
public sealed record Choice(string Title, string? Subtitle, string? Details);

/// <summary>What the main window view model asks of its window: the launcher, file pickers and dialogs.</summary>
public interface IMainView
{
    IStorageProvider StorageProvider { get; }
    ILauncher Launcher { get; }

    /// <summary>Lets the user pick one of <paramref name="choices"/>; null when they cancel.</summary>
    Task<int?> ChooseAsync(string title, string message, IReadOnlyList<Choice> choices, string confirmText);

    /// <summary>Lets the user tick any number of <paramref name="choices"/>, starting from <paramref name="selected"/>; null when they cancel.</summary>
    Task<IReadOnlyList<int>?> ChooseManyAsync(string title, string message, IReadOnlyList<Choice> choices, IReadOnlyList<int> selected, string confirmText);

    /// <summary>Asks a yes or no question; true when the user chose <paramref name="confirmText"/>.</summary>
    Task<bool> ConfirmAsync(string title, string message, string confirmText);

    Task ShowMessageAsync(string title, string message);
}

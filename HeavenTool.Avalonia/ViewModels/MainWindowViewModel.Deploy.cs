using CommunityToolkit.Mvvm.Input;
using HeavenTool.Avalonia.Settings;
using HeavenTool.ModManager.Deploy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels;

// Deploying the enabled library mods to the emulator's mod folder.
public partial class MainWindowViewModel
{
    // Skyline comes from GitHub, which asks for a user agent.
    private static readonly HttpClient Http = new() { DefaultRequestHeaders = { { "User-Agent", "HeavenTool" } } };

    private bool CanDeploy() => !IsDeploying;

    [RelayCommand(CanExecute = nameof(CanDeploy))]
    private async Task Deploy()
    {
        if (view == null) return;
        var settings = AppSettings.Current;
        if (string.IsNullOrWhiteSpace(settings.ModFolder))
        {
            if (await view.ConfirmAsync("Choose where to deploy", "Heaven Tool deploys mods into your emulator's mod folder for Animal Crossing. Choose it in Settings first.", "Open Settings"))
                OpenSettings();
            return;
        }

        IsDeploying = true;
        BusyText = "Deploying...";
        var enabled = installed.Where(mod => mod.Enabled).ToList();
        string title, message;
        try
        {
            var warnings = new List<string>();
            string? skyline = null;
            if (settings.InstallSkyline && enabled.Count > 0)
            {
                BusyText = "Getting Skyline...";
                try { skyline = await Skyline.GetExefsAsync(Path.Combine(AppSettings.Folder, "Skyline"), Http); }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
                {
                    warnings.Add($"Skyline couldn't be downloaded, so it wasn't deployed, and mods that need it won't load: {ex.Message}");
                }
            }

            var deployer = new ModDeployer
            {
                ModFolder = settings.ModFolder,
                FolderName = AppSettings.DeployFolderName,
                VanillaRomfs = GameFiles.FindRomfs(settings.RomfsFolder),
                SkylineExefs = skyline,
            };
            var progress = new Progress<string>(text => { if (IsDeploying) BusyText = text; });
            // Installs move library folders, so they wait for the deploy to finish reading them.
            await installLock.WaitAsync();
            DeployResult result;
            try { result = await Task.Run(() => deployer.Deploy(enabled, progress)); }
            finally { installLock.Release(); }
            result.Warnings.InsertRange(0, warnings);
            title = enabled.Count > 0 ? "Mods deployed" : "Nothing deployed";
            message = DescribeDeploy(result, Emulators.NameFor(settings.ModFolder) ?? "your mod folder");
        }
        catch (Exception ex)
        {
            title = "Couldn't deploy";
            message = ex is IOException ? $"{ex.Message}\n\nIf the emulator is running the game, close it and deploy again." : ex.Message;
        }
        finally
        {
            IsDeploying = false;
            BusyText = null;
            UpdateDeployStatus();
        }
        await view.ShowMessageAsync(title, message);
    }

    private static string DescribeDeploy(DeployResult result, string place)
    {
        const int listed = 12;
        var text = new StringBuilder();
        if (result.ModCount == 0)
            text.Append($"No mods are turned on, so nothing is deployed to {place} now.");
        else
        {
            text.Append($"{result.ModCount} mod{(result.ModCount == 1 ? "" : "s")} ({result.FileCount:N0} files) deployed to {place}.");
            if (result.SkylineInstalled) text.Append(" Skyline is included.");
            if (result.ResourceSizeTableUpdated) text.Append(" The resource size table was updated to fit them.");
        }

        if (result.MergedFiles.Count > 0)
        {
            text.Append($"\n\nMerged {result.MergedFiles.Count} file{(result.MergedFiles.Count == 1 ? "" : "s")} that several mods change:");
            foreach (var file in result.MergedFiles.Take(listed)) text.Append($"\n• {file}");
            if (result.MergedFiles.Count > listed) text.Append($"\n• ...and {result.MergedFiles.Count - listed} more");
        }
        if (result.Conflicts.Count > 0)
        {
            text.Append($"\n\n{result.Conflicts.Count} file{(result.Conflicts.Count == 1 ? "" : "s")} couldn't be merged, so the mod higher in the list wins:");
            foreach (var conflict in result.Conflicts.Take(listed))
                text.Append($"\n• {conflict.Path}: {conflict.Winner} over {string.Join(", ", conflict.Overwritten.Distinct())}");
            if (result.Conflicts.Count > listed) text.Append($"\n• ...and {result.Conflicts.Count - listed} more");
        }
        foreach (var warning in result.Warnings) text.Append($"\n\n{warning}");
        return text.ToString();
    }

    /// <summary>Compares what was last deployed with the library's enabled mods, for the line under the Deploy button.</summary>
    private void UpdateDeployStatus()
    {
        var settings = AppSettings.Current;
        if (string.IsNullOrWhiteSpace(settings.ModFolder))
        {
            DeployStatus = "Choose a mod folder in Settings";
            return;
        }
        var enabled = installed.Where(mod => mod.Enabled).ToList();
        var marker = ModDeployer.ReadMarker(Path.Combine(settings.ModFolder, AppSettings.DeployFolderName));
        DeployStatus = marker == null
            ? enabled.Count == 0 ? "No mods turned on" : "Not deployed yet"
            : marker.Matches(enabled, settings.InstallSkyline) ? "Up to date" : "Changes not deployed";
    }
}

using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using GamebananaAPI.Data;
using HeavenTool.ModManager.Library;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels;

// Installing mods into the library, from GameBanana or from an archive on disk.
public partial class MainWindowViewModel
{
    internal async Task InstallAsync(ModCardViewModel card)
    {
        if (view == null || card.ModId is not { } modId || card.IsBusy) return;
        var cancellation = card.Cancellation = new CancellationTokenSource();
        var temporary = NewTemporaryFolder();
        card.IsBusy = true;
        card.IsProgressKnown = false;
        card.Status = "Getting the file list...";
        try
        {
            var profile = GetProfileAsync(modId, cancellation.Token);
            var files = (await client.GetDownloadPage(modId, cancellation.Token))?.Files ?? [];
            if (files.Length == 0)
            {
                if (card.CanOpenPage && await view.ConfirmAsync(card.Name, "This mod has no files to download on GameBanana. Open its page to see how to get it?", "Open page"))
                    await OpenPageAsync(card);
                return;
            }

            var file = files.Length == 1 ? files[0] : await ChooseFileAsync(card.Name, files);
            if (file == null) return;

            // GameBanana lists an archive's files, so a save can be turned down before downloading it.
            card.Status = "Checking the file...";
            if (await GetFileListAsync(file, cancellation.Token) is { } paths && ModArchive.ContainsSaveData(paths) && !ModArchive.ContainsGameFiles(paths))
            {
                await ShowSaveFileWarningAsync(card.Name);
                return;
            }

            card.Status = $"Downloading {file.FileName}...";
            card.IsProgressKnown = file.Size > 0;
            card.Progress = 0;
            var archive = Path.Combine(temporary, ArchiveName(file.FileName));
            await using (var stream = File.Create(archive))
                await client.DownloadFile(file, stream, new Progress<long>(bytes => card.Progress = Math.Min(1, (double)bytes / file.Size)), cancellation.Token);
            await VerifyChecksumAsync(archive, file, cancellation.Token);

            var metadata = new InstalledMod
            {
                Name = card.Name,
                Author = card.Author,
                Version = file.Version,
                ModId = modId,
                FileId = file.IdRow,
                FileName = file.FileName,
                ProfileUrl = card.ProfileUrl,
                ImageUrl = card.PreviewUrl,
            };
            if (await profile is { } details)
            {
                // A mod in a root category has no super category, and its own category is the root.
                metadata.Category = (details.SuperCategory ?? details.Category)?.Name;
                metadata.Subcategory = details.SuperCategory != null ? details.Category?.Name : null;
                metadata.Tags = [.. details.Tags.Select(tag => tag.ToString()).Where(tag => tag.Length > 0)];
            }
            await InstallArchiveAsync(archive, temporary, metadata, card, cancellation.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await view.ShowMessageAsync($"Couldn't install {card.Name}", ex.Message);
        }
        finally
        {
            card.IsBusy = false;
            card.Status = null;
            card.Cancellation = null;
            DeleteTemporaryFolder(temporary);
        }
    }

    /// <summary>A mod's category and tags for its metadata, or null when GameBanana can't give them, which doesn't stop the install.</summary>
    private async Task<ModProfile?> GetProfileAsync(int modId, CancellationToken cancellation)
    {
        try { return await client.GetProfilePage(modId, cancellation); }
        // Started before the file is picked, so it's never awaited when the install stops early, and must not throw.
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or Newtonsoft.Json.JsonException) { return null; }
    }

    /// <summary>The paths inside a GameBanana file, or null when GameBanana can't list them, in which case the archive is checked once extracted.</summary>
    private async Task<string[]?> GetFileListAsync(GamebananaFile file, CancellationToken cancellation)
    {
        try { return await client.GetRawFileList(file.IdRow, cancellation); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellation.IsCancellationRequested) { return null; }
    }

    private Task ShowSaveFileWarningAsync(string name) =>
        view!.ShowMessageAsync($"{name} is a save file",
            "This download is an Animal Crossing save (it has files like main.dat and mainHeader.dat), not a mod of the game's files. Heaven Tool can't install save files yet.");

    [RelayCommand]
    private async Task InstallFromFile()
    {
        if (view == null) return;
        var picked = await view.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose a mod archive",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Mod archives") { Patterns = ["*.zip", "*.7z", "*.rar", "*.tar", "*.tar.gz", "*.gz"] }],
        });
        if (picked.Count == 0 || picked[0].TryGetLocalPath() is not { } archive) return;

        var temporary = NewTemporaryFolder();
        var name = Path.GetFileNameWithoutExtension(archive);
        BusyText = $"Installing {name}...";
        try
        {
            var metadata = new InstalledMod { Name = name, FileName = Path.GetFileName(archive) };
            await InstallArchiveAsync(archive, temporary, metadata, null, CancellationToken.None);
        }
        catch (Exception ex)
        {
            await view.ShowMessageAsync($"Couldn't install {name}", ex.Message);
        }
        finally
        {
            BusyText = null;
            DeleteTemporaryFolder(temporary);
        }
    }

    /// <summary>Extracts an archive, asks which variants to keep when it holds more than one, and installs them in the library.</summary>
    private async Task InstallArchiveAsync(string archive, string temporary, InstalledMod metadata, ModCardViewModel? card, CancellationToken cancellation)
    {
        var extracted = Path.Combine(temporary, "files");
        if (card != null)
        {
            card.Status = "Extracting...";
            card.IsProgressKnown = true;
            card.Progress = 0;
        }
        var progress = new Progress<double>(share => { if (card != null) card.Progress = share; });
        await Task.Run(() => ModArchive.Extract(archive, extracted, progress, cancellation), cancellation);

        var variants = ModArchive.FindVariants(extracted);
        // A save that comes with game file changes still installs those; a save on its own can't be installed yet.
        if (variants.Count == 0 && ModArchive.ContainsSaveData(extracted))
        {
            await ShowSaveFileWarningAsync(metadata.Name);
            return;
        }
        if (variants.Count == 0)
            throw new InvalidDataException("No romfs or exefs folder was found in this file, so Heaven Tool can't tell where its files go.");
        var chosen = variants.Count == 1 ? variants : await ChooseVariantsAsync(metadata.Name, variants);
        if (chosen == null) return;

        if (card != null)
        {
            card.Status = "Installing...";
            card.IsProgressKnown = false;
        }
        await installLock.WaitAsync(cancellation);
        try { await Task.Run(() => Library.Install(metadata, chosen), cancellation); }
        finally { installLock.Release(); }

        LoadInstalled();
        if (IsInstalledPage) await ReloadAsync();
    }

    private async Task<GamebananaFile?> ChooseFileAsync(string modName, GamebananaFile[] files)
    {
        var choices = files.Select(file =>
        {
            var details = new List<string> { ModCardViewModel.FormatSize(file.Size) };
            if (!string.IsNullOrWhiteSpace(file.Version)) details.Add("v" + file.Version.TrimStart('v', 'V'));
            if (file.DateAdded > 0) details.Add(ModCardViewModel.FormatDate(DateTimeOffset.FromUnixTimeSeconds(file.DateAdded)));
            details.Add($"{file.DownloadCount:N0} downloads");
            var description = string.IsNullOrWhiteSpace(file.Description) ? null : file.Description.Trim();
            return new Choice(description ?? file.FileName, description != null ? file.FileName : null, string.Join(" · ", details));
        }).ToList();
        var index = await view!.ChooseAsync(modName, "This mod has more than one file. Choose the one to install.", choices, "Download");
        return index is { } i ? files[i] : null;
    }

    private async Task<List<ModVariant>?> ChooseVariantsAsync(string modName, List<ModVariant> variants)
    {
        var choices = variants.Select(variant =>
        {
            var parts = new List<string>();
            if (variant.Romfs != null) parts.Add("romfs");
            if (variant.Exefs != null) parts.Add("exefs");
            var count = new[] { variant.Romfs, variant.Exefs }.Where(folder => folder != null)
                .Sum(folder => Directory.EnumerateFiles(folder!, "*", SearchOption.AllDirectories).Count());
            return new Choice(variant.Name.Length > 0 ? variant.Name : "Main files", null, $"{string.Join(" + ", parts)} · {count} file{(count == 1 ? "" : "s")}");
        }).ToList();
        // Archives split into parts usually mark the one every install needs.
        List<int> required = [.. variants.Select((variant, index) => (variant, index))
            .Where(pair => pair.variant.Name.Contains("required", StringComparison.OrdinalIgnoreCase)).Select(pair => pair.index)];
        var picked = await view!.ChooseManyAsync(modName,
            "This file holds more than one set of mod files: either versions to pick from, or parts to combine. Tick the ones to install; they're installed together as one mod.",
            choices, required.Count > 0 ? required : [0], "Install");
        return picked is { Count: > 0 } ? [.. picked.Order().Select(index => variants[index])] : null;
    }

    private static async Task VerifyChecksumAsync(string path, GamebananaFile file, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(file.Md5Checksum)) return;
        await using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(await MD5.HashDataAsync(stream, cancellation));
        if (!hash.Equals(file.Md5Checksum.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"{file.FileName} didn't download correctly (its checksum doesn't match). Try again.");
    }

    private static string NewTemporaryFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "HeavenTool", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static void DeleteTemporaryFolder(string folder)
    {
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    // The server's name decides the archive type's extension, but must not leave the temporary folder.
    private static string ArchiveName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ? "download" : name;
    }
}

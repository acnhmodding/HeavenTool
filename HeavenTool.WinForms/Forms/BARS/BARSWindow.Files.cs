using AeonSake.NintendoTools.Compression.Zstd;
using HeavenTool.IO;
using HeavenTool.IO.Audio;
using HeavenTool.IO.FileFormats.BARS;
using HeavenTool.IO.FileFormats.BWAV;
using HeavenTool.Utility;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HeavenTool.Forms.BARS;

public partial class BARSWindow
{
    private readonly ZstdDecompressor decompressor = new();
    private bool importing;
    private bool isDirty;
    private AudioAsset? standaloneAsset;
    private BARSFileReader? barsFile;
    private string lastLoadedPath = string.Empty;
    private string? loadedFileName;

    private void SetDirty(bool dirty)
    {
        isDirty = dirty;
        Text = "Binary Audio Resources Editor"
            + (loadedFileName == null ? "" : $" - {loadedFileName}{(dirty ? " *" : "")}");
    }

    private bool ConfirmDiscardChanges()
    {
        if (!ConfirmLoopEditorChanges()) return false;
        if (!isDirty) return true;
        return MessageBox.Show(this, $"Save changes to {loadedFileName} before continuing?",
            "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning) switch
        {
            DialogResult.Yes => SaveFileAs(),
            DialogResult.No => true,
            _ => false
        };
    }

    private string StandaloneAssetName
    {
        get
        {
            string name = Path.GetFileNameWithoutExtension(loadedFileName) ?? "Audio";
            return name.EndsWith(".prefetch", StringComparison.OrdinalIgnoreCase) ? name[..^".prefetch".Length] : name;
        }
    }

    private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
    {
        using var openFileDialog = new OpenFileDialog
        {
            Title = "Select a BARS or BWAV file",
            Filter = "Audio Resource|*.bars;*.bwav;*.bars.zs",
            FilterIndex = 1,
            RestoreDirectory = true,
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
            RunFileAction(() => LoadFile(openFileDialog.FileName), "Could not open audio resource");
    }

    private void LoadFile(string fileName)
    {
        if (importing || !ConfirmDiscardChanges()) return;
        UnloadFile();

        ConsoleUtilities.WriteLine($"Loading {Path.GetFileName(fileName)}", ConsoleColor.Gray);

        using var compressedStream = new FileStream(fileName, FileMode.Open);
        using var uncompressedStream = new MemoryStream();

        if (decompressor.CanDecompress(compressedStream))
        {
            ConsoleUtilities.WriteLine("Decompressing BARS file...", ConsoleColor.Gray);
            decompressor.Decompress(compressedStream, uncompressedStream);
        }
        else compressedStream.CopyTo(uncompressedStream);

        uncompressedStream.Position = 0;

        var magic = System.Text.Encoding.ASCII.GetString(uncompressedStream.GetBuffer(), 0, (int)Math.Min(4, uncompressedStream.Length));
        switch (magic)
        {
            case "BARS": barsFile = new BARSFileReader(uncompressedStream); break;
            case "BWAV": standaloneAsset = new AudioAsset { BinaryWave = new BinaryWaveFile(uncompressedStream) }; break;
            default: throw new InvalidDataException("Expected a BARS or BWAV file.");
        }
        loadedFileName = Path.GetFileName(fileName);
        lastLoadedPath = Path.GetDirectoryName(fileName) ?? string.Empty;
        LoadSingers();
        BuildTree();
        SetDirty(false);
        UpdateFileControls();
    }

    private async void ImportAudio(AudioAsset asset)
    {
        if (importing || asset.BinaryWave == null) return;
        if (!WinFormsUtility.OpenFileDialog("Audio (*.bwav;*.wav)|*.bwav;*.wav", "bwav", out var path)) return;
        bool bwav = Path.GetExtension(path).Equals(".bwav", StringComparison.OrdinalIgnoreCase);
        if (ChooseImportLoops(asset, bwav) is not { } loopChoice) return;
        importing = true;
        UpdateFileControls();
        try
        {
            using var template = asset.BinaryWave.CreateSnapshot();
            using var replacement = await Task.Run(() => AudioImporter.Read(path, template, loopChoice));
            if (IsDisposed || (!ReferenceEquals(standaloneAsset, asset) && !(barsFile?.AudioAssets.Contains(asset) ?? false))) return;
            asset.ReplaceWave(replacement);
            SetDirty(true);
            singingCache = new(StringComparer.OrdinalIgnoreCase);
            BuildTree(asset);
        }
        catch (Exception ex)
        {
            if (!IsDisposed) MessageBox.Show(this, ex.Message, "Could not import audio", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            importing = false;
            if (!IsDisposed) UpdateFileControls();
        }
    }

    private void ImportMetadata(AudioAsset asset)
    {
        if (importing || !WinFormsUtility.OpenFileDialog("BWAV Metadata (*.bwav.meta)|*.bwav.meta|AMTA (*.amta)|*.amta", "meta", out var path)) return;
        RunFileAction(() =>
        {
            asset.ReplaceMetadata(File.ReadAllBytes(path));
            SetDirty(true);
            BuildTree(asset);
        }, "Could not import metadata");
    }

    private bool SaveFileAs()
    {
        if (importing || loadedFileName == null) return false;
        if (!ConfirmLoopEditorChanges()) return false;
        bool archive = barsFile != null;
        var name = loadedFileName.EndsWith(".zs", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileNameWithoutExtension(loadedFileName) : loadedFileName;
        if (!WinFormsUtility.SaveFileDialog(archive ? "Audio Resource (*.bars)|*.bars" : "Binary Waveform (*.bwav)|*.bwav", name, out var path)) return false;
        bool saved = false;
        RunFileAction(() => saved = SaveResource(path), "Could not save audio resource");
        return saved;
    }

    private bool SaveResource(string path)
    {
        if (barsFile != null)
        {
            if (!AudioResourceWriter.SaveArchive(barsFile, path, Path.Combine(lastLoadedPath, loadedFileName!), ChooseSharedStreamAction))
                return false;
        }
        else if (standaloneAsset != null)
            AudioResourceWriter.SaveAsset(standaloneAsset, path);
        else return false;
        SetDirty(false);
        return true;
    }

    private SharedStreamSaveChoice ChooseSharedStreamAction(IReadOnlyList<string> siblings)
    {
        var patch = new TaskDialogCommandLinkButton("Patch sibling BARS", "Save updated prefetches for the listed BARS and the shared full stream.");
        var embed = new TaskDialogCommandLinkButton("Embed full recording in this BARS", "Keep siblings unchanged. Increases this BARS's size and may increase game memory usage.");
        var page = new TaskDialogPage
        {
            Caption = "Shared audio stream",
            Heading = "Other BARS use this replacement's external stream",
            Text = "Affected sibling BARS:\n" + string.Join("\n", siblings)
                + "\n\nChoose how to save the shared audio replacements. Embedding keeps their full audio inside this BARS instead of writing shared streams.",
            Icon = TaskDialogIcon.Warning,
            Buttons = { patch, embed, TaskDialogButton.Cancel },
            DefaultButton = TaskDialogButton.Cancel,
            AllowCancel = true
        };
        var result = TaskDialog.ShowDialog(this, page);
        return result == patch ? SharedStreamSaveChoice.PatchSiblings
            : result == embed ? SharedStreamSaveChoice.EmbedFullAudio : SharedStreamSaveChoice.Cancel;
    }

    private void ExportBwav(string name, AudioAsset asset)
    {
        if (WinFormsUtility.SaveFileDialog("Binary Waveform (*.bwav)|*.bwav", $"{name}.bwav", out var path))
            RunFileAction(() => AudioResourceWriter.SaveAsset(asset, path), "Could not export BWAV");
    }

    private void ExportMetadataMenuItem_Click(object sender, EventArgs e)
    {
        if (menuAsset?.AudioMetadata is not { } metadata) return;
        if (WinFormsUtility.SaveFileDialog("BWAV Metadata (*.bwav.meta)|*.bwav.meta", $"{metadata.AssetName}.bwav.meta", out var path))
            RunFileAction(() => File.WriteAllBytes(path, metadata.ToBytes()), "Could not save audio resource");
    }

    private void ExportWave(string name, AudioAsset asset)
    {
        if (!WinFormsUtility.SaveFileDialog("Waveform (*.wav)|*.wav", $"{name}.wav", out var path)) return;
        RunFileAction(() =>
        {
            var audio = GetAudioData(name, asset);
            using var output = new WaveFileWriter(path, new WaveFormat(audio.SampleRate, 16, audio.Channels));
            output.Write(MemoryMarshal.AsBytes(audio.Samples.AsSpan()));
        }, "Could not export WAV");
    }

    private bool RunFileAction(Action action, string errorTitle)
    {
        try { action(); return true; }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, errorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private void UpdateFileControls()
    {
        bool loaded = loadedFileName != null && !importing;
        UseWaitCursor = importing;
        fileToolStripMenuItem.Enabled = !importing;
        resourcesPanel.Enabled = loaded;
        previewPanel.Enabled = loaded;
        itemPropertyGrid.Enabled = loaded;
        saveToolStripMenuItem.Enabled = loaded;
        unloadToolStripMenuItem.Enabled = loaded;
        foreach (var session in loopEditors.Values) session.Editor.Enabled = loaded;
    }

    private void UnloadFile()
    {
        CloseLoopEditors();
        ResetPreview();
        itemPropertyGrid.SelectedObject = null;
        ClearTree();
        barsFile?.Dispose();
        standaloneAsset?.BinaryWave?.Dispose();
        standaloneAsset?.ExternalWave?.Dispose();
        barsFile = null;
        standaloneAsset = null;
        loadedFileName = null;
        SetDirty(false);
        singingCache = new(StringComparer.OrdinalIgnoreCase);
        UpdateFileControls();
    }

    private void UnloadToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (!importing && ConfirmDiscardChanges()) UnloadFile();
    }
}

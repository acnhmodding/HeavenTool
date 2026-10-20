using HeavenTool.IO.Audio;
using HeavenTool.IO.FileFormats.BARS;
using HeavenTool.IO.FileFormats.BWAV;
using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace HeavenTool.Forms.BARS;

public partial class BARSWindow
{
    private ImportLoopChoice? ChooseImportLoops(AudioAsset asset, bool bwav)
    {
        bool hasExisting = asset.BinaryWave!.Channels.Any(c => c.LoopEnd > c.LoopStart || c.LoopInfo.Length > 1);
        if (!hasExisting && !bwav) return ImportLoopChoice.Disable;
        var keep = new TaskDialogCommandLinkButton("Keep existing loop data where possible",
            "Adjust positions for the new sample rate. Shorter audio may shorten or disable the loop.") { Enabled = hasExisting };
        var imported = new TaskDialogCommandLinkButton("Use the imported BWAV's loop data");
        var disable = new TaskDialogCommandLinkButton("Disable looping", "Play the replacement once.");
        var page = new TaskDialogPage
        {
            Caption = "Import audio", Heading = "Which loop data should the replacement use?",
            Buttons = { keep, disable, TaskDialogButton.Cancel }, AllowCancel = true,
            DefaultButton = hasExisting ? keep : imported
        };
        if (bwav) page.Buttons.Insert(1, imported);
        var result = TaskDialog.ShowDialog(this, page);
        return result == keep ? ImportLoopChoice.KeepExisting
            : result == imported ? ImportLoopChoice.UseImported
            : result == disable ? ImportLoopChoice.Disable : null;
    }

    private readonly System.Collections.Generic.Dictionary<AudioAsset, LoopEditSession> loopEditors = [];

    private void EditLoop(AudioAsset asset)
    {
        if (importing || asset.BinaryWave == null) return;
        if (loopEditors.TryGetValue(asset, out var existing))
        {
            RunFileAction(existing.Refresh, "Could not refresh audio loop");
            existing.Editor.Activate();
            return;
        }
        RunFileAction(() =>
        {
            var session = new LoopEditSession(this, asset);
            loopEditors.Add(asset, session);
            session.Editor.Disposed += (_, _) =>
            {
                loopEditors.Remove(asset);
                session.Dispose();
            };
            session.Editor.Activated += (_, _) => SelectPreview(asset.AudioMetadata?.AssetName ?? StandaloneAssetName, asset);
            session.Editor.Show(dockPanel, WeifenLuo.WinFormsUI.Docking.DockState.Document);
        }, "Could not edit audio loop");
    }

    private bool ConfirmLoopEditorChanges()
    {
        foreach (var session in loopEditors.Values.ToArray())
            if (!session.Editor.ConfirmPendingChanges()) return false;
        return true;
    }

    private void RefreshLoopEditors()
    {
        foreach (var session in loopEditors.Values)
            RunFileAction(session.Refresh, "Could not refresh audio loop");
    }

    private void CloseLoopEditors()
    {
        foreach (var session in loopEditors.Values.ToArray())
            session.Editor.CloseWithoutPrompt();
    }

    private BinaryWaveFile LoadLoopWave(AudioAsset asset)
    {
        var full = asset.ExternalWave ?? asset.BinaryWave!;
        BinaryWaveFile? loaded = null;
        try
        {
            if (full.AssetType == AssetType.Prefetch || full.Channels.Any(c => c.LocalSampleCount != c.TotalSampleCount))
            {
                string name = asset.AudioMetadata?.AssetName ?? StandaloneAssetName;
                var path = new[] { Path.Combine(lastLoadedPath, "Stream", name + ".bwav"), Path.Combine(lastLoadedPath, name + ".bwav") }
                    .FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("Loop editing requires the full companion BWAV in the resource folder or its Stream folder.");
                loaded = full = new BinaryWaveFile(File.ReadAllBytes(path));
            }
            full.ValidateCompleteAudio(decode: false);
            return full.CreateSnapshot();
        }
        finally { loaded?.Dispose(); }
    }

    private async void SeekLoopEditor(AudioAsset asset, long frame)
    {
        SelectPreview(asset.AudioMetadata?.AssetName ?? StandaloneAssetName, asset);
        var task = previewTask;
        await task;
        if (!resourcesDisposed && ReferenceEquals(task, previewTask) && ReferenceEquals(asset, currentAudioAsset))
            RunFileAction(() => SeekPlayback(frame), "Could not seek audio");
    }

    private sealed class LoopEditSession : IDisposable
    {
        private readonly BARSWindow owner;
        private readonly AudioAsset asset;
        private BinaryWaveFile full;
        private BinaryWaveFile? sourceWave, sourceExternal;
        public BwavLoopEditor Editor { get; }

        public LoopEditSession(BARSWindow owner, AudioAsset asset)
        {
            this.owner = owner;
            this.asset = asset;
            full = owner.LoadLoopWave(asset);
            Editor = new BwavLoopEditor();
            Editor.GetCursor = () => ReferenceEquals(asset, owner.currentAudioAsset) && owner.currentWave != null
                ? owner.currentWave.GetPlaybackPosition(owner.GetPlayedFrames(owner.currentWave)) / owner.currentWave.WaveFormat.BlockAlign : null;
            Editor.SeekRequested = frame => owner.SeekLoopEditor(asset, frame);
            Editor.ApplyRequested = Apply;
            Editor.ReloadRequested = () => owner.RunFileAction(Reload, "Could not reload audio loop");
            Editor.SaveRequested = () => owner.SaveFileAs();
            UpdateEditor();
        }

        private void UpdateEditor()
        {
            sourceWave = asset.BinaryWave;
            sourceExternal = asset.ExternalWave;
            Editor.LoadWave(full, asset.AudioMetadata?.AssetName ?? owner.StandaloneAssetName);
        }

        private void Reload()
        {
            var replacement = owner.LoadLoopWave(asset);
            full.Dispose();
            full = replacement;
            UpdateEditor();
        }

        public void Refresh()
        {
            if (ReferenceEquals(sourceWave, asset.BinaryWave) && ReferenceEquals(sourceExternal, asset.ExternalWave)) return;
            if (Editor.HasChanges) Editor.MarkSourceChanged();
            else Reload();
        }

        private bool Apply()
        {
            if (owner.importing) return false;
            return owner.RunFileAction(() =>
            {
                if (!ReferenceEquals(sourceWave, asset.BinaryWave) || !ReferenceEquals(sourceExternal, asset.ExternalWave))
                    throw new InvalidOperationException("This recording changed after the loop editor was opened. Click Reload loop before editing it again.");
                if (!full.Channels.All(c => Editor.LoopStart is int start
                    ? c.LoopStart == start && c.LoopEnd == Editor.LoopEnd : c.LoopEnd <= c.LoopStart))
                {
                    using var replacement = full.WithLoop(Editor.LoopStart, Editor.LoopEnd);
                    asset.ReplaceWave(replacement);
                    owner.SetDirty(true);
                    owner.BuildTree(asset);
                }
                Reload();
            }, "Could not edit audio loop");
        }

        public void Dispose() => full.Dispose();
    }
}

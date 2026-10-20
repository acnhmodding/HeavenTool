using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace HeavenTool.Forms.BARS;

public partial class BARSWindow : Form
{
    private sealed record Singer(string Name, string FileName)
    {
        public bool Checked { get; set; }
    }

    private readonly List<Singer> Singers =
    [
        new("Girl", "Npc_Vocal_Girl.bars") {
            Checked = true
        },
        new("Boy", "Npc_Vocal_Boy.bars"),
        new("Man", "Npc_Vocal_Man.bars")
    ];

    private bool resourcesDisposed;

    private void DisposeResources()
    {
        if (resourcesDisposed) return;
        resourcesDisposed = true;
        UnloadFile();
        audioPlayer.Dispose();
        playbackTimer.Dispose();
        foreach (var content in dockContents) content.Dispose();
        dockContents.Clear();
    }

    public BARSWindow()
    {
        InitializeComponent();
        if (System.ComponentModel.LicenseManager.UsageMode == System.ComponentModel.LicenseUsageMode.Designtime) return;
        InitializeDocking();
        saveToolStripMenuItem.Click += (_, _) => SaveFileAs();
        waveViewer.SeekRequested += (_, frame) => RunFileAction(() => SeekPlayback(frame), "Could not seek audio");
        itemPropertyGrid.PropertyValueChanged += (_, _) =>
        {
            if (loadedFileName == null) return;
            SetDirty(true);
            previewSourceWave = null;
            waveViewer.SetMarkers(currentAudioAsset?.AudioMetadata?.MarkerList?.Markers ?? []);
            if (pendingPreview != null) RefreshPreview();
        };
        barsTreeView.NodeMouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Right) barsTreeView.SelectedNode = e.Node;
        };

        loopToolStripMenuItem.CheckedChanged += (_, _) =>
        {
            currentWave?.LoopEnabled = loopToolStripMenuItem.Checked;
        };

        whistlingToolStripMenuItem.CheckedChanged += (_, _) => RefreshPreview();
        baseToolStripMenuItem.CheckedChanged += (_, _) => RefreshPreview();

        timeLabel.Text = "";
        barsTreeView.AfterSelect += BarsTreeView_AfterSelect;

        UpdateFileControls();
        playButton.Enabled = false;

        audioPlayer.PlaybackStopped += AudioPlayer_PlaybackStopped;

        volumeSlider.Volume = audioPlayer.Volume;
        volumeSlider.VolumeChanged += (_, e) => audioPlayer.Volume = volumeSlider.Volume;
        playbackTimer.Interval = 30;
        playbackTimer.Tick += (_, _) => UpdateTimeLabel();

        barsTreeView.DrawMode = TreeViewDrawMode.OwnerDrawText;
        barsTreeView.DrawNode += (_, e) =>
        {
            var searchText = searchBox.Text;
            if (string.IsNullOrEmpty(searchText) || e.Node == null)
            {
                e.DrawDefault = true;
                return;
            }

            if (e.Node.Text.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                e.Graphics.FillRectangle(Brushes.DarkGray, e.Bounds);
                TextRenderer.DrawText(e.Graphics, e.Node.Text, barsTreeView.Font, e.Bounds, Color.White, TextFormatFlags.GlyphOverhangPadding);
            }
            else
            {
                e.DrawDefault = true;
            }
        };

        FormClosing += (_, e) =>
        {
            if (!e.Cancel) e.Cancel = importing || !ConfirmDiscardChanges();
        };
    }

    private void LoadSingers()
    {
        while (singersToolStripMenuItem.DropDownItems.Count > 0)
            singersToolStripMenuItem.DropDownItems[0].Dispose();

        var noSinger = new ToolStripMenuItem("No Singers")
        {
            Checked = Singers.All(x => !x.Checked)
        };

        singersToolStripMenuItem.DropDownItems.Add(noSinger);
        singersToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());

        var allSingers = new Dictionary<ToolStripMenuItem, Singer>();
        foreach (var singer in Singers)
        {
            var item = new ToolStripMenuItem(singer.Name)
            {
                Enabled = File.Exists(Path.Combine(lastLoadedPath, singer.FileName)),
                Checked = singer.Checked
            };
            item.Click += (_, _) =>
            {
                singer.Checked = !singer.Checked;
                item.Checked = singer.Checked;
                noSinger.Checked = Singers.All(x => !x.Checked);
                RefreshPreview();
            };
            singersToolStripMenuItem.DropDownItems.Add(item);
            allSingers.Add(item, singer);
        }

        noSinger.Click += (_, _) =>
        {
            noSinger.Checked = true;
            foreach (var (item, singer) in allSingers)
            {
                singer.Checked = false;
                item.Checked = false;
            }
            RefreshPreview();
        };

    }

    private void SearchBox_TextChanged(object sender, EventArgs e) => barsTreeView.Invalidate();

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
            searchBox.Clear();
        else if (e.KeyCode == Keys.Enter)
            FindSearchMatch(e.Shift);
        else return;
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void FindSearchMatch(bool backwards)
    {
        var text = searchBox.Text.Trim();
        if (text.Length == 0 || barsTreeView.Nodes.Count == 0) return;
        var selected = barsTreeView.SelectedNode;
        while (selected?.Parent != null) selected = selected.Parent;
        int start = selected?.Index ?? (backwards ? 0 : -1);
        int count = barsTreeView.Nodes.Count;
        for (int offset = 1; offset <= count; offset++)
        {
            int index = (start + (backwards ? -offset : offset) + count) % count;
            var node = barsTreeView.Nodes[index];
            if (!node.Text.Contains(text, StringComparison.OrdinalIgnoreCase)) continue;
            barsTreeView.SelectedNode = node;
            node.EnsureVisible();
            return;
        }
    }

    private void ReadPrefetchedFilesToolStripMenuItem_Click(object sender, EventArgs e)
    {
        readPrefetchedFilesToolStripMenuItem.Checked = !readPrefetchedFilesToolStripMenuItem.Checked;
        RefreshPreview(preservePlayback: false);
    }
}

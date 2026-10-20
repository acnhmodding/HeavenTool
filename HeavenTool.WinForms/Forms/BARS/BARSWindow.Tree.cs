using HeavenTool.IO.FileFormats.BARS;
using HeavenTool.IO.FileFormats.BARS.MINF;
using HeavenTool.IO.FileFormats.BARS.MINF.Sections.PerformanceTracks;
using HeavenTool.IO.FileFormats.BWAV;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace HeavenTool.Forms.BARS;

public partial class BARSWindow
{
    private void BuildTree(AudioAsset? selectedAsset = null)
    {
        RefreshLoopEditors();
        TreeNode? selectedNode = null;
        var nodes = new List<TreeNode>();
        barsTreeView.BeginUpdate();
        try
        {
            ClearTree();
            itemPropertyGrid.SelectedObject = null;
            var assets = barsFile?.AudioAssets ?? (standaloneAsset is { } standalone ? [standalone] : []);
            foreach (var asset in assets)
            {
                var node = CreateAssetNode(barsFile != null ? asset.ToString() : StandaloneAssetName, asset);
                if (node != null) nodes.Add(node);
                if (ReferenceEquals(asset, selectedAsset)) selectedNode = node;
            }
            barsTreeView.Nodes.AddRange([.. nodes]);
        }
        finally { barsTreeView.EndUpdate(); }
        if (selectedNode != null) barsTreeView.SelectedNode = selectedNode;
    }

    private sealed class AssetTreeNode(string name, AudioAsset asset) : TreeNode(name)
    {
        internal AudioAsset Asset { get; } = asset;
        internal bool Populated { get; set; }
    }

    private AudioAsset? menuAsset;
    private string? menuAssetName;
    private TreeNode? contextNode;

    private void UpdateNodeContextMenu(TreeNode? node)
    {
        if (contextNode != null) contextNode.ContextMenuStrip = null;
        contextNode = node;
        if (node is AssetTreeNode) node.ContextMenuStrip = assetContextMenu;
        else if (node?.Parent is AssetTreeNode && node.Text == "Metadata")
            node.ContextMenuStrip = metadataContextMenu;
    }

    private static AssetTreeNode? GetAssetNode(TreeNode? node)
    {
        while (node != null && node is not AssetTreeNode) node = node.Parent;
        return node as AssetTreeNode;
    }

    private void BarsTreeView_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        UpdateNodeContextMenu(e.Node);
        itemPropertyGrid.SelectedObject = e.Node?.Tag;
        if (e.Node?.Tag is BinaryWaveFile or AudioMetadata or MarkerList or MusicInfo
            && GetAssetNode(e.Node) is { } entry)
            SelectPreview(entry.Text, entry.Asset);
    }

    private static TreeNode? AddPropertyNode(TreeNodeCollection nodes, string name, object? value)
    {
        if (value == null) return null;
        var node = new TreeNode(name) { Tag = value };
        nodes.Add(node);
        return node;
    }

    private static TreeNode? CreateAssetNode(string name, AudioAsset asset)
    {
        if (asset.BinaryWave is not { } wave) return null;
        var node = new AssetTreeNode(name, asset) { Tag = wave };
        node.Nodes.Add(new TreeNode());
        return node;
    }

    private void BarsTreeView_BeforeExpand(object sender, TreeViewCancelEventArgs e)
    {
        if (e.Node != null) PopulateAssetNode(e.Node);
    }

    private void PopulateAssetNode(TreeNode node)
    {
        if (node is not AssetTreeNode { Populated: false } entry) return;
        var asset = entry.Asset;
        node.Nodes.Clear();
        if (asset.AudioMetadata is { } metadata)
        {
            var metadataNode = AddPropertyNode(node.Nodes, "Metadata", metadata)!;
            AddPropertyNode(metadataNode.Nodes, "Markers", metadata.MarkerList);
            if (metadata.MusicInfo is { } music)
                AddMusicNodes(AddPropertyNode(metadataNode.Nodes, "Music Info (MINF)", music)!.Nodes, music);
        }
        for (int i = 0; i < asset.BinaryWave!.Channels.Length; i++)
            AddPropertyNode(node.Nodes, $"Channel #{i}", asset.BinaryWave.Channels[i]);
        entry.Populated = true;
    }

    private void AddMusicNodes(TreeNodeCollection nodes, MusicInfo music)
    {
        AddPropertyNode(nodes, "Tempo Map", music.TempoMap);
        AddPropertyNode(nodes, "Chord Track", music.ChordTrack);
        AddPropertyNode(nodes, "Beat Map", music.BeatMap);
        AddPropertyNode(nodes, "Measure Map", music.MeasureMap);
        if (music.NamedMarkerTracks is { } markers)
        {
            var parent = AddPropertyNode(nodes, "Named Marker Tracks", markers)!;
            foreach (var track in markers.Tracks) AddPropertyNode(parent.Nodes, track.Name, track);
        }
        if (music.NamedEventTracks is { } events)
        {
            var parent = AddPropertyNode(nodes, "Named Event Tracks", events)!;
            foreach (var track in events.Tracks) AddPropertyNode(parent.Nodes, track.Name, track);
        }
        if (music.PerformanceSequence is { } sequence)
        {
            var parent = AddPropertyNode(nodes, "Performance Sequence", sequence)!;
            AddPropertyNode(parent.Nodes, "Vocals / Whistle", sequence.Vocals);
            if (sequence.PitchBends.Length > 0) AddPropertyNode(parent.Nodes, "Pitch Bend Tracks", sequence.PitchBends);
            if (sequence.Vibrato.Length > 0) AddPropertyNode(parent.Nodes, "Vibrato Tracks", sequence.Vibrato);
            foreach (var (instrument, track) in sequence.Instruments.OrderBy(pair => pair.Key))
                AddPropertyNode(parent.Nodes, instrument == InstrumentType.AdditionalTrack ? "Additional Track" : instrument.ToString(), track);
        }
    }

    private void EntryMenu_Opening(object sender, CancelEventArgs e)
    {
        var node = GetAssetNode(barsTreeView.SelectedNode);
        menuAsset = node?.Asset;
        menuAssetName = node?.Text;
        e.Cancel = importing || menuAsset?.BinaryWave == null;
    }

    private void ExportBwavMenuItem_Click(object sender, EventArgs e)
    {
        if (menuAsset != null) ExportBwav(menuAssetName!, menuAsset);
    }

    private void ExportWaveMenuItem_Click(object sender, EventArgs e)
    {
        if (menuAsset != null) ExportWave(menuAssetName!, menuAsset);
    }

    private void ImportAudioMenuItem_Click(object sender, EventArgs e)
    {
        if (menuAsset != null) ImportAudio(menuAsset);
    }

    private void EditLoopMenuItem_Click(object sender, EventArgs e)
    {
        if (menuAsset != null) EditLoop(menuAsset);
    }

    private void ImportMetadataMenuItem_Click(object sender, EventArgs e)
    {
        if (menuAsset != null) ImportMetadata(menuAsset);
    }

    private void ClearTree()
    {
        assetContextMenu.Close();
        metadataContextMenu.Close();
        menuAsset = null;
        menuAssetName = null;
        UpdateNodeContextMenu(null);
        barsTreeView.Nodes.Clear();
    }
}

using HeavenTool.IO.FileFormats.BWAV;
using System;
using System.Linq;
using System.ComponentModel;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace HeavenTool.Forms.BARS;

public partial class BwavLoopEditor : DockContent
{
    private int sampleRate = 48000;
    private bool updating, loaded, closingWithoutPrompt, originalEnabled, mixedLoops, sourceChanged;
    private decimal originalStart, originalEnd;
    private string assetName = "Audio";

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Func<long?>? GetCursor { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Action<long>? SeekRequested { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Func<bool>? ApplyRequested { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Action? ReloadRequested { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Action? SaveRequested { get; set; }
    internal int? LoopStart => enabled.Checked ? (int)start.Value : null;
    internal int? LoopEnd => enabled.Checked ? (int)end.Value : null;
    internal bool HasChanges => loaded && (enabled.Checked != originalEnabled
        || (enabled.Checked && (start.Value != originalStart || end.Value != originalEnd)));

    public BwavLoopEditor()
    {
        InitializeComponent();
        UpdateState();
    }

    internal void LoadWave(BinaryWaveFile wave, string name)
    {
        var channel = wave.Channels[0];
        int samples = channel.LocalSampleCount;
        if (samples <= 0 || channel.SampleRate <= 0)
            throw new InvalidOperationException("Loop editing requires a non-empty recording.");
        updating = true;
        try
        {
            assetName = name;
            sampleRate = channel.SampleRate;
            start.Value = 0;
            end.Value = 1;
            start.Maximum = samples - 1;
            end.Maximum = samples;
            originalStart = start.Value = Math.Clamp(channel.LoopStart, 0, samples - 1);
            originalEnd = end.Value = channel.LoopEnd > 0 ? Math.Min(channel.LoopEnd, samples) : samples;
            originalEnabled = enabled.Checked = channel.LoopEnd > channel.LoopStart;
            loaded = true;
            sourceChanged = false;
            audioInfoLabel.Text = $"{name}\n{samples:N0} samples · {sampleRate:N0} Hz · {(double)samples / sampleRate:0.000} seconds · {wave.Channels.Length} channel(s)";
            mixedLoops = wave.Channels.Any(c => c.LoopStart != channel.LoopStart || c.LoopEnd != channel.LoopEnd);
            mixedChannelsLabel.Visible = mixedLoops;
        }
        finally { updating = false; }
        UpdateState();
    }

    private void LoopValueChanged(object sender, EventArgs e)
    {
        if (!updating) UpdateState();
    }

    private void TimeValueChanged(object sender, EventArgs e)
    {
        if (updating) return;
        var target = ReferenceEquals(sender, startTime) ? start : end;
        var seconds = (NumericUpDown)sender;
        target.Value = Math.Clamp(decimal.Round(seconds.Value * sampleRate, 0, MidpointRounding.AwayFromZero), target.Minimum, target.Maximum);
        UpdateState();
    }

    private void StartCursor_Click(object sender, EventArgs e) => UseCursor(start);
    private void EndCursor_Click(object sender, EventArgs e) => UseCursor(end);
    private void UseCursor(NumericUpDown target)
    {
        if (GetCursor?.Invoke() is not long frame)
        {
            statusLabel.Text = "This recording's preview is not ready. Select it and wait for loading to finish.";
            return;
        }
        target.Value = Math.Clamp(frame, (long)target.Minimum, (long)target.Maximum);
    }

    private void StartSeek_Click(object sender, EventArgs e) => SeekRequested?.Invoke((long)start.Value);
    private void EndSeek_Click(object sender, EventArgs e) => SeekRequested?.Invoke((long)end.Value);
    private void Reload_Click(object sender, EventArgs e) => ReloadRequested?.Invoke();
    private void Close_Click(object sender, EventArgs e) => Close();
    private void Apply_Click(object sender, EventArgs e) => ApplyChanges();
    private void Entire_Click(object sender, EventArgs e)
    {
        start.Value = 0;
        end.Value = end.Maximum;
        enabled.Checked = true;
    }

    internal bool ApplyChanges()
    {
        ValidateChildren();
        if (sourceChanged) return false;
        if (!HasChanges && !mixedLoops) return true;
        return (!enabled.Checked || start.Value < end.Value) && ApplyRequested?.Invoke() == true;
    }

    internal bool ConfirmPendingChanges()
    {
        if (!HasChanges) return true;
        Activate();
        var choice = MessageBox.Show(this, $"Apply loop changes to {assetName}?", "Unapplied loop changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (choice == DialogResult.Yes) return ApplyChanges();
        if (choice != DialogResult.No) return false;
        updating = true;
        enabled.Checked = originalEnabled;
        start.Value = originalStart;
        end.Value = originalEnd;
        updating = false;
        UpdateState();
        return true;
    }

    internal void CloseWithoutPrompt()
    {
        closingWithoutPrompt = true;
        Close();
        Dispose();
    }

    internal void MarkSourceChanged()
    {
        sourceChanged = true;
        UpdateState();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!closingWithoutPrompt && !ConfirmPendingChanges()) e.Cancel = true;
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.S)) { SaveRequested?.Invoke(); return true; }
        if (keyData == (Keys.Control | Keys.Enter)) { ApplyChanges(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void UpdateState()
    {
        updating = true;
        startTime.Value = decimal.Round(start.Value / sampleRate, 6);
        endTime.Value = decimal.Round(end.Value / sampleRate, 6);
        updating = false;
        bool editable = loaded && enabled.Checked;
        start.Enabled = end.Enabled = startTime.Enabled = endTime.Enabled = editable;
        startCursor.Enabled = endCursor.Enabled = startSeek.Enabled = endSeek.Enabled = editable;
        enabled.Enabled = entire.Enabled = reload.Enabled = loaded;
        bool valid = !enabled.Checked || start.Value < end.Value;
        apply.Enabled = valid && loaded && !sourceChanged;
        durationLabel.Text = enabled.Checked && valid
            ? $"Loop duration: {end.Value - start.Value:N0} samples · {(end.Value - start.Value) / sampleRate:0.000000} seconds"
            : enabled.Checked ? "Invalid loop range" : "Looping disabled";
        statusLabel.Text = sourceChanged ? "This recording changed. Reload loop before applying these edits."
            : valid ? "Apply updates all channels. Save the resource to write changes to disk."
            : "The end sample must be greater than the start sample.";
        statusLabel.ForeColor = sourceChanged || !valid ? System.Drawing.Color.LightSalmon : ForeColor;
        Text = $"Loop · {assetName}{(HasChanges ? " *" : "")}";
        TabText = Text;
    }
}

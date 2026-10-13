using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using static HeavenTool.IO.FileFormats.BARS.MarkerList;

namespace HeavenTool.Forms.Components;

public partial class CustomWaveViewer : UserControl
{
    private WaveStream? waveStream;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public WaveStream? PlaybackStream { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long? PlaybackPosition { get; set; }

    private int samplesPerPixel = 128;
    private long startPosition;
    private int bytesPerSample;

    private Color waveColor = Color.White;
    private Pen? penColor;

    private bool isDragging;
    private Point lastMousePos;

    private readonly Dictionary<int, PeakData> peakCache = [];
    private readonly HashSet<int> pendingPeaks = [];
    private int waveRevision;
    private readonly List<MarkerEntry> markers = [];
    private int loopStart = -1;
    private int loopEnd = -1;

    private class PeakData
    {
        public Peak[] Peaks = [];
        public long BytesPerPixel;
    }

    private struct Peak
    {
        public float Min;
        public float Max;
    }

    public CustomWaveViewer()
    {
        InitializeComponent();
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public WaveStream? WaveStream
    {
        get => waveStream;
        set
        {
            waveStream = value;
            waveRevision++;

            peakCache.Clear();
            pendingPeaks.Clear();
            StartPosition = 0;

            if (waveStream != null)
            {
                waveStream.Position = 0;

                bytesPerSample = waveStream.WaveFormat.BitsPerSample / 8 * waveStream.WaveFormat.Channels;

                _ = BuildPeaksAsync(samplesPerPixel);
            }

            Invalidate();
        }
    }

    [DefaultValue(128)]
    public int SamplesPerPixel
    {
        get => samplesPerPixel;
        set
        {
            if (value < 16) value = 16;
            if (value > 4096) value = 4096;

            if (samplesPerPixel == value)
                return;

            samplesPerPixel = value;
            ClampStart();

            if (waveStream != null && !peakCache.ContainsKey(value))
            {
                _ = BuildPeaksAsync(value);
            }

            Invalidate();
        }
    }

    [DefaultValue(0)]
    public long StartPosition
    {
        get => startPosition;
        set
        {
            startPosition = value;
            ClampStart();
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color WaveColor
    {
        get => waveColor;
        set
        {
            waveColor = value;

            penColor?.Dispose();
            penColor = new Pen(waveColor, 1f);

            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (waveStream == null)
            return;

        if (!peakCache.TryGetValue(samplesPerPixel, out var data))
            return;

        penColor ??= new Pen(waveColor, 1f);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);

        DrawCenterLine(e.Graphics);

        var bytesPerPixel = data.BytesPerPixel;

        var firstPixel = (int)(startPosition / bytesPerPixel);

        var mid = Height / 2f;
        var scale = Height / 2f;

        for (var x = 0; x < Width; x++)
        {
            var index = firstPixel + x;

            if (index < 0 || index >= data.Peaks.Length)
                continue;

            var p = data.Peaks[index];

            var y1 = mid - (p.Max * scale);
            var y2 = mid - (p.Min * scale);

            e.Graphics.DrawLine(penColor, x, y1, x, y2);
        }

        DrawLoop(e.Graphics, bytesPerPixel);
        DrawMarkers(e.Graphics, bytesPerPixel);
        DrawPlayhead(e.Graphics, bytesPerPixel);
    }

    private void DrawCenterLine(Graphics g)
    {
        using Pen p = new(Color.FromArgb(50, Color.Gray));
        g.DrawLine(p, 0, Height / 2, Width, Height / 2);
    }

    private void DrawLoop(Graphics g, long bytesPerPixel)
    {
        if (loopEnd == -1) return;

        var loopStartPosition = (int)(((long)loopStart * bytesPerSample - startPosition) / bytesPerPixel);
        var loopEndPosition = (int)(((long)loopEnd * bytesPerSample - startPosition) / bytesPerPixel);

        using var markerPen = new Pen(Color.Green, 2f);

        g.DrawLine(markerPen, loopStartPosition, 0, loopStartPosition, Height);
        g.DrawLine(markerPen, loopEndPosition, 0, loopEndPosition, Height);
    }

    private void DrawMarkers(Graphics g, long bytesPerPixel)
    {
        if (SamplesPerPixel >= 2048)
            return;

        foreach (var marker in markers)
        {
            var markerX = (int)((marker.StartPosition * bytesPerSample - startPosition) / bytesPerPixel);

            if (markerX >= 0 && markerX < Width)
            {
                using var markerPen = new Pen(Color.Red, 2f);
                g.DrawLine(markerPen, markerX, Height / 2, markerX, Height);
                if (SamplesPerPixel <= 128)
                    g.DrawString(marker.Name, Font, Brushes.Red, markerX + 2, (Height / 2) + 2);
            }
        }
    }

    private void DrawPlayhead(Graphics g, long bytesPerPixel)
    {
        if (waveStream == null)
            return;

        var position = PlaybackPosition ?? (PlaybackStream ?? waveStream).Position;
        var x = (int)((position - startPosition) / bytesPerPixel);

        if (x >= 0 && x < Width)
        {
            using var pen = new Pen(Color.Yellow, 2f); // playhead color
            g.DrawLine(pen, x, 0, x, Height);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (e.Delta > 0)
            ZoomIn();
        else
            ZoomOut();

        base.OnMouseWheel(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            isDragging = true;
            lastMousePos = e.Location;
            Cursor = Cursors.Hand;
        }

        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (isDragging && waveStream != null)
        {
            var dx = e.X - lastMousePos.X;

            long bytesPerPixel = samplesPerPixel * bytesPerSample;

            StartPosition -= (long)(dx * bytesPerPixel * 1.1f);

            lastMousePos = e.Location;
        }

        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            isDragging = false;
            Cursor = Cursors.Default;
        }

        base.OnMouseUp(e);
    }

    public void ZoomIn()
    {
        SamplesPerPixel = samplesPerPixel / 2;
    }

    public void ZoomOut()
    {
        SamplesPerPixel = samplesPerPixel * 2;
    }

    public void ResetZoom()
    {
        SamplesPerPixel = 128;
    }

    public void AddMarker(MarkerEntry marker)
    {
        markers.Add(marker);
        ClampStart();
        Invalidate();
    }

    public void ClearMarkers()
    {
        markers.Clear();
        Invalidate();
    }
    public void SetLoop(int start, int end)
    {
        loopStart = start;
        loopEnd = end;
        Invalidate();
    }

    public void RemoveLoop()
    {
        loopStart = -1;
        loopEnd = -1;
        Invalidate();
    }

    private async Task BuildPeaksAsync(int spp)
    {
        var stream = waveStream;
        var revision = waveRevision;
        if (stream == null || !pendingPeaks.Add(spp))
            return;

        var peaks = await Task.Run(() => BuildPeaks(stream, spp));
        if (IsDisposed || revision != waveRevision || stream != waveStream) return;
        pendingPeaks.Remove(spp);
        peakCache[spp] = peaks;
        Invalidate();
    }

    private static PeakData BuildPeaks(WaveStream stream, int spp)
    {
        var isFloat =
            stream.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat;

        var channels = stream.WaveFormat.Channels;
        var bits = stream.WaveFormat.BitsPerSample;

        var bytesPerFrame = stream.WaveFormat.BlockAlign;

        var bytesPerPixel = (long)spp * bytesPerFrame;

        List<Peak> peaks = [];

        var buffer = new byte[spp * bytesPerFrame];

        lock (stream)
        {
            var oldPosition = stream.Position;
            stream.Position = 0;

            while (true)
            {
                var read = stream.Read(buffer, 0, buffer.Length);

                if (read == 0)
                    break;

                var min = 1f;
                var max = -1f;

                if (isFloat && bits == 32)
                {
                    for (var i = 0; i + bytesPerFrame <= read; i += bytesPerFrame)
                    {
                        for (var c = 0; c < channels; c++)
                        {
                            var s = BitConverter.ToSingle(
                                buffer,
                                i + (c * 4));

                            if (!float.IsFinite(s)) continue;
                            min = Math.Min(min, s);
                            max = Math.Max(max, s);
                        }

                    }
                }
                else if (bits == 16)
                {
                    for (var i = 0; i + bytesPerFrame <= read; i += bytesPerFrame)
                    {
                        for (var c = 0; c < channels; c++)
                        {
                            var s = BitConverter.ToInt16(
                                buffer,
                                i + (c * 2));

                            var sample = s / 32768f;
                            min = Math.Min(min, sample);
                            max = Math.Max(max, sample);
                        }

                    }
                }
                else
                {
                    continue;
                }

                peaks.Add(new Peak
                {
                    Min = min,
                    Max = max
                });
            }

            stream.Position = oldPosition;
        }

        return new PeakData
        {
            Peaks = [.. peaks],
            BytesPerPixel = bytesPerPixel
        };
    }

    private void ClampStart()
    {
        if (waveStream == null)
            return;

        var length = waveStream.Length;

        // Prefetched songs may have no local samples; markers still define a scrollable range.
        if (length == 0 && markers.Count > 0)
            foreach (var marker in markers)
                length = Math.Max(length, ((long)marker.StartPosition + marker.Length + 2) * bytesPerSample);

        var visibleBytes = (long)Width * samplesPerPixel * bytesPerSample;

        var max = Math.Max(0, length - visibleBytes);

        startPosition = Math.Clamp(startPosition, 0, max);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ClampStart();
    }
}

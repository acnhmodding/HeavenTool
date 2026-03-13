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

    private int samplesPerPixel = 128;
    private long startPosition;
    private int bytesPerSample;

    private Color waveColor = Color.White;
    private Pen? penColor;

    private bool isDragging;
    private Point lastMousePos;

    private readonly Dictionary<int, PeakData> peakCache = [];
    private readonly List<MarkerEntry> markers = [];

    private class PeakData
    {
        public Peak[] Peaks = Array.Empty<Peak>();
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

    // ============================================================
    // Properties
    // ============================================================

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public WaveStream? WaveStream
    {
        get => waveStream;
        set
        {
            waveStream = value;

            peakCache.Clear();
            StartPosition = 0;

            if (waveStream != null)
            {
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

    // ============================================================
    // Painting
    // ============================================================

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

        long bytesPerPixel = data.BytesPerPixel;

        int firstPixel = (int)(startPosition / bytesPerPixel);

        float mid = Height / 2f;
        float scale = Height / 2f;

        for (int x = 0; x < Width; x++)
        {
            int index = firstPixel + x;

            if (index < 0 || index >= data.Peaks.Length)
                continue;

            var p = data.Peaks[index];

            float y1 = mid - (p.Max * scale);
            float y2 = mid - (p.Min * scale);

            e.Graphics.DrawLine(penColor, x, y1, x, y2);
        }

        DrawMarkers(e, bytesPerPixel);
    }

    private void DrawMarkers(PaintEventArgs e, long bytesPerPixel)
    {
        foreach (var marker in markers)
        {
            // Convert marker StartPosition to pixel coordinate
            int markerX = (int)((marker.StartPosition - startPosition) / bytesPerPixel);

            if (markerX >= 0 && markerX < Width)
            {
                using var markerPen = new Pen(Color.Red, 2f); // You can customize the color and width
                e.Graphics.DrawLine(markerPen, markerX, 0, markerX, Height);
                if (SamplesPerPixel <= 128)
                    e.Graphics.DrawString(marker.Name, Font, Brushes.Red, markerX + 2, 2); // Optional: Display marker name
            }
        }
    }

    private void DrawCenterLine(Graphics g)
    {
        using Pen p = new(Color.FromArgb(50, Color.Gray));

        g.DrawLine(
            p,
            0,
            Height / 2,
            Width,
            Height / 2
        );
    }

    // ============================================================
    // Mouse / Zoom
    // ============================================================

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
            int dx = e.X - lastMousePos.X;

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
    }

    public void ClearMarkers()
    {
        markers.Clear();
    }

    // ============================================================
    // Peak Building
    // ============================================================

    private async Task BuildPeaksAsync(int spp)
    {
        if (waveStream == null)
            return;

        await Task.Run(() =>
        {
            lock (peakCache)
            {
                if (peakCache.ContainsKey(spp))
                    return;

                var peaks = BuildPeaks(spp);

                peakCache[spp] = peaks;
            }
        });

        if (!IsDisposed)
            BeginInvoke(new Action(Invalidate));
    }

    private PeakData BuildPeaks(int spp)
    {
        if (waveStream == null)
            return new PeakData();

        WaveStream stream = waveStream;

        bool isFloat =
            stream.WaveFormat.Encoding == WaveFormatEncoding.IeeeFloat;

        int channels = stream.WaveFormat.Channels;
        int bits = stream.WaveFormat.BitsPerSample;

        int bytesPerFrame = bytesPerSample;

        long bytesPerPixel = (long)spp * bytesPerFrame;

        List<Peak> peaks = [];

        byte[] buffer = new byte[spp * bytesPerFrame];

        lock (stream)
        {
            stream.Position = 0;

            while (true)
            {
                int read = stream.Read(buffer, 0, buffer.Length);

                if (read == 0)
                    break;

                float min = 1f;
                float max = -1f;

                if (isFloat && bits == 32)
                {
                    for (int i = 0; i < read; i += 4 * channels)
                    {
                        float sum = 0;

                        for (int c = 0; c < channels; c++)
                        {
                            float s = BitConverter.ToSingle(
                                buffer,
                                i + (c * 4));

                            sum += s;
                        }

                        float sample = sum / channels;

                        min = Math.Min(min, sample);
                        max = Math.Max(max, sample);
                    }
                }
                else if (bits == 16)
                {
                    for (int i = 0; i < read; i += 2 * channels)
                    {
                        float sum = 0;

                        for (int c = 0; c < channels; c++)
                        {
                            short s = BitConverter.ToInt16(
                                buffer,
                                i + (c * 2));

                            sum += s / 32768f;
                        }

                        float sample = sum / channels;

                        min = Math.Min(min, sample);
                        max = Math.Max(max, sample);
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
        }

        return new PeakData
        {
            Peaks = [.. peaks],
            BytesPerPixel = bytesPerPixel
        };
    }

    // ============================================================
    // Helpers
    // ============================================================

    private void ClampStart()
    {
        if (waveStream == null)
            return;

        var length = waveStream.Length;

        // If stream is empty (bgm are usually empty) use marker data to determine the length of the stream, so that user can scroll to the end of the bgm
        if (length == 0 && markers.Count > 0)
            length = markers[^1].StartPosition + markers[^1].Length + 2;

        var visibleBytes = Width * samplesPerPixel * bytesPerSample;

        var max = Math.Max(0, length - visibleBytes);

        startPosition = Math.Clamp(startPosition, 0, max);
    }
}
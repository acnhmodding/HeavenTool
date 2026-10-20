using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static HeavenTool.IO.FileFormats.BARS.MarkerList;

namespace HeavenTool.Forms.Components;

public partial class CustomWaveViewer : UserControl
{
    private WaveStream? waveStream;
    private WaveformPeaks? peaks;
    private CancellationTokenSource? peakCancellation;
    private Bitmap? waveformImage;
    private string? waveformError;
    private readonly List<MarkerEntry> markers = [];
    private long markerEnd;
    private int samplesPerPixel = 128;
    private long startPosition;
    private int bytesPerFrame;
    private long? playbackPosition;
    private Color waveColor = Color.White;
    private bool isDragging;
    private bool mouseDown;
    private Point mouseDownPosition;
    private int lastMouseX;
    private int loopStart = -1;
    private int loopEnd = -1;
    private long BytesPerPixel => (long)samplesPerPixel * bytesPerFrame;

    public CustomWaveViewer()
    {
        InitializeComponent();
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
    }

    /// <summary>Requests a seek to an absolute sample frame.</summary>
    public event EventHandler<long>? SeekRequested;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public WaveStream? PlaybackStream { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public long? PlaybackPosition
    {
        get => playbackPosition;
        set
        {
            if (playbackPosition == value) return;
            InvalidatePlayhead();
            playbackPosition = value;
            InvalidatePlayhead();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public WaveStream? WaveStream
    {
        get => waveStream;
        set
        {
            Capture = false;
            CancelPeakGeneration();
            waveStream = value;
            peaks = null;
            waveformError = null;
            bytesPerFrame = value?.WaveFormat.BlockAlign ?? 0;
            startPosition = 0;
            InvalidateWaveform();
            if (value != null && !IsDisposed)
            {
                peakCancellation = new CancellationTokenSource();
                _ = BuildPeaksAsync(value, peakCancellation.Token);
            }
        }
    }

    [DefaultValue(128)]
    [Description("Frames per pixel, rounded up to a power of two between 16 and 4096.")]
    public int SamplesPerPixel
    {
        get => samplesPerPixel;
        set
        {
            value = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Clamp(value,
                WaveformPeaks.MinimumSamplesPerPixel, WaveformPeaks.MaximumSamplesPerPixel));
            if (samplesPerPixel == value) return;
            samplesPerPixel = value;
            ClampStart();
            InvalidateWaveform();
        }
    }

    [DefaultValue(0L)]
    public long StartPosition
    {
        get => startPosition;
        set
        {
            long previous = startPosition;
            startPosition = value;
            ClampStart();
            if (startPosition != previous) InvalidateWaveform();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color WaveColor
    {
        get => waveColor;
        set
        {
            if (waveColor == value) return;
            waveColor = value;
            InvalidateWaveform();
        }
    }

    private void InvalidateWaveform()
    {
        waveformImage?.Dispose();
        waveformImage = null;
        Invalidate();
    }

    private double GetX(long position) => BytesPerPixel == 0 ? -1 : (position - startPosition) / (double)BytesPerPixel;
    private double PlayheadX => GetX(playbackPosition ?? PlaybackStream?.Position ?? 0);

    private void InvalidatePlayhead()
    {
        double x = PlayheadX;
        if (x >= -2 && x <= Width + 2) Invalidate(new Rectangle((int)Math.Floor(x) - 2, 0, 5, Height));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (waveStream == null || Width <= 0 || Height <= 0 || bytesPerFrame == 0) return;
        if (waveformImage == null)
        {
            waveformImage = new Bitmap(Width, Height);
            using var graphics = Graphics.FromImage(waveformImage);
            graphics.Clear(BackColor);
            DrawWaveform(graphics);
        }
        e.Graphics.DrawImageUnscaled(waveformImage, 0, 0);
        double x = PlayheadX;
        if (x >= 0 && x < Width)
            e.Graphics.DrawLine(Pens.Yellow, (float)x, 0, (float)x, Height);
    }

    private void DrawWaveform(Graphics graphics)
    {
        float middle = Height / 2f;
        graphics.DrawLine(Pens.DimGray, 0, middle, Width, middle);
        if (peaks != null)
        {
            var level = peaks.GetLevel(samplesPerPixel);
            long first = startPosition / BytesPerPixel;
            double offset = (startPosition % BytesPerPixel) / (double)BytesPerPixel;
            using var pen = new Pen(waveColor);
            int count = (int)Math.Min(Width + 1L, level.LongLength - first);
            for (int i = 0; i < count; i++)
            {
                var peak = level[(int)(first + i)];
                float x = (float)(i - offset);
                graphics.DrawLine(pen, x, middle - Math.Clamp(peak.Max, -1, 1) * middle,
                    x, middle - Math.Clamp(peak.Min, -1, 1) * middle);
            }
        }
        using var loopPen = new Pen(Color.Green, 2);
        if (loopEnd >= 0)
        {
            DrawPosition(graphics, (long)loopStart * bytesPerFrame, loopPen);
            DrawPosition(graphics, (long)loopEnd * bytesPerFrame, loopPen);
        }
        if (samplesPerPixel < 2048)
        {
            using var markerPen = new Pen(Color.Red, 2);
            foreach (var marker in markers)
            {
                double x = GetX((long)marker.StartPosition * bytesPerFrame);
                if (x < 0 || x >= Width) continue;
                graphics.DrawLine(markerPen, (float)x, middle, (float)x, Height);
                if (samplesPerPixel <= 128)
                    graphics.DrawString(marker.Name, Font, Brushes.Red, (float)x + 2, middle + 2);
            }
        }
        if (waveformError != null)
            TextRenderer.DrawText(graphics, waveformError, Font, ClientRectangle, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
    }

    private void DrawPosition(Graphics graphics, long position, Pen pen)
    {
        double x = GetX(position);
        if (x >= 0 && x < Width) graphics.DrawLine(pen, (float)x, 0, (float)x, Height);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        long anchor = startPosition + (long)e.X * BytesPerPixel;
        int steps = Math.Max(1, Math.Abs(e.Delta / 120));
        for (int i = 0; i < Math.Min(steps, 8); i++)
            if (e.Delta > 0) ZoomIn(); else if (e.Delta < 0) ZoomOut();
        StartPosition = anchor - (long)e.X * BytesPerPixel;
        base.OnMouseWheel(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && waveStream != null)
        {
            Focus();
            mouseDown = true;
            mouseDownPosition = e.Location;
            isDragging = false;
            lastMouseX = e.X;
            Capture = true;
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (mouseDown && !isDragging)
        {
            var size = SystemInformation.DragSize;
            isDragging = !new Rectangle(mouseDownPosition.X - size.Width / 2,
                mouseDownPosition.Y - size.Height / 2, size.Width, size.Height).Contains(e.Location);
            if (isDragging) Cursor = Cursors.Hand;
        }
        if (isDragging)
        {
            StartPosition -= (long)(e.X - lastMouseX) * BytesPerPixel;
            lastMouseX = e.X;
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            bool seek = mouseDown && !isDragging && ClientRectangle.Contains(e.Location) && waveStream != null;
            Capture = false;
            if (seek && waveStream != null && bytesPerFrame > 0)
                SeekRequested?.Invoke(this, Math.Clamp(startPosition / bytesPerFrame + (long)e.X * samplesPerPixel,
                    0, waveStream.Length / bytesPerFrame));
        }
        base.OnMouseUp(e);
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        if (!Capture)
        {
            isDragging = false;
            mouseDown = false;
            Cursor = Cursors.Default;
        }
        base.OnMouseCaptureChanged(e);
    }

    public void ZoomIn() => SamplesPerPixel /= 2;
    public void ZoomOut() => SamplesPerPixel *= 2;
    public void ResetZoom() => SamplesPerPixel = 128;

    public void EnsurePositionVisible(long position)
    {
        if (GetX(position) < 0 || GetX(position) >= Width)
            StartPosition = position - (Width / 2L) * BytesPerPixel;
    }

    public void AddMarker(MarkerEntry marker)
    {
        markers.Add(marker);
        markerEnd = Math.Max(markerEnd, (long)marker.StartPosition + marker.Length + 2);
        ClampStart();
        InvalidateWaveform();
    }

    public void ClearMarkers()
        => SetMarkers([]);

    public void SetMarkers(IEnumerable<MarkerEntry> values)
    {
        markers.Clear();
        markers.AddRange(values);
        markerEnd = 0;
        foreach (var marker in markers)
            markerEnd = Math.Max(markerEnd, (long)marker.StartPosition + marker.Length + 2);
        ClampStart();
        InvalidateWaveform();
    }

    public void SetLoop(int start, int end)
    {
        loopStart = start;
        loopEnd = end;
        InvalidateWaveform();
    }

    public void RemoveLoop() => SetLoop(-1, -1);

    private async Task BuildPeaksAsync(WaveStream stream, CancellationToken cancellation)
    {
        try
        {
            var result = await Task.Run(() => WaveformPeaks.Build(stream, cancellation));
            if (result == null || cancellation.IsCancellationRequested || IsDisposed || stream != waveStream) return;
            peaks = result;
            InvalidateWaveform();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (cancellation.IsCancellationRequested || IsDisposed || stream != waveStream) return;
            waveformError = ex.Message;
            InvalidateWaveform();
        }
    }

    private void CancelPeakGeneration()
    {
        peakCancellation?.Cancel();
        peakCancellation?.Dispose();
        peakCancellation = null;
    }

    private void DisposeWaveform()
    {
        CancelPeakGeneration();
        peaks = null;
        waveStream = null;
        PlaybackStream = null;
        waveformImage?.Dispose();
        waveformImage = null;
    }

    private void ClampStart()
    {
        long length = waveStream?.Length ?? 0;
        if (length == 0) length = markerEnd * bytesPerFrame;
        startPosition = Math.Clamp(startPosition, 0, Math.Max(0, length - (long)Width * BytesPerPixel));
        if (bytesPerFrame > 0) startPosition -= startPosition % bytesPerFrame;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ClampStart();
        InvalidateWaveform();
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        InvalidateWaveform();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        InvalidateWaveform();
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        InvalidateWaveform();
    }
}

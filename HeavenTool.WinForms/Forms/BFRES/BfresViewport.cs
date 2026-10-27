using HeavenTool.Rendering.BFRES;
using OpenTK.GLControl;
using OpenTK.Windowing.Common;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace HeavenTool.Forms.BFRES;

// WinForms owns the context and input; rendering is shared with Avalonia.
internal sealed class BfresViewport : GLControl
{
    public BfresRenderer Renderer { get; } = new();
    public event Action<Exception>? RenderFailed;
    private Point previous, mouseDown;
    private bool failed;

    public BfresViewport() : base(new GLControlSettings
    {
        APIVersion = new Version(4, 4), Profile = ContextProfile.Core,
        Flags = ContextFlags.ForwardCompatible, NumberOfSamples = 0, SrgbCapable = true
    })
    {
        Dock = DockStyle.Fill;
        TabStop = true;
        BackColor = Color.FromArgb(28, 30, 35);
        Renderer.Invalidated += Invalidate;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (DesignMode || failed || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        try
        {
            MakeCurrent();
            Renderer.Render(ClientSize.Width, ClientSize.Height, 0, DeviceDpi / 96f);
            SwapBuffers();
        }
        catch (Exception ex)
        {
            failed = true;
            RenderFailed?.Invoke(ex);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus(); previous = mouseDown = e.Location; Capture = true;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        Capture = false;
        if (e.Button == MouseButtons.Left && Math.Abs(e.X - mouseDown.X) + Math.Abs(e.Y - mouseDown.Y) <= 4 * DeviceDpi / 96f)
            Renderer.RequestBonePick(e.X, e.Y);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        float scale = DeviceDpi / 96f;
        float dx = (e.X - previous.X) / scale, dy = (e.Y - previous.Y) / scale;
        if (e.Button == MouseButtons.Left) Renderer.Orbit(dx, dy);
        else if (e.Button is MouseButtons.Right or MouseButtons.Middle) Renderer.Pan(dx, dy);
        previous = e.Location;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Renderer.Zoom(e.Delta / 120f);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.F && !e.Handled) { Renderer.FrameSelection(); e.Handled = true; }
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (!DesignMode && Context != null)
        {
            MakeCurrent();
            Renderer.ReleaseGraphics();
        }
        failed = false;
        base.OnHandleDestroyed(e);
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HeavenTool.Rendering.BFRES;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using System;

namespace HeavenTool.Avalonia.Controls;

public sealed class BfresViewport : OpenGlControlBase, ICustomHitTest
{
    public BfresRenderer Renderer { get; } = new();
    public event Action<Exception>? RenderFailed;
    private Point previous, mouseDown;
    private bool failed, contextLost, bindingsLoaded, presentationPending;
    private int attachmentVersion, renderedVersion;

    public BfresViewport()
    {
        Focusable = true;
        ClipToBounds = true;
        Renderer.ShowGround = true;
        Renderer.Invalidated += InvalidateViewport;
    }

    private void InvalidateViewport()
    {
        RequestNextFrameRendering();
        ((Visual)this).InvalidateVisual();
    }

    // The GL image is a child composition visual and draws nothing of its own, so without this
    // hit testing falls through to the panel behind and the viewport never sees the mouse.
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    private sealed class Bindings(GlInterface gl) : IBindingsContext
    {
        public IntPtr GetProcAddress(string name) => gl.GetProcAddress(name);
    }

    protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // OpenGlControlBase logs backend initialization failures but has no failure event.
        // Surface a software-only backend in the viewer rather than leaving a blank viewport.
        var compositor = ElementComposition.GetElementVisual(this)?.Compositor;
        if (compositor == null) return;
        try
        {
            var interop = await compositor.TryGetCompositionGpuInterop();
            if (interop == null && this.IsAttachedToVisualTree())
                Fail(new NotSupportedException("The current UI backend cannot host the 3D view. A desktop OpenGL 4.4 graphics driver is required."));
        }
        catch (Exception ex) { if (this.IsAttachedToVisualTree()) Fail(ex); }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        attachmentVersion++;
        presentationPending = false;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        if (contextLost) return;
        failed = false;
        try
        {
            if (GlVersion.Type != GlProfileType.OpenGL || GlVersion.Major < 4 || GlVersion.Major == 4 && GlVersion.Minor < 4)
                throw new NotSupportedException($"The BFRES viewer requires desktop OpenGL 4.4. This context provides {GlVersion}.");
            GL.LoadBindings(new Bindings(gl));
            bindingsLoaded = true;
        }
        catch (Exception ex) { Fail(ex); }
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (failed) return;
        try
        {
            double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            Renderer.Render(Math.Max(1, (int)(Bounds.Width * scale)), Math.Max(1, (int)(Bounds.Height * scale)), fb, (float)scale);
            renderedVersion++;
            if (!presentationPending)
            {
                presentationPending = true;
                int attachment = attachmentVersion;
                // OpenGlControlBase publishes the shared surface after this callback returns.
                Dispatcher.UIThread.Post(() => PresentSurfaceAsync(attachment), DispatcherPriority.Background);
            }
        }
        catch (Exception ex) { Fail(ex); }
    }

    private async void PresentSurfaceAsync(int attachment)
    {
        try
        {
            var visual = ElementComposition.GetElementVisual(this);
            if (visual == null) return;
            int frame;
            do
            {
                frame = renderedVersion;
                // Repaint only after the compositor has applied the surface update. Waiting
                // asynchronously avoids glFinish's UI-thread stall and the stale final frame.
                await visual.Compositor.RequestCommitAsync();
                if (attachment != attachmentVersion || !this.IsAttachedToVisualTree() || failed) return;
                ((Visual)this).InvalidateVisual();
            } while (frame != renderedVersion);
        }
        catch (Exception ex) { if (attachment == attachmentVersion && this.IsAttachedToVisualTree()) Fail(ex); }
        finally { if (attachment == attachmentVersion) presentationPending = false; }
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        if (!contextLost && bindingsLoaded) Renderer.ReleaseGraphics();
        bindingsLoaded = false;
    }

    protected override void OnOpenGlLost()
    {
        contextLost = true;
        Fail(new InvalidOperationException("The graphics context was lost. Close and reopen the BFRES viewer to restore it."));
    }

    private void Fail(Exception error)
    {
        failed = true;
        RenderFailed?.Invoke(error);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        previous = mouseDown = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var point = e.GetPosition(this);
        if (e.InitialPressMouseButton == MouseButton.Left && Math.Abs(point.X - mouseDown.X) + Math.Abs(point.Y - mouseDown.Y) <= 4)
        {
            double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            Renderer.RequestBonePick((float)(point.X * scale), (float)(point.Y * scale));
        }
        e.Pointer.Capture(null);
        InvalidateViewport();
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        InvalidateViewport();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetCurrentPoint(this);
        float dx = (float)(point.Position.X - previous.X), dy = (float)(point.Position.Y - previous.Y);
        if (point.Properties.IsLeftButtonPressed) Renderer.Orbit(dx, dy);
        else if (point.Properties.IsRightButtonPressed || point.Properties.IsMiddleButtonPressed) Renderer.Pan(dx, dy);
        previous = point.Position;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Renderer.Zoom((float)e.Delta.Y);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || HandleNavigationKey(e)) return;
        if (e.KeyModifiers != KeyModifiers.None) return;
        if (e.Key == Key.F) { Renderer.FrameSelection(); e.Handled = true; }
        // B is handled by the window so the menu stays in sync.
    }

    // Also used by the scene tree so the selected mesh or bone can be framed from there.
    public bool HandleNavigationKey(KeyEventArgs e)
    {
        if (e.Handled || (e.KeyModifiers & (KeyModifiers.Alt | KeyModifiers.Meta)) != 0) return false;
        bool control = (e.KeyModifiers & KeyModifiers.Control) != 0;
        bool shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        // Physical numpad keys keep working when Num Lock is off or the decimal key uses a comma.
        var key = e.PhysicalKey switch
        {
            PhysicalKey.NumPad1 => Key.NumPad1, PhysicalKey.NumPad2 => Key.NumPad2,
            PhysicalKey.NumPad3 => Key.NumPad3, PhysicalKey.NumPad4 => Key.NumPad4,
            PhysicalKey.NumPad5 => Key.NumPad5, PhysicalKey.NumPad6 => Key.NumPad6,
            PhysicalKey.NumPad7 => Key.NumPad7, PhysicalKey.NumPad8 => Key.NumPad8,
            PhysicalKey.NumPad9 => Key.NumPad9, PhysicalKey.NumPadAdd => Key.Add,
            PhysicalKey.NumPadSubtract => Key.Subtract,
            PhysicalKey.NumPadDecimal or PhysicalKey.NumPadComma => Key.Decimal,
            _ => e.Key
        };
        const float step = MathF.PI / 12;
        if (control && !shift)
        {
            switch (key)
            {
                case Key.NumPad1: Renderer.SetView(MathF.PI, 0); break;
                case Key.NumPad3: Renderer.SetView(-MathF.PI / 2, 0); break;
                case Key.NumPad7: Renderer.SetView(0, -MathF.PI / 2); break;
                case Key.NumPad2: Renderer.Pan(0, -100); break;
                case Key.NumPad4: Renderer.Pan(100, 0); break;
                case Key.NumPad6: Renderer.Pan(-100, 0); break;
                case Key.NumPad8: Renderer.Pan(0, 100); break;
                default: return false;
            }
        }
        else if (shift && !control)
        {
            switch (key)
            {
                case Key.NumPad4: Renderer.RollStep(-step); break;
                case Key.NumPad6: Renderer.RollStep(step); break;
                default: return false;
            }
        }
        else if (!shift && !control)
        {
            switch (key)
            {
                case Key.NumPad1: Renderer.SetView(0, 0); break;
                case Key.NumPad3: Renderer.SetView(MathF.PI / 2, 0); break;
                case Key.NumPad7: Renderer.SetView(0, MathF.PI / 2); break;
                case Key.NumPad2: Renderer.OrbitStep(0, -step); break;
                case Key.NumPad4: Renderer.OrbitStep(-step, 0); break;
                case Key.NumPad6: Renderer.OrbitStep(step, 0); break;
                case Key.NumPad8: Renderer.OrbitStep(0, step); break;
                case Key.NumPad5: Renderer.ToggleProjection(); break;
                case Key.NumPad9: Renderer.OppositeView(); break;
                case Key.Add: Renderer.Zoom(1); break;
                case Key.Subtract: Renderer.Zoom(-1); break;
                case Key.Decimal:
                case Key.Separator: Renderer.FrameSelection(); break;
                case Key.Home: Renderer.FrameScene(); break;
                default: return false;
            }
        }
        else return false;
        e.Handled = true;
        return true;
    }
}

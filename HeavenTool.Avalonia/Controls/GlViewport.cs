using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Rendering;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HeavenTool.Rendering;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using System;

namespace HeavenTool.Avalonia.Controls;

/// <summary>Hosts an <see cref="IViewportRenderer"/> with orbit, pan, zoom and numpad navigation.</summary>
public abstract class GlViewport : OpenGlControlBase, ICustomHitTest
{
    private readonly IViewportRenderer renderer;
    private readonly string viewerName;
    public event Action<Exception>? RenderFailed;
    private Point previous, mouseDown;
    private bool failed, contextLost, bindingsLoaded, presentationPending, toolActive;
    private int attachmentVersion, renderedVersion;

    protected GlViewport(IViewportRenderer renderer, string viewerName)
    {
        this.renderer = renderer;
        this.viewerName = viewerName;
        Focusable = true;
        ClipToBounds = true;
        renderer.Invalidated += InvalidateViewport;
    }

    protected void InvalidateViewport()
    {
        RequestNextFrameRendering();
        ((Visual)this).InvalidateVisual();
    }

    protected double RenderScaling => global::Avalonia.Controls.TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;

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
                throw new NotSupportedException($"The {viewerName} requires desktop OpenGL 4.4. This context provides {GlVersion}.");
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
            double scale = RenderScaling;
            renderer.Render(Math.Max(1, (int)(Bounds.Width * scale)), Math.Max(1, (int)(Bounds.Height * scale)), fb, (float)scale);
            // The compositor takes the image once the GPU is done with it, which can be later than the repaint below, and
            // the window then keeps the old image until something else repaints it.
            GL.Finish();
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
        if (!contextLost && bindingsLoaded) renderer.ReleaseGraphics();
        bindingsLoaded = false;
    }

    protected override void OnOpenGlLost()
    {
        contextLost = true;
        Fail(new InvalidOperationException($"The graphics context was lost. Close and reopen the {viewerName} to restore it."));
    }

    private void Fail(Exception error)
    {
        failed = true;
        RenderFailed?.Invoke(error);
    }

    /// <summary>A left click that didn't drag, in physical pixels.</summary>
    protected virtual void OnViewportClick(float x, float y, KeyModifiers modifiers) { }

    /// <summary>
    /// A left press, in physical pixels. Returning true hands the left drag to the tool instead of orbiting;
    /// it then gets <see cref="ContinueTool"/> while it drags and <see cref="EndTool"/> once released.
    /// </summary>
    protected virtual bool BeginTool(float x, float y, KeyModifiers modifiers) => false;
    protected virtual void ContinueTool(float x, float y) { }
    protected virtual void EndTool() { }
    /// <summary>The cursor moved with no button held, in physical pixels; null once it leaves the viewport.</summary>
    protected virtual void OnViewportHover(float? x, float? y) { }
    /// <summary>While true, a right drag orbits instead of panning, so a tool can own the left button.</summary>
    protected virtual bool RightDragOrbits => false;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var point = e.GetCurrentPoint(this);
        previous = mouseDown = point.Position;
        e.Pointer.Capture(this);
        e.Handled = true;
        double scale = RenderScaling;
        if (point.Properties.IsLeftButtonPressed && !toolActive)
            toolActive = BeginTool((float)(point.Position.X * scale), (float)(point.Position.Y * scale), e.KeyModifiers);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var point = e.GetPosition(this);
        if (e.InitialPressMouseButton == MouseButton.Left && toolActive)
        {
            toolActive = false;
            EndTool();
        }
        else if (e.InitialPressMouseButton == MouseButton.Left && Math.Abs(point.X - mouseDown.X) + Math.Abs(point.Y - mouseDown.Y) <= 4)
        {
            double scale = RenderScaling;
            OnViewportClick((float)(point.X * scale), (float)(point.Y * scale), e.KeyModifiers);
        }
        e.Pointer.Capture(null);
        InvalidateViewport();
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (toolActive)
        {
            toolActive = false;
            EndTool();
        }
        InvalidateViewport();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetCurrentPoint(this);
        float dx = (float)(point.Position.X - previous.X), dy = (float)(point.Position.Y - previous.Y);
        double scale = RenderScaling;
        float x = (float)(point.Position.X * scale), y = (float)(point.Position.Y * scale);
        var buttons = point.Properties;
        if (toolActive) ContinueTool(x, y);
        else if (buttons.IsLeftButtonPressed || buttons.IsRightButtonPressed && RightDragOrbits) renderer.Orbit(dx, dy);
        else if (buttons.IsRightButtonPressed || buttons.IsMiddleButtonPressed) renderer.Pan(dx, dy);
        else OnViewportHover(x, y);
        previous = point.Position;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!toolActive) OnViewportHover(null, null);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        renderer.Zoom((float)e.Delta.Y);
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || HandleNavigationKey(e)) return;
        if (e.KeyModifiers != KeyModifiers.None) return;
        if (e.Key == Key.F) { renderer.FrameSelection(); e.Handled = true; }
    }

    // Also used by side panels so the selection can be framed from there.
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
                case Key.NumPad1: renderer.SetView(MathF.PI, 0); break;
                case Key.NumPad3: renderer.SetView(-MathF.PI / 2, 0); break;
                case Key.NumPad7: renderer.SetView(0, -MathF.PI / 2); break;
                case Key.NumPad2: renderer.Pan(0, -100); break;
                case Key.NumPad4: renderer.Pan(100, 0); break;
                case Key.NumPad6: renderer.Pan(-100, 0); break;
                case Key.NumPad8: renderer.Pan(0, 100); break;
                default: return false;
            }
        }
        else if (shift && !control)
        {
            switch (key)
            {
                case Key.NumPad4: renderer.RollStep(-step); break;
                case Key.NumPad6: renderer.RollStep(step); break;
                default: return false;
            }
        }
        else if (!shift && !control)
        {
            switch (key)
            {
                case Key.NumPad1: renderer.SetView(0, 0); break;
                case Key.NumPad3: renderer.SetView(MathF.PI / 2, 0); break;
                case Key.NumPad7: renderer.SetView(0, MathF.PI / 2); break;
                case Key.NumPad2: renderer.OrbitStep(0, -step); break;
                case Key.NumPad4: renderer.OrbitStep(-step, 0); break;
                case Key.NumPad6: renderer.OrbitStep(step, 0); break;
                case Key.NumPad8: renderer.OrbitStep(0, step); break;
                case Key.NumPad5: renderer.ToggleProjection(); break;
                case Key.NumPad9: renderer.OppositeView(); break;
                case Key.Add: renderer.Zoom(1); break;
                case Key.Subtract: renderer.Zoom(-1); break;
                case Key.Decimal:
                case Key.Separator: renderer.FrameSelection(); break;
                case Key.Home: renderer.FrameScene(); break;
                default: return false;
            }
        }
        else return false;
        e.Handled = true;
        return true;
    }
}

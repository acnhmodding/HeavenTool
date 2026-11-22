namespace HeavenTool.Rendering;

/// <summary>A renderer with an orbit camera that an OpenGL host control can drive.</summary>
public interface IViewportRenderer
{
    event Action? Invalidated;

    /// <summary>Renders into a host framebuffer, in physical pixels. Never swaps the host's buffers.</summary>
    void Render(int pixelWidth, int pixelHeight, int framebuffer, float scale = 1);

    /// <summary>Releases GPU objects in the current context; keeps the scene for reattachment.</summary>
    void ReleaseGraphics();

    void Orbit(float dx, float dy);
    void Pan(float dx, float dy);
    void Zoom(float steps);
    void SetView(float yawRadians, float pitchRadians);
    void OrbitStep(float horizontalRadians, float verticalRadians);
    void RollStep(float radians);
    void OppositeView();
    void ToggleProjection();
    void FrameSelection();
    void FrameScene();
}

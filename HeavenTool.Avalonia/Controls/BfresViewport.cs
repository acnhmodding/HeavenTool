using Avalonia.Input;
using HeavenTool.Rendering.BFRES;

namespace HeavenTool.Avalonia.Controls;

public sealed class BfresViewport : GlViewport
{
    public BfresRenderer Renderer { get; }

    public BfresViewport() : this(new BfresRenderer()) { }

    private BfresViewport(BfresRenderer renderer) : base(renderer, "BFRES viewer")
    {
        Renderer = renderer;
        Renderer.ShowGround = true;
    }

    protected override void OnViewportClick(float x, float y, KeyModifiers modifiers) => Renderer.RequestBonePick(x, y);

    // B is handled by the window so the menu stays in sync.
}

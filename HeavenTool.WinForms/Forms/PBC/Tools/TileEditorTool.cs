using System.Drawing;
using System.Windows.Forms;

namespace HeavenTool.Forms.PBC.Tools;

public abstract class TileEditorTool(TileEditor core)
{
    public TileEditor Core => core;

    public abstract void OnMouseDown(MouseEventArgs mouseEventArgs);
    public abstract void OnMouseMove(MouseEventArgs mouseEventArgs);
    public abstract void OnMouseUp(MouseEventArgs mouseEventArgs);

    public bool TryGetTilePosition(Point mouse, out TilePosition pos) => core.TryGetTilePosition(mouse, out pos);
}
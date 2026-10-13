using System.Windows.Forms;

namespace HeavenTool.Forms.PBC.Tools;

public class InspectorTool(TileEditor core) : TileEditorTool(core)
{
    public override void OnMouseDown(MouseEventArgs e)
    {
        if (!Control.ModifierKeys.HasFlag(Keys.Control) && Core.PBCFile != null && TryGetTilePosition(e.Location, out var pos))
        {
            var tile = Core.PBCFile[pos.TileY, pos.TileX];
            Core.SelectTriangle(tile.HeightMap.Triangles[(int)pos.Triangle]);
        }
    }

    public override void OnMouseMove(MouseEventArgs e) { }

    public override void OnMouseUp(MouseEventArgs e)
    {

    }
}
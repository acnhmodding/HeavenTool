using System.Windows.Forms;

namespace HeavenTool.Forms.PBC.Tools;

public class InspectorTool(TileEditor core) : TileEditorTool(core)
{
    public override void OnMouseDown(MouseEventArgs e)
    {
        if (!Control.ModifierKeys.HasFlag(Keys.Control) && Core.PBCFile != null && TryGetTilePosition(e.Location, out var pos))
        {
            var tile = Core.PBCFile[pos.TileY, pos.TileX];
            Core.SelectQuadrant(tile.HeightMap.Quadrants[pos.SubY, pos.SubX]);
        }
    }

    public override void OnMouseMove(MouseEventArgs e)
    {
        if (Core.PBCFile != null && TryGetTilePosition(e.Location, out var pos))
        {
            var tile = Core.PBCFile[pos.TileY, pos.TileX];

            if (Core.CurrentView == ViewType.HeightMap)
                Core.HighlightedHeight = Core.GetHeightInfo(tile.HeightMap, pos.SubY, pos.SubX);
            else Core.HighlightedHeight = null;
        }
    }

    public override void OnMouseUp(MouseEventArgs e)
    {

    }
}

using HeavenTool.IO.FileFormats.PBC;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace HeavenTool.Forms.PBC.Tools;

public class CollisionBrushTool(TileEditor core) : TileEditorTool(core)
{
    private List<TileChange>? _currentStroke;

    public override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left
            || Control.ModifierKeys.HasFlag(Keys.Control)
            || Core.PBCFile == null
            || Core.TileBrush is not TileType brush)
            return;

        _currentStroke = [];

        if (TryGetTilePosition(e.Location, out var pos))
        {
            Apply(Core.PBCFile, pos, brush);
            Core.Invalidate();
        }
    }

    public override void OnMouseMove(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || Control.ModifierKeys.HasFlag(Keys.Control))
            return;

        if (Core.PBCFile == null || _currentStroke == null || Core.TileBrush is not TileType brush)
            return;

        if (TryGetTilePosition(e.Location, out var pos))
        {
            Apply(Core.PBCFile, pos, brush);
            Core.Invalidate();
        }
    }

    public override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        var stroke = _currentStroke;
        _currentStroke = null;
        if (Core.PBCFile == null || stroke == null || stroke.Count == 0)
            return;

        var command = new TileEditCommand(Core.PBCFile, stroke);
        Core.UndoManager.Execute(command, skipFirstRedo: true);
    }

    private void Apply(PBCFileReader file, TilePosition pos, TileType type)
    {
        if (_currentStroke == null) return;

        var tile = file[pos.TileY, pos.TileX];
        var oldValue = tile.Materials[(int)pos.Triangle];

        if (oldValue == type)
            return;

        if (_currentStroke!.Any(c =>
            c.TileX == pos.TileX &&
            c.TileY == pos.TileY &&
            c.Triangle == pos.Triangle))
            return;

        _currentStroke.Add(new TileChange
        {
            TileX = pos.TileX,
            TileY = pos.TileY,
            Triangle = pos.Triangle,
            OldValue = oldValue,
            NewValue = type
        });

        tile.Materials[(int)pos.Triangle] = type;
    }
}
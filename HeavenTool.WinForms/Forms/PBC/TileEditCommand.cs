using HeavenTool.IO.FileFormats.PBC;
using HeavenTool.IO.UndoSystem;
using System.Collections.Generic;

namespace HeavenTool.Forms.PBC;

public class TileEditCommand(PBCFileReader file, List<TileChange> changes) : IUndoCommand
{
    private readonly PBCFileReader _file = file;
    public readonly List<TileChange> Changes = changes;

    public void Undo()
    {
        foreach (var c in Changes)
            _file[c.TileY, c.TileX].Materials[(int)c.Triangle] = c.OldValue;
    }

    public void Redo()
    {
        foreach (var c in Changes)
            _file[c.TileY, c.TileX].Materials[(int)c.Triangle] = c.NewValue;
    }
}

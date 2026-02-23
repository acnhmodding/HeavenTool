using HeavenTool.IO.FileFormats.PBC;
using HeavenTool.Utility.UndoSystem;
using System.Collections.Generic;

namespace HeavenTool.Forms.PBC;

public class TileEditCommand(PBCFileReader file, List<TileChange> changes) : IUndoCommand
{
    private readonly PBCFileReader _file = file;
    public readonly List<TileChange> Changes = changes;

    public void Undo()
    {
        foreach (var c in Changes)
            _file[c.TileY, c.TileX].Type[c.SubY, c.SubX] = c.OldValue;
    }

    public void Redo()
    {
        foreach (var c in Changes)
            _file[c.TileY, c.TileX].Type[c.SubY, c.SubX] = c.NewValue;
    }
}

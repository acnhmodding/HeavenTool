using System.Collections.Generic;

namespace HeavenTool.IO.UndoSystem;

public class MergedUndoCommand : IUndoCommand
{
    public List<IUndoCommand> Commands = [];
    public void Redo()
    {
        foreach (var redoCommand in Commands)
            redoCommand.Redo();
    }

    public void Undo()
    {
        foreach (var undoCommand in Commands)
            undoCommand.Undo();
    }
}

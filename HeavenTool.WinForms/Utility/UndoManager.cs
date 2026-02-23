using HeavenTool.Utility.UndoSystem;
using System.Collections.Generic;

namespace HeavenTool.Utility;

public class UndoManager
{
    private readonly Stack<IUndoCommand> _undo = new();
    private readonly Stack<IUndoCommand> _redo = new();

    public void Execute(IUndoCommand command, bool skipFirstRedo = false)
    {
        if (!skipFirstRedo)
            command.Redo();

        _undo.Push(command);
        _redo.Clear();
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;

        var cmd = _undo.Pop();
        cmd.Undo();
        _redo.Push(cmd);
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;

        var cmd = _redo.Pop();
        cmd.Redo();
        _undo.Push(cmd);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}

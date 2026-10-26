using System;
using System.Collections.Generic;

namespace HeavenTool.IO.UndoSystem;

public class UndoManager
{
    private readonly Stack<IUndoCommand> _undo = new();
    private readonly Stack<IUndoCommand> _redo = new();
    // The command on top of the undo stack when the document was last saved; null means the initial state,
    // and any other object a state no command leads back to.
    private object? _savePoint;

    public event EventHandler? Changed;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public bool IsAtSavePoint => ReferenceEquals(_undo.Count > 0 ? _undo.Peek() : null, _savePoint);

    public void Execute(IUndoCommand command, bool skipFirstRedo = false)
    {
        if (!skipFirstRedo)
            command.Redo();

        _undo.Push(command);
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IUndoCommand? Undo()
    {
        if (!_undo.TryPop(out var cmd)) return null;

        cmd.Undo();
        _redo.Push(cmd);
        Changed?.Invoke(this, EventArgs.Empty);
        return cmd;
    }

    public IUndoCommand? Redo()
    {
        if (!_redo.TryPop(out var cmd)) return null;

        cmd.Redo();
        _undo.Push(cmd);
        Changed?.Invoke(this, EventArgs.Empty);
        return cmd;
    }

    public void MarkSaved()
    {
        _savePoint = _undo.Count > 0 ? _undo.Peek() : null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Drops the history; with <paramref name="atSavePoint"/> false the document stays unsaved.</summary>
    public void Clear(bool atSavePoint = true)
    {
        _undo.Clear();
        _redo.Clear();
        _savePoint = atSavePoint ? null : new object();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public int GetUndoCount() => _undo.Count;
    public int GetRedoCount() => _redo.Count;
}

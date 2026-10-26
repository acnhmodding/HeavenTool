namespace HeavenTool.IO.UndoSystem;

public interface IUndoCommand
{
    void Undo();
    void Redo();
}

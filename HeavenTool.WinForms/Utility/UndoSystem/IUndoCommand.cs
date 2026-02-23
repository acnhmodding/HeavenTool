namespace HeavenTool.Utility.UndoSystem;

public interface IUndoCommand
{
    void Undo();
    void Redo();
}

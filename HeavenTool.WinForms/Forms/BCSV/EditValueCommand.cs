using HeavenTool.IO.FileFormats.BCSV;
using HeavenTool.Utility.UndoSystem;

namespace HeavenTool.Forms.BCSV;

internal class EditValueCommand(BinaryCSV bcsv) : IUndoCommand
{
    public int rowIndex;
    public int columnIndex;
    public required object oldValue;
    public required object newValue;

    public void Redo()
    {
        bcsv[rowIndex, columnIndex] = newValue;
    }

    public void Undo()
    {
        bcsv[rowIndex, columnIndex] = oldValue;
    }
}

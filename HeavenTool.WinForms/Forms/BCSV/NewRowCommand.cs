using HeavenTool.IO.FileFormats.BCSV;
using HeavenTool.Utility.UndoSystem;

namespace HeavenTool.Forms.BCSV;

public class NewRowCommand(BinaryCSV bcsv, object[] newRowValues) : IUndoCommand
{
    private int? index;

    public void Redo()
    {
        if (!index.HasValue)
        {
            index = bcsv.Entries.Count;
            bcsv.Entries.Add(newRowValues);
        } else
        {
            bcsv.Entries.Insert(index.Value, newRowValues);
        }
    }

    public void Undo()
    {
        if (index.HasValue)
            bcsv.Entries.RemoveAt(index.Value);
    }
}

using HeavenTool.IO.FileFormats.BCSV;
using HeavenTool.Utility.UndoSystem;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HeavenTool.Forms.BCSV;

internal class DeleteRowsCommand(BinaryCSV bcsv) : IUndoCommand
{
    public Dictionary<int, object[]> deletedRows = [];

    public void Redo()
    {
        foreach (var item in deletedRows)
        {
            bcsv.Entries.RemoveAt(item.Key);
        }
        
    }

    public void Undo()
    {
        foreach (var (index, val) in deletedRows.Reverse())
        {
            bcsv.Entries.Insert(index, val);
        }
    }
}

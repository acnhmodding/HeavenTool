using HeavenTool.IO.UndoSystem;
using System.Collections.Generic;
using System.Linq;

namespace HeavenTool.Avalonia.FileTypes.BCSV;

public interface IBCSVCommand : IUndoCommand
{
    /// <summary>Rows to select after the command is undone or redone.</summary>
    IEnumerable<BCSVRow> RowsToSelect(bool undone);
}

public readonly record struct CellChange(BCSVRow Row, int FieldIndex, object OldValue, object NewValue);

public sealed class SetCellsCommand(IReadOnlyList<CellChange> changes) : IBCSVCommand
{
    public void Redo()
    {
        foreach (var change in changes) change.Row[change.FieldIndex] = change.NewValue;
    }

    public void Undo()
    {
        for (int i = changes.Count - 1; i >= 0; i--) changes[i].Row[changes[i].FieldIndex] = changes[i].OldValue;
    }

    public IEnumerable<BCSVRow> RowsToSelect(bool undone) => changes.Select(change => change.Row).Distinct();
}

/// <summary>Inserts rows at their source indexes on redo (or removes them, when <paramref name="remove"/> is set).</summary>
public sealed class ChangeRowsCommand(BCSVRowList rows, IReadOnlyList<(int Index, BCSVRow Row)> items, bool remove) : IBCSVCommand
{
    // Ascending indexes are valid when inserting in order; removing walks them backwards.
    private readonly (int Index, BCSVRow Row)[] items = [.. items.OrderBy(item => item.Index)];

    public void Redo()
    {
        if (remove) Remove();
        else Insert();
    }

    public void Undo()
    {
        if (remove) Insert();
        else Remove();
    }

    // The grid handles each insertion and removal on its own; past half the table one reset is cheaper.
    private void Insert()
    {
        if (items.Length <= rows.Count)
        {
            foreach (var (index, row) in items) rows.Insert(index, row);
            return;
        }
        var merged = new List<BCSVRow>(rows.Count + items.Length);
        int next = 0;
        foreach (var (index, row) in items)
        {
            while (merged.Count < index) merged.Add(rows[next++]);
            merged.Add(row);
        }
        while (next < rows.Count) merged.Add(rows[next++]);
        rows.ReplaceAll(merged);
    }

    private void Remove()
    {
        if (items.Length > rows.Count / 2)
        {
            var removed = items.Select(item => item.Row).ToHashSet();
            rows.ReplaceAll([.. rows.Where(row => !removed.Contains(row))]);
            return;
        }
        for (int i = items.Length - 1; i >= 0; i--) rows.RemoveAt(items[i].Index);
    }

    public IEnumerable<BCSVRow> RowsToSelect(bool undone) => remove == undone ? items.Select(item => item.Row) : [];
}

using HeavenTool.IO.FileFormats.PBC;
using HeavenTool.IO.UndoSystem;
using System.Collections.Generic;

namespace HeavenTool.Avalonia.FileTypes.PBC;

/// <summary>What a left click or drag does in the 3D view.</summary>
public enum PbcTool { Select, Paint, Fill, Raise, Lower, Flatten }

/// <summary>One triangle of one tile.</summary>
public readonly record struct TilePosition(int TileX, int TileZ, TriangleDirection Triangle);

public readonly record struct TileChange(TilePosition Position, TileType OldValue, TileType NewValue);

/// <summary>A brush stroke or a single material edit.</summary>
public sealed class TileEditCommand(PBCFileReader file, IReadOnlyList<TileChange> changes) : IUndoCommand
{
    public void Undo()
    {
        foreach (var change in changes)
            file[change.Position.TileZ, change.Position.TileX].Materials[(int)change.Position.Triangle] = change.OldValue;
    }

    public void Redo()
    {
        foreach (var change in changes)
            file[change.Position.TileZ, change.Position.TileX].Materials[(int)change.Position.Triangle] = change.NewValue;
    }
}

/// <summary>One height of one triangle: corner 0 is the center, 1 and 2 the triangle's corners in file order.</summary>
public readonly record struct HeightPoint(TilePosition Position, int Corner);

public readonly record struct HeightChange(HeightPoint Point, float OldValue, float NewValue);

/// <summary>A raise, lower or flatten stroke.</summary>
public sealed class HeightEditCommand(PBCFileReader file, IReadOnlyList<HeightChange> changes) : IUndoCommand
{
    public void Undo()
    {
        foreach (var change in changes) Set(file, change.Point, change.OldValue);
    }

    public void Redo()
    {
        foreach (var change in changes) Set(file, change.Point, change.NewValue);
    }

    public static float Get(PBCFileReader file, HeightPoint point)
    {
        var heights = file[point.Position.TileZ, point.Position.TileX].HeightMap.Triangles[(int)point.Position.Triangle];
        return point.Corner switch { 0 => heights.CenterHeight, 1 => heights.Corner1Height, _ => heights.Corner2Height };
    }

    public static void Set(PBCFileReader file, HeightPoint point, float value)
    {
        var heights = file[point.Position.TileZ, point.Position.TileX].HeightMap.Triangles[(int)point.Position.Triangle];
        switch (point.Corner)
        {
            case 0: heights.CenterHeight = value; break;
            case 1: heights.Corner1Height = value; break;
            default: heights.Corner2Height = value; break;
        }
    }
}

/// <summary>Sets one height of one triangle: 0 center, 1 corner 1, 2 corner 2.</summary>
public sealed class TriangleHeightCommand(PBCFileReader file, TilePosition position, int corner, float oldValue, float newValue) : IUndoCommand
{
    public void Undo() => Set(oldValue);
    public void Redo() => Set(newValue);

    private void Set(float value)
    {
        var heights = file[position.TileZ, position.TileX].HeightMap.Triangles[(int)position.Triangle];
        switch (corner)
        {
            case 0: heights.CenterHeight = value; break;
            case 1: heights.Corner1Height = value; break;
            default: heights.Corner2Height = value; break;
        }
    }
}

/// <summary>Keeps both grids so undoing a shrink restores the cut tiles.</summary>
public sealed class ResizeCommand(PBCFileReader file, PBCFileReader.Tile[,] oldTiles, PBCFileReader.Tile[,] newTiles) : IUndoCommand
{
    public void Undo() => file.SetTiles(oldTiles);
    public void Redo() => file.SetTiles(newTiles);
}

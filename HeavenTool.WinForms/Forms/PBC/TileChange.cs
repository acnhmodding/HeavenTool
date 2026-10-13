using HeavenTool.IO.FileFormats.PBC;

namespace HeavenTool.Forms.PBC;

public class TileChange
{
    public int TileX;
    public int TileY;
    public TriangleDirection Triangle;
    public TileType OldValue;
    public TileType NewValue;
}


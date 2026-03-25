// Big thanks to https://github.com/McSpazzy/PBC
using System.ComponentModel;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.PBC;

/// <summary>
/// A class to read PBC (Physical Block Collision) files
/// </summary>
public partial class PBCFileReader
{
    public class Quadrant
    {
        public float Layer0 { get; set; }
        public float Layer1 { get; set; }
        public float Layer2 { get; set; }

        public Quadrant()
        {
            Layer0 = 0;
            Layer1 = 0;
            Layer2 = 0;
        }

        public Quadrant(BinaryReader reader)
        {
            Layer0 = reader.ReadSingle();
            Layer1 = reader.ReadSingle();
            Layer2 = reader.ReadSingle();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Layer0);
            writer.Write(Layer1);
            writer.Write(Layer2);
        }
    }

    public class HeightMap
    {
        public Quadrant[,] Quadrants = new Quadrant[2, 2];

        public HeightMap()
        {
            Quadrants[0, 1] = new Quadrant();
            Quadrants[0, 0] = new Quadrant();
            Quadrants[1, 0] = new Quadrant();
            Quadrants[1, 1] = new Quadrant();
        }

        public HeightMap(BinaryReader reader)
        {
            Quadrants[0, 1] = new Quadrant(reader);
            Quadrants[0, 0] = new Quadrant(reader);
            Quadrants[1, 0] = new Quadrant(reader);
            Quadrants[1, 1] = new Quadrant(reader);
        }

        public void Write(BinaryWriter writer)
        {
            Quadrants[0, 1].Write(writer);
            Quadrants[0, 0].Write(writer);
            Quadrants[1, 0].Write(writer);
            Quadrants[1, 1].Write(writer);
        }
    }


    public class Tile
    {
        public HeightMap HeightMap;
        public TileType[,] Type;

        public Tile()
        {
            HeightMap = new HeightMap();

            Type = new TileType[2, 2];
            Type[0, 1] = TileType.Null;
            Type[0, 0] = TileType.Null;
            Type[1, 0] = TileType.Null;
            Type[1, 1] = TileType.Null;
        }

        public Tile(BinaryReader reader)
        {
            // Read Height Map
            HeightMap = new HeightMap(reader);

            // Read Collision Map
            Type = new TileType[2, 2];

            Type[0, 1] = (TileType)reader.ReadByte();
            Type[0, 0] = (TileType)reader.ReadByte();
            Type[1, 0] = (TileType)reader.ReadByte();
            Type[1, 1] = (TileType)reader.ReadByte();
        }


        public void Write(BinaryWriter writer)
        {
            HeightMap.Write(writer);

            writer.Write((byte)Type[0, 1]);
            writer.Write((byte)Type[0, 0]);
            writer.Write((byte)Type[1, 0]);
            writer.Write((byte)Type[1, 1]);
        }
    }

    /// <summary>
    /// Check for file magic, return true if <paramref name="magic"/> is <b>pbc\0</b>
    /// </summary>
    /// <param name="magic">The first 4 bytes of the file.</param>
    /// <returns></returns>
    public static bool CheckMagic(byte[] magic) => "pbc\0"u8.SequenceEqual(magic);

    private int _width;
    private int _height;

    /// <summary>
    /// Image Width
    /// </summary>
    public int Width
    {
        get => _width;
        set
        {
            if (_width == value)
                return;

            _width = value;
            ResizeTiles();
        }
    }

    /// <summary>
    /// Image Height
    /// </summary>
    public int Height
    {
        get => _height;
        set
        {
            if (_height == value)
                return;

            _height = value;
            ResizeTiles();
        }
    }

    public int OffsetX { get; }
    public int OffsetY { get; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Tile[,] Tiles { get; internal set; }

    public Tile this[int height, int width]
    {
        get => Tiles[height, width];
        set => Tiles[height, width] = value;
    }


    public PBCFileReader(byte[] buffer) : this(new MemoryStream(buffer))
    {

    }

    public PBCFileReader(Stream stream)
    {
        using var reader = new BinaryReader(stream);

        if (!CheckMagic(reader.ReadByteArray(4)))
            throw new Exception("This is not a PBC file!");

        _width = reader.ReadInt32();
        _height = reader.ReadInt32();

        OffsetX = reader.ReadInt32();
        OffsetY = reader.ReadInt32();

        Tiles = new Tile[Height, Width];

        for (var h = 0; h < Height; h++)
            for (var w = 0; w < Width; w++)
                Tiles[h, w] = new Tile(reader);
    }

    public byte[] SaveAsBytes()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);

        writer.Write("pbc\0"u8);

        writer.Write(Width);
        writer.Write(Height);

        writer.Write(OffsetX);
        writer.Write(OffsetY);

        for (var h = 0; h < Height; h++)
            for (var w = 0; w < Width; w++)
                Tiles[h, w].Write(writer);

        return stream.ToArray();
    }

    private void ResizeTiles()
    {
        if (_width <= 0 || _height <= 0)
        {
            Tiles = new Tile[0, 0];
            return;
        }

        var newTiles = new Tile[_height, _width];

        // Create empty tiles for the new array
        for (var h = 0; h < _height; h++)
            for (var w = 0; w < _width; w++)
                newTiles[h, w] = new Tile();

        // Copy existing tiles to the new array, up to the new dimensions
        if (Tiles != null)
        {
            var minHeight = Math.Min(Tiles.GetLength(0), _height);
            var minWidth = Math.Min(Tiles.GetLength(1), _width);

            for (var h = 0; h < minHeight; h++)
                for (var w = 0; w < minWidth; w++)
                    newTiles[h, w] = Tiles[h, w];
        }

        Tiles = newTiles;
    }
}
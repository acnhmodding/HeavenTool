// Big thanks to https://github.com/McSpazzy/PBC
using System.ComponentModel;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;
using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.PBC;

public partial class PBCFileReader
{
    public class TriangleHeights
    {
        [DisplayName("Center height")]
        public float CenterHeight { get; set; }
        [DisplayName("Corner 1 height")]
        public float Corner1Height { get; set; }
        [DisplayName("Corner 2 height")]
        public float Corner2Height { get; set; }

        public TriangleHeights()
        {
            CenterHeight = 0;
            Corner1Height = 0;
            Corner2Height = 0;
        }

        public TriangleHeights(BinaryReader reader)
        {
            CenterHeight = reader.ReadSingle();
            Corner1Height = reader.ReadSingle();
            Corner2Height = reader.ReadSingle();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(CenterHeight);
            writer.Write(Corner1Height);
            writer.Write(Corner2Height);
        }
    }

    public class HeightMap
    {
        // File order: top, left, bottom, right.
        public TriangleHeights[] Triangles = new TriangleHeights[4];

        public HeightMap()
        {
            for (var i = 0; i < Triangles.Length; i++)
                Triangles[i] = new TriangleHeights();
        }

        public HeightMap(BinaryReader reader)
        {
            for (var i = 0; i < Triangles.Length; i++)
                Triangles[i] = new TriangleHeights(reader);
        }

        public void Write(BinaryWriter writer)
        {
            foreach (var triangle in Triangles)
                triangle.Write(writer);
        }
    }

    public class Tile
    {
        public HeightMap HeightMap;
        // Materials use the same triangle order as HeightMap.
        public TileType[] Materials;

        public Tile()
        {
            HeightMap = new HeightMap();
            Materials = new TileType[4];
            Array.Fill(Materials, TileType.Null);
        }

        public Tile(BinaryReader reader)
        {
            // All four height records precede the four material bytes.
            HeightMap = new HeightMap(reader);
            Materials = new TileType[4];
            for (var i = 0; i < Materials.Length; i++)
                Materials[i] = (TileType)reader.ReadByte();
        }

        public void Write(BinaryWriter writer)
        {
            HeightMap.Write(writer);
            foreach (var material in Materials)
                writer.Write((byte)material);
        }
    }

    public static bool CheckMagic(byte[] magic) => "pbc\0"u8.SequenceEqual(magic);

    private int _width;
    private int _height;

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
    public int OffsetZ { get; }

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
        OffsetZ = reader.ReadInt32();

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
        writer.Write(OffsetZ);

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

        for (var h = 0; h < _height; h++)
            for (var w = 0; w < _width; w++)
                newTiles[h, w] = new Tile();

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
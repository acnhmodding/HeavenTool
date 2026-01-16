// Big thanks to https://github.com/McSpazzy/PBC
namespace HeavenTool.IO.FileFormats.PBC;

/// <summary>
/// A class to read PBC files
/// </summary>
public partial class PBCFileReader
{
    public class Quadrant
    {
        public float Val1 { get; set; }
        public float Val2 { get; set; }
        public float Val3 { get; set; }

        public Quadrant(BinaryReader reader)
        {
            Val1 = reader.ReadSingle();
            Val2 = reader.ReadSingle();
            Val3 = reader.ReadSingle();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Val1);
            writer.Write(Val2);
            writer.Write(Val3);
        }
    }

    public class HeightMap
    {
        public Quadrant[,] Quadrants;

        public HeightMap(BinaryReader reader)
        {

            Quadrants = new Quadrant[2, 2];

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
        //public float[][,] Layers;
        public HeightMap HeightMap;
        public TileType[,] Type;

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
    public static bool MagicMatches(byte[] magic)
    {
        return "pbc\0"u8.SequenceEqual(magic);
    }

    /// <summary>
    /// Image Width
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Image Height
    /// </summary>
    public int Height { get; }

    public int OffsetX { get; }
    public int OffsetY { get; }

    public Tile[,] Tiles { get; set; }

    public Tile this[int height, int width]
    {
        get => Tiles[height, width];
        set => Tiles[height, width] = value;
    }


    public PBCFileReader(byte[] buffer)
    {
        using var stream = new MemoryStream(buffer);
        using var reader = new BinaryFileReader(stream);

        if (!MagicMatches(reader.ReadBytes(4)))
            throw new Exception("This is not a PBC file!");

        Width = reader.ReadInt32();
        Height = reader.ReadInt32();

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
}
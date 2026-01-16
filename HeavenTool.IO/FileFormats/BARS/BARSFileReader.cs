using HeavenTool.IO.FileFormats.BWAV;
using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.IO.FileFormats.BARS;

public class AudioAsset
{
    public uint crcHash;
    //public uint amtaOffset;
    public int assetOffset;
    public string assetName;
    public string assetType;
    public bool isPrefetch = false;
    public byte[] assetData;
    public AudioMetadata amtaData;
    public BinaryWaveFile binaryWave;

    public override string ToString()
    {
        return assetName;
    }
}

public class BARSFileReader
{
    public BARSFileReader(string fileName) : this(new FileStream(fileName, FileMode.Open))
    {
        // Open using a fileName path
    }

    public bool BigEndian { get; set; }
    public byte VersionMajor { get; set; }
    public byte VersionMinor { get; set; }

    public AudioAsset[] AudioAssets { get; set; }

    public BARSFileReader(Stream stream)
    {
        using var reader = new BinaryReader(stream);

        var magic = reader.ReadString(4);

        if (magic != "BARS")
            throw new Exception("This is not a BARS file");

        var size = reader.ReadInt32();
        var endian = reader.ReadUInt16();

        reader.BigEndian = BigEndian = endian != 0xFEFF;

        VersionMajor = reader.ReadByte();
        VersionMinor = reader.ReadByte();

        if (VersionMajor != 1 || (VersionMinor != 1 && VersionMinor != 2))
            throw new Exception("BARS version not supported");

        var assetCount = reader.ReadInt32();
        AudioAssets = new AudioAsset[assetCount];

        // crc hashes
        for (int i = 0; i < assetCount; i++)
        {
            AudioAssets[i] = new AudioAsset()
            {
                crcHash = reader.ReadUInt32()
            };
        }

        for (int i = 0; i < assetCount; i++)
        {
            var audioAsset = AudioAssets[i];

            // Read AudioMetadata
            using (reader.CreateScopeAt(reader.ReadUInt32()))
                audioAsset.amtaData = new AudioMetadata(audioAsset, reader);

            var checkHash = audioAsset.amtaData.AssetName.ToCRC32();
            if (checkHash != audioAsset.crcHash) throw new Exception($"Invalid CRC32 Hash for {i}: {audioAsset.amtaData.AssetName}! Expected: {audioAsset.crcHash:x} | Got {checkHash:x}");
            audioAsset.assetOffset = reader.ReadInt32();
        }

        // Sort AudioAssets by lowest assetOffset
        Array.Sort(AudioAssets, (a, b) => a.assetOffset.CompareTo(b.assetOffset));

        // Here we have to group because some files shares the same assetOffset
        var groups = AudioAssets.GroupBy(x => x.assetOffset).ToList();
        int groupCount = groups.Count;

        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            var assetOffset = group.Key;

            if (assetOffset <= 0)
                throw new Exception("Invalid asset offset, file may be corrupted");

            // Read the magic string for BWAV check
            var magicBwav = reader.ReadStringAt(assetOffset, 4);
            if (magicBwav != "BWAV")
                throw new Exception($"Only BWAV files are supported at this moment. (Got {magicBwav})");

            var nextAssetOffset = (i + 1 < groupCount) ? groups[i + 1].Key : (int) reader.Length;
            var assetData = reader.ReadByteArrayAt(assetOffset, nextAssetOffset - assetOffset);
            var binaryWave = new BinaryWaveFile(assetData);

            // Assign the BinaryWave to all the audio assets in the group
            foreach (var audioAsset in group)
                audioAsset.binaryWave = binaryWave;
        }


        //for (int i = 0; i < assetCount; i++)
        //{
        //    var audioAsset = AudioAssets[i];
        //    if (audioAsset.assetOffset <= 0)
        //    {
        //        throw new Exception($"{audioAsset.amtaData.AssetName} does not contain a valid asset offset!"); 
        //    }


        //    var assetType = audioAsset.assetType = reader.ReadStringAt(audioAsset.assetOffset, 4);

        //    if (assetType != "BWAV")
        //    {
        //        throw new Exception("non-BWAV file is not supported");
        //        audioAsset.isPrefetch = assetType == "FSTP";
        //        reader.Position += 8;
        //        int assetSize = reader.ReadInt32();

        //        reader.JumpTo(AudioAssets[i].assetOffset);
        //        audioAsset.assetData = reader.ReadByteArray(assetSize);
        //    }
        //    else
        //    {


        //        byte[] assetData;
        //        if (i != AudioAssets.Length - 1)
        //        {
        //            assetData = reader.ReadByteArrayAt(audioAsset.assetOffset, (int)(AudioAssets[i + 1].assetOffset - audioAsset.assetOffset));
        //        }
        //        else
        //        {  // last entry
        //            assetData = reader.ReadByteArrayAt(audioAsset.assetOffset, (int)(reader.Length - audioAsset.assetOffset));
        //        }

        //        Console.WriteLine("-----------");
        //        Console.WriteLine($"Current index: {i}\n" +
        //            $"Current offset: {audioAsset.assetOffset}\n" +
        //            $"Next offset: {AudioAssets[i + 1].assetOffset}");
        //        Console.WriteLine(reader.ReadStringAt(audioAsset.assetOffset, 4));
        //        Console.WriteLine(assetData.Length);
        //        Console.WriteLine($"IsLast: {i == AudioAssets.Length - 1}");

        //        AudioAssets[i].binaryWave = new BinaryWaveFile(assetData);
        //    }

        //}
    }
}
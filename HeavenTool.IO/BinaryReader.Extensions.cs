using BinaryReader = AeonSake.BinaryTools.BinaryReader;

namespace HeavenTool.IO;

public static class BinaryReaderExtensions
{
    extension(BinaryReader reader)
    {
        public string ReadStringPointer()
        {
            var offset = reader.Position + reader.ReadUInt32();

            using (reader.CreateScopeAt(offset))
                return reader.ReadTerminatedString();
        }
    }
}

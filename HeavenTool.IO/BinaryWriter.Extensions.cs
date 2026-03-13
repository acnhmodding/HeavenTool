using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO;

public static class BinaryWriterExtensions
{
    extension(BinaryWriter writer)
    {
        public WriterScopePointer CreatePointer()
        {
            var pointer = new WriterScopePointer(writer, writer.Position);
            writer.Pad(4);
            return pointer;
        }

        public WriterScopePointer CreatePointerAt(long position)
        {
            var pointer = new WriterScopePointer(writer, position);
            writer.Pad(4);
            return pointer;
        }
    }
}

public sealed class WriterScopePointer(BinaryWriter writer, long position)
{
    public long Position { get; } = position;

    public void Resolve(uint pos)
    {
        using (writer.CreateScope())
            writer.WriteAt(Position, pos);
    }

    public void Resolve(Action<BinaryWriter> action, long? relativePosition = null)
    {
        var pos = (uint)writer.Position;
        if (relativePosition.HasValue)
            pos -= (uint)relativePosition.Value;
        else pos -= (uint) Position;

        action(writer);

        using (writer.CreateScope())
            writer.WriteAt(Position, pos);
    }
}
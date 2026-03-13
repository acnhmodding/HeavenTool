using BinaryWriter = AeonSake.BinaryTools.BinaryWriter;

namespace HeavenTool.IO.FileFormats.BARS;

public class StringPool
{
    public Dictionary<string, List<WriterScopePointer>> Strings { get; private set; } = [];

    public void Write(BinaryWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        foreach (var (str, scopes) in Strings)
        {
            foreach (var scope in scopes)
            {
                var offset = (uint) (writer.Position - scope.Position);
                scope.Resolve(offset);
            }
            writer.WriteTerminatedString(str);
        }
    }

    public WriterScopePointer AddString(BinaryWriter writer, string str)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(str);

        var pointer = writer.CreatePointer();

        // Add to the strings list so we can resolve the offset later
        if (Strings.TryGetValue(str, out List<WriterScopePointer>? value))
            value.Add(pointer);
        else Strings[str] = [pointer];

        return pointer;
    }
}

using System.Text;

namespace HeavenTool.IO.FileFormats.ResourceSizeTable;

/// <summary>Reads the SARC container of ACNH model archives and validates their BFRES members.</summary>
internal static class ModelArchive
{
    internal readonly record struct Member(string? Name, int Offset, int Length);

    /// <summary>Reads a little-endian SARC's member ranges (absolute offsets) in node order.</summary>
    public static bool TryReadSarc(ReadOnlySpan<byte> archive, out Member[] members)
    {
        try
        {
            members = ReadSarc(archive);
            return true;
        }
        catch (InvalidDataException)
        {
            members = [];
            return false;
        }
    }

    /// <inheritdoc cref="TryReadSarc"/>
    /// <exception cref="InvalidDataException">The data is not a valid SARC.</exception>
    public static Member[] ReadSarc(ReadOnlySpan<byte> archive)
    {
        var sarc = new ResourceReader(archive);
        if (!sarc.Magic(0, "SARC"u8) || sarc.U16(4) != 0x14 || sarc.U16(6) != 0xfeff || sarc.U32(8) != sarc.Length ||
            sarc.U16(16) != 0x100 || !sarc.Magic(20, "SFAT"u8) || sarc.U16(24) != 12)
            throw new InvalidDataException("Expected a little-endian SARC archive.");

        int count = sarc.U16(26), names = 32 + count * 16;
        long data = sarc.U32(12);
        if (names > sarc.Length || data > sarc.Length) throw new InvalidDataException("SARC nodes exceed the archive.");
        bool hasNames = names <= sarc.Length - 8 && sarc.Magic(names, "SFNT"u8);

        var members = new Member[count];
        for (int i = 0; i < count; i++)
        {
            int node = 32 + i * 16;
            uint attributes = sarc.U32(node + 4);
            long start = data + sarc.U32(node + 8), end = data + sarc.U32(node + 12);
            if (end < start || end > sarc.Length) throw new InvalidDataException("SARC member exceeds the archive.");
            string? name = null;
            if (attributes >> 24 != 0)
            {
                long at = names + 8 + (attributes & 0xffffff) * 4L;
                int terminator = hasNames && at < data ? sarc.Range(at, data - at).IndexOf((byte)0) : -1;
                if (terminator < 0) throw new InvalidDataException("SARC member name is invalid.");
                name = Encoding.ASCII.GetString(sarc.Range(at, terminator));
            }
            members[i] = new(name, (int)start, (int)(end - start));
        }
        return members;
    }

    /// <summary>
    /// The canonical single-member ACNH model archive: one BFRES at 0x2000. Other members
    /// (physics, shaders, nested archives) have independent allocations.
    /// </summary>
    public static bool TryReadSingleBfres(ReadOnlySpan<byte> archive, out Member bfres)
    {
        bfres = default;
        if (!TryReadSarc(archive, out var members) || members is not [{ Offset: 0x2000 } member] ||
            !IsBfres(archive.Slice(member.Offset, member.Length)))
            return false;
        bfres = member;
        return true;
    }

    /// <summary>An unrelocated little-endian BFRES v9 with a relocation table inside its bounds.</summary>
    public static bool IsBfres(ReadOnlySpan<byte> data)
    {
        var bfres = new ResourceReader(data);
        if (bfres.Length < 0xf0 || !bfres.Magic(0, "FRES    "u8) || bfres.U32(8) != 0x00090000 ||
            bfres.U16(12) != 0xfeff || bfres.U16(20) != 0 || bfres.U32(28) != bfres.Length)
            return false;
        uint relocation = bfres.U32(24);
        return relocation <= bfres.Length - 16L && bfres.Magic(relocation, "_RLT"u8);
    }
}

#nullable enable

namespace HeavenTool.ModManager.Villagers;

/// <summary>The NmlNpcParam columns the villager creator edits, by the string the game hashes for each.</summary>
public static class VillagerColumns
{
    public const string Label = "Label string8";
    public const string UniqueId = "UniqueID u16";
    public const string ResName = "ResName string64";
    public const string Personality = "NpcLooks.hshCstringRef";
    public const string SubPersonality = "NpcTalkType u8";
    public const string Hobby = "Hobby.hshCstringRef";
    public const string BirthMonth = "BirthMonth u8";
    public const string BirthDay = "BirthMDay u8";
    public const string Color1 = "Color1 u8";
    public const string Color2 = "Color2 u8";
    public const string NameColor = "NpcColor u8";
    public const string Tops = "Tops u16";
    public const string RainHat = "RainHat u16";
    public const string RainWear = "RainWare u16";
    public const string Umbrella = "Umbrella u16";
    public const string Photo = "BromideItemID s16";
    public const string Poster = "PosterItemID s16";

    // Columns whose names aren't known, by hash. The wiki guide names the styles; the copy of UniqueID always matches it in vanilla.
    public const uint UniqueIdCopy = 0x504428b5;
    public const uint Style1 = 0x5ef86f1f;
    public const uint Style2 = 0x195815cf;
}
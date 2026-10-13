using System.ComponentModel;

namespace HeavenTool.IO.FileFormats.PBC;

// IDs are zero-based row indices in ColGroundAttributeParam.bcsv.
public enum TileType : byte
{
    [Description("Grass")]
    Grass = 0,
    [Description("River")]
    River = 1,
    [Description("Soil (hard)")]
    SoilHard = 2,
    [Description("Stone")]
    Stone = 3,
    [Description("Sandy beach")]
    SandyBeach = 4,
    [Description("Sea")]
    Sea = 5,
    [Description("Wood (thick)")]
    WoodThick = 6,
    [Description("Null")]
    Null = 7,
    [Description("Building")]
    Building = 8,
    [Description("Editing prohibited")]
    EditingProhibited = 9,
    [Description("Door 0")]
    Door0 = 10,
    [Description("Camera")]
    Camera = 11,
    [Description("Brackish water")]
    BrackishWater = 12,
    [Description("Replacement floor")]
    ReplacementFloor = 13,
    [Description("Landing")]
    Landing = 14,
    [Description("Terraforming prohibited")]
    TerraformingProhibited = 15,
    [Description("Soil (soft)")]
    SoilSoft = 16,
    [Description("Marble")]
    Marble = 17,
    [Description("Coated floor")]
    CoatedFloor = 18,
    [Description("Gravel")]
    Gravel = 19,
    [Description("Fallen leaves")]
    FallenLeaves = 20,
    [Description("Snow")]
    Snow = 21,
    [Description("Wood (thin)")]
    WoodThin = 22,
    [Description("Carpet")]
    Carpet = 23,
    [Description("Tatami")]
    Tatami = 24,
    [Description("Iron")]
    Iron = 25,
    [Description("Path: dirt")]
    PathDirt = 26,
    [Description("Path: stone paving")]
    PathStonePaving = 27,
    [Description("Railing")]
    Railing = 28,
    [Description("Shoreline / water's edge")]
    Shoreline = 29,
    [Description("Rug only")]
    RugOnly = 30,
    [Description("Path: dark dirt")]
    PathDarkDirt = 31,
    [Description("Path: sand")]
    PathSand = 32,
    [Description("Path: brick")]
    PathBrick = 33,
    [Description("Path: wood")]
    PathWood = 34,
    [Description("Path: tile")]
    PathTile = 35,
    [Description("Path: fan-shaped stone paving")]
    PathFanShapedStonePaving = 36,
    [Description("Entry prohibited")]
    EntryProhibited = 37,
    [Description("Sponge mat")]
    SpongeMat = 38,
    [Description("Vinyl sheet")]
    VinylSheet = 39,
    [Description("Custom-design floor")]
    CustomDesignFloor = 40,
    [Description("Rocky ground")]
    RockyGround = 41,
    [Description("Rocky ground: entry prohibited")]
    RockyGroundEntryProhibited = 42,
    [Description("Rocky-ground puddle")]
    RockyGroundPuddle = 43,
    [Description("Sandstone")]
    Sandstone = 44,
    [Description("Pier")]
    Pier = 45,
    [Description("Shoreline: entry prohibited")]
    ShorelineEntryProhibited = 46,
    [Description("Building without grass growth")]
    BuildingWithoutGrassGrowth = 47,
    [Description("Indoor grass")]
    IndoorGrass = 48,
    [Description("Editing prohibited, without grass growth")]
    EditingProhibitedWithoutGrassGrowth = 49,
    [Description("Mud / swamp")]
    MudSwamp = 50,
    [Description("Grass for rock generation")]
    GrassForRockGeneration = 51,
    [Description("Outdoor custom design")]
    OutdoorCustomDesign = 52,
    [Description("Puddle (soil)")]
    PuddleSoil = 53,
    [Description("Museum water surface")]
    MuseumWaterSurface = 54,
    [Description("Right rotation prohibited")]
    RightRotationProhibited = 55,
    [Description("Left rotation prohibited")]
    LeftRotationProhibited = 56,
    [Description("Rotation prohibited")]
    RotationProhibited = 57,
    [Description("Hard soil: entry prohibited")]
    HardSoilEntryProhibited = 58,
    [Description("Sandy beach: entry prohibited")]
    SandyBeachEntryProhibited = 59,
    [Description("Water (sand)")]
    WaterSand = 60,
    [Description("Water (gravel)")]
    WaterGravel = 61,
    [Description("Lava")]
    Lava = 62,
    [Description("Indoor sand")]
    IndoorSand = 63,
    [Description("Sea: entry prohibited")]
    SeaEntryProhibited = 64,
    [Description("Luna: indoor only")]
    LunaIndoorOnly = 65,
    [Description("Earth mound / embankment")]
    EarthMound = 66,
    [Description("Custom 0")]
    Custom0 = 67,
    [Description("Custom 1")]
    Custom1 = 68,
    [Description("Custom 2")]
    Custom2 = 69,
    [Description("Sandy beach (no footprints)")]
    SandyBeachNoFootprints = 70,
    [Description("Custom 3")]
    Custom3 = 71,
    [Description("Ink")]
    Ink = 72,
}
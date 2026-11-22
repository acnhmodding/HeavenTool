#nullable enable
using AeonSake.NintendoTools.FileFormats.Byml;
using System.Text.RegularExpressions;
using static HeavenTool.IO.AcnhEnvironment.AcnhEnvironmentParameters;

namespace HeavenTool.IO.AcnhEnvironment;

/// <summary>
/// One EnvPaletteIdrSelector entry: how a building interior picks its palette and color grading. The game takes
/// <see cref="PaletteSetName"/> when <see cref="UsesPaletteSet"/>, and otherwise the IdrInside set with
/// <see cref="PaletteName"/> as a fixed palette, which it copies whole instead of blending the set.
/// </summary>
/// <param name="ColorCorrection">The mColorCorrectType file's lower-case name, such as envstrairport.</param>
public sealed record AcnhIdrEnvironmentEntry(string IdrName, bool UsesPaletteSet, string PaletteSetName, string PaletteName, string ColorCorrection);

/// <summary>Param/Gfx/EnvPaletteIdrSelector.byml, and which entry a building's model is lit by.</summary>
public sealed partial class AcnhIdrEnvironment
{
    /// <summary>The set a building interior falls back to, and Stage_Idr's SceneEnvMgr default.</summary>
    public const string DefaultSet = "IdrInside";

    // mColorCorrectType's enum names in order, and the baglccr file the game picks for each.
    private static readonly string[] ColorCorrectTypes = ["cDefault", "cRoomInside", "cDemoAmiibo", "cDemoSpotlight", "cDemoTourCounter",
        "cRoomWhite", "cStrMuseumEnt", "cStrMuseumFish00", "cStrMuseumFish01", "cStrMuseumFish02", "cStrMuseumFossil",
        "cStrMuseumInsect00", "cStrMuseumInsect01", "cStrMuseumInsect02", "cStrAirport"];
    private static readonly string[] ColorCorrectFiles = ["env", "envroom", "envdemoamiibo", "envdemospotlight", "envdemotourcounter",
        "envroomwhite", "envstrmuseument", "envstrmuseumfish00", "envstrmuseumfish01", "envstrmuseumfish02", "envstrmuseumfossil",
        "envstrmuseuminsect00", "envstrmuseuminsect01", "envstrmuseuminsect02", "envstrairport"];

    private readonly Dictionary<string, AcnhIdrEnvironmentEntry> entries;

    public IReadOnlyCollection<AcnhIdrEnvironmentEntry> Entries => entries.Values;

    private AcnhIdrEnvironment(Dictionary<string, AcnhIdrEnvironmentEntry> entries) => this.entries = entries;

    public static AcnhIdrEnvironment Read(Func<string, IBymlNode?> read)
    {
        var body = Dictionary(Field(Dictionary(read("Param/Gfx/EnvPaletteIdrSelector.byml")), "EnvPaletteIdrSelector"));
        var entries = new Dictionary<string, AcnhIdrEnvironmentEntry>(StringComparer.Ordinal);
        foreach (var node in Array(Field(body, "mPaletteList[]")))
        {
            var p = Dictionary(node);
            string type = Field(p, "mColorCorrectType", "Enum") is BymlStringValue value ? value.Value
                : throw new InvalidDataException("Invalid mColorCorrectType.");
            int index = System.Array.IndexOf(ColorCorrectTypes, type);
            if (index < 0) throw new InvalidDataException($"Unknown mColorCorrectType {type}.");
            bool usesSet = Field(p, "mIdrEnvType", "s32") is BymlInt32Value envType ? envType.Value == 1
                : throw new InvalidDataException("Invalid mIdrEnvType.");
            var entry = new AcnhIdrEnvironmentEntry(Text(p, "mIdrName"), usesSet, Text(p, "mPaletteSetName"), Text(p, "mPaletteName"),
                ColorCorrectFiles[index]);
            entries.TryAdd(entry.IdrName, entry);
        }
        return new(entries);
    }

    public AcnhIdrEnvironmentEntry? Find(string idrName) => entries.GetValueOrDefault(idrName);

    /// <summary>
    /// The selector name a building model's interior is looked up by, or null for a model that isn't a building interior. The
    /// game builds it as "Idr" + the stage's building name + a number for the buildings that have several, and picks the model
    /// from the same number: IdrMarket0%d for both, IdrAirPort00 for IdrAirPort, IdrWherearenOffice00_%d for
    /// IdrWherearenOffice and IdrWherearenOffice_1 (the key adds _%d only past the first).
    /// </summary>
    public static string? IdrName(string model)
    {
        return model switch
        {
            "IdrAirPort00" => "IdrAirPort",
            "IdrHotelEnt00" => "IdrHotelEnt",
            "IdrMuseumCafe00" => "IdrMuseumCafe",
            "IdrTailor00" => "IdrTailor",
            "IdrTanukichi00" => "IdrTanukichi",
            "IdrTsunekichi00" => "IdrTsunekichi",
            "IdrTourCounterDemo" => "IdrTourCounterDemo",
            "IdrOffice01" => "IdrOffice01",
            "IdrOfficeDemo01" => "IdrOfficeDemo01",
            // The cloud island's dream house loads IdrDream01 under the name IdrDreamCloudIsland.
            "IdrDream00" => "IdrDream00",
            "IdrDream01" => "IdrDreamCloudIsland",
            "IdrWherearenOffice00_0" => "IdrWherearenOffice",
            "IdrWherearenOffice00_1" => "IdrWherearenOffice_1",
            // Buildings whose model and key share their number: IdrMarket01, IdrMuseumEnt02, IdrMuseumFish_1, ...
            _ => NumberedIdr().IsMatch(model) ? model : null,
        };
    }

    [GeneratedRegex(@"^Idr(Market0\d|MuseumEnt0\d|Museum(Art|Fish|Fossil|Insect)_\d)$")]
    private static partial Regex NumberedIdr();
}

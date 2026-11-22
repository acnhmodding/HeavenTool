#nullable enable
using AeonSake.NintendoTools.FileFormats.Byml;
using HeavenTool.IO.FileFormats.BFRES;
using System.Numerics;
using static HeavenTool.IO.AcnhEnvironment.AcnhEnvironmentParameters;

namespace HeavenTool.IO.AcnhEnvironment;

public sealed record AcnhRoomLightParameters(Vector3 Position, float Size, float Radius, float AlternateZ,
    Vector4 WarmColor, Vector4 WhiteColor, float WarmIntensity, float WhiteIntensity);

public sealed record AcnhStageEnvironment(float MinimumLightAngle, float IndirectSkyIntensity,
    float IndirectGroundIntensity, float IndirectIntensity, float AoIntensity, float DirectionalAoIntensity);

/// <summary>Immutable scene inputs, loaded once from the user's game resources.</summary>
public sealed class AcnhEnvironmentResources
{
    public AcnhEnvironmentPalettes Palettes { get; }
    public AcnhSunOrbit SunOrbit { get; }
    public AcnhStageEnvironment OutdoorStage { get; }
    public AcnhStageEnvironment RoomStage { get; }
    // Stage_Idr's SceneEnvMgr, for the building interiors.
    public AcnhStageEnvironment IdrStage { get; }
    public AcnhIdrEnvironment Idr { get; }
    public AcnhRoomLightParameters VillagerRoomLight { get; }
    public AcnhSkyScatterParameters SkyScatter { get; }
    // The villager room's EnvRoom grading, and the agl program that bakes it.
    public AcnhColorCorrection RoomColorCorrection { get; }
    public SwitchProcessShaderProgram ColorCorrectionMap { get; }
    public AcnhPanoramaEnvironment Panorama { get; }
    public AcnhAmbientOcclusion AmbientOcclusion { get; }
    // The house's projected shade; none when the game leaves it off.
    public AcnhProjectionShadow? InsideProjectionShadow { get; }
    // GameEnv's single bloom setting, used inside and out; none when it is disabled.
    public AcnhBloom? Bloom { get; }
    // SceneEnvMgr.byml's sunrise and sunset times, which turn building interiors' lights to their night setting.
    public AcnhDayNight DayNight { get; }
    // The light grids of the villager room's stage, which has no DynamicLightMgr file, and of Stage_Idr.
    public AcnhLightGridParameters RoomLightGrid { get; }
    public AcnhLightGridParameters IdrLightGrid { get; }

    private readonly string romfs;
    private readonly Func<string, IBymlNode?> read;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, AcnhColorCorrection> colorCorrections = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, AcnhSceneLights> sceneLights = new();

    private AcnhEnvironmentResources(AcnhEnvironmentPalettes palettes, AcnhSunOrbit sunOrbit, AcnhStageEnvironment outdoorStage,
        AcnhStageEnvironment roomStage, AcnhStageEnvironment idrStage, AcnhIdrEnvironment idr, string romfs, AcnhRoomLightParameters villagerRoomLight, AcnhSkyScatterParameters skyScatter,
        AcnhColorCorrection roomColorCorrection, SwitchProcessShaderProgram colorCorrectionMap, AcnhPanoramaEnvironment panorama,
        AcnhAmbientOcclusion ambientOcclusion, AcnhProjectionShadow? insideProjectionShadow, AcnhBloom? bloom,
        Func<string, IBymlNode?> read, AcnhDayNight dayNight, AcnhLightGridParameters roomLightGrid, AcnhLightGridParameters idrLightGrid)
    {
        Palettes = palettes;
        SunOrbit = sunOrbit;
        OutdoorStage = outdoorStage;
        RoomStage = roomStage;
        IdrStage = idrStage;
        Idr = idr;
        this.romfs = romfs;
        VillagerRoomLight = villagerRoomLight;
        SkyScatter = skyScatter;
        RoomColorCorrection = roomColorCorrection;
        ColorCorrectionMap = colorCorrectionMap;
        Panorama = panorama;
        AmbientOcclusion = ambientOcclusion;
        InsideProjectionShadow = insideProjectionShadow;
        Bloom = bloom;
        this.read = read;
        DayNight = dayNight;
        RoomLightGrid = roomLightGrid;
        IdrLightGrid = idrLightGrid;
    }

    public static AcnhEnvironmentResources Load(string romfs) => Read(OpenStaticParam(romfs), romfs);

    /// <summary>Reads StaticParam through <paramref name="read"/>, and the files outside it from <paramref name="romfs"/>.</summary>
    public static AcnhEnvironmentResources Read(Func<string, IBymlNode?> read, string romfs)
    {
        var body = Dictionary(Field(Dictionary(read("Param/Gfx/RoomLightParamSet_RVer_20000.byml")), "RoomLightParamSet"));
        AcnhRoomLightParameters[] roomLights = [.. Array(Field(body, "mRoomLightParamArray[]")).Select(ReadRoomLight)];
        if (roomLights.Length < 2) throw new InvalidDataException("Missing room light parameters.");

        return new(AcnhEnvironmentPalettes.Read(read), AcnhSunOrbit.Load(romfs), ReadStage(read, "Stage"), ReadStage(read, "Stage_Inside"),
            ReadStage(read, "Stage_Idr"), AcnhIdrEnvironment.Read(read), romfs,
            roomLights[1], AcnhSkyScatterParameters.Read(read), AcnhColorCorrection.Load(romfs, "envroom"), AcnhColorCorrection.LoadMapProgram(romfs),
            AcnhPanoramaEnvironment.Read(read, romfs), AcnhAmbientOcclusion.Load(romfs), AcnhProjectionShadow.LoadInside(romfs), AcnhBloom.Load(romfs),
            read, AcnhDayNight.Read(read), AcnhLightGridParameters.Read(read, "Stage_Inside"), AcnhLightGridParameters.Read(read, "Stage_Idr"));
    }

    /// <summary>A GameEnv color correction by its lower-case name, such as envstrairport, read once.</summary>
    public AcnhColorCorrection ColorCorrection(string name) =>
        name == "envroom" ? RoomColorCorrection : colorCorrections.GetOrAdd(name, file => AcnhColorCorrection.Load(romfs, file));

    /// <summary>A scene's own lights from Param/Gfx/&lt;scene&gt;/, such as IdrTailor's, read once.</summary>
    public AcnhSceneLights SceneLights(string scene) => sceneLights.GetOrAdd(scene, name => AcnhSceneLights.Read(read, name));

    private static AcnhRoomLightParameters ReadRoomLight(IBymlNode entry)
    {
        var p = Dictionary(entry);
        return new(
            Position: Position3(p, "mPos"),
            Size: Number(p, "mSize"),
            Radius: Number(p, "mRadius"),
            AlternateZ: Number(p, "mAlternateLightPosZ"),
            WarmColor: Color(p, "mOrangeColor"),
            WhiteColor: Color(p, "mWhiteColor"),
            WarmIntensity: Number(p, "mOrangeColorIntensity"),
            WhiteIntensity: Number(p, "mWhiteColorIntensity"));
    }

    private static AcnhStageEnvironment ReadStage(Func<string, IBymlNode?> read, string stage)
    {
        var p = Dictionary(Field(Dictionary(read($"Param/Gfx/{stage}/SceneEnvMgr_0.byml")), "SceneEnvParam"));
        return new(
            MinimumLightAngle: Number(p, "mLimitLightAngle"),
            IndirectSkyIntensity: Number(p, "mIndirectSkyLightIntensity"),
            IndirectGroundIntensity: Number(p, "mIndirectGroundLightIntensity"),
            IndirectIntensity: Number(p, "mIndirectLightIntensity"),
            AoIntensity: Number(p, "mAoIntensity"),
            DirectionalAoIntensity: Number(p, "mAoDirectionalIntensity"));
    }
}

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

    private AcnhEnvironmentResources(AcnhEnvironmentPalettes palettes, AcnhSunOrbit sunOrbit, AcnhStageEnvironment outdoorStage,
        AcnhStageEnvironment roomStage, AcnhRoomLightParameters villagerRoomLight, AcnhSkyScatterParameters skyScatter,
        AcnhColorCorrection roomColorCorrection, SwitchProcessShaderProgram colorCorrectionMap, AcnhPanoramaEnvironment panorama,
        AcnhAmbientOcclusion ambientOcclusion, AcnhProjectionShadow? insideProjectionShadow, AcnhBloom? bloom)
    {
        Palettes = palettes;
        SunOrbit = sunOrbit;
        OutdoorStage = outdoorStage;
        RoomStage = roomStage;
        VillagerRoomLight = villagerRoomLight;
        SkyScatter = skyScatter;
        RoomColorCorrection = roomColorCorrection;
        ColorCorrectionMap = colorCorrectionMap;
        Panorama = panorama;
        AmbientOcclusion = ambientOcclusion;
        InsideProjectionShadow = insideProjectionShadow;
        Bloom = bloom;
    }

    public static AcnhEnvironmentResources Load(string romfs) => Read(OpenStaticParam(romfs), romfs);

    /// <summary>Reads StaticParam through <paramref name="read"/>, and the files outside it from <paramref name="romfs"/>.</summary>
    public static AcnhEnvironmentResources Read(Func<string, IBymlNode?> read, string romfs)
    {
        var body = Dictionary(HashedField(Dictionary(read("Param/Gfx/RoomLightParamSet_RVer_20000.byml")), "3ba9f63d"));
        AcnhRoomLightParameters[] roomLights = [.. Array(HashedField(body, "f0492a22")).Select(ReadRoomLight)];
        if (roomLights.Length < 2) throw new InvalidDataException("Missing room light parameters.");

        return new(AcnhEnvironmentPalettes.Read(read), AcnhSunOrbit.Load(romfs), ReadStage(read, "Stage"), ReadStage(read, "Stage_Inside"),
            roomLights[1], AcnhSkyScatterParameters.Read(read), AcnhColorCorrection.Load(romfs, "envroom"), AcnhColorCorrection.LoadMapProgram(romfs),
            AcnhPanoramaEnvironment.Read(read, romfs), AcnhAmbientOcclusion.Load(romfs), AcnhProjectionShadow.LoadInside(romfs), AcnhBloom.Load(romfs));
    }

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
        var p = Dictionary(HashedField(Dictionary(read($"Param/Gfx/{stage}/SceneEnvMgr_0.byml")), "d837d8ea"));
        return new(
            MinimumLightAngle: Number(p, "mLimitLightAngle"),
            IndirectSkyIntensity: Number(p, "mIndirectSkyLightIntensity"),
            IndirectGroundIntensity: Number(p, "mIndirectGroundLightIntensity"),
            IndirectIntensity: Number(p, "mIndirectLightIntensity"),
            AoIntensity: Number(p, "mAoIntensity"),
            DirectionalAoIntensity: Number(p, "mAoDirectionalIntensity"));
    }
}

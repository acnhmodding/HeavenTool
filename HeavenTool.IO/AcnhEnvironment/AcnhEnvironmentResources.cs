#nullable enable
using AeonSake.NintendoTools.FileFormats.Byml;
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

    private AcnhEnvironmentResources(AcnhEnvironmentPalettes palettes, AcnhSunOrbit sunOrbit, AcnhStageEnvironment outdoorStage,
        AcnhStageEnvironment roomStage, AcnhRoomLightParameters villagerRoomLight, AcnhSkyScatterParameters skyScatter)
    {
        Palettes = palettes;
        SunOrbit = sunOrbit;
        OutdoorStage = outdoorStage;
        RoomStage = roomStage;
        VillagerRoomLight = villagerRoomLight;
        SkyScatter = skyScatter;
    }

    public static AcnhEnvironmentResources Load(string romfs) => Read(OpenStaticParam(romfs), AcnhSunOrbit.Load(romfs));

    public static AcnhEnvironmentResources Read(Func<string, IBymlNode?> read, AcnhSunOrbit sunOrbit)
    {
        var body = Dictionary(HashedField(Dictionary(read("Param/Gfx/RoomLightParamSet_RVer_20000.byml")), "3ba9f63d"));
        AcnhRoomLightParameters[] roomLights = [.. Array(HashedField(body, "f0492a22")).Select(ReadRoomLight)];
        if (roomLights.Length < 2) throw new InvalidDataException("Missing room light parameters.");

        return new(AcnhEnvironmentPalettes.Read(read), sunOrbit, ReadStage(read, "Stage"), ReadStage(read, "Stage_Inside"),
            roomLights[1], AcnhSkyScatterParameters.Read(read));
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

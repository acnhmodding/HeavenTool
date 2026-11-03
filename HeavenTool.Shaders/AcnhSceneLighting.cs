using HeavenTool.IO.AcnhEnvironment;
using System.Numerics;

namespace HeavenTool.Shaders;

/// <summary>Supplies the recovered game lighting inputs; missing scene GI/output passes remain renderer responsibilities.</summary>
public static class AcnhSceneLighting
{
    public static DateTime PreviewDate { get; } = DateTime.Now;

    public static AcnhLightingPreset Evaluate(AcnhEnvironmentResources resources, float clockHour, bool inside = false, string? indoorSet = null)
    {
        float hour = AcnhEnvironmentTime.EffectiveHour(clockHour, AcnhEnvironmentTime.SeasonalOffset(PreviewDate));
        string set = inside ? indoorSet ?? AcnhEnvironmentPalettes.IndoorSet(true, true) : "NormalOutside";
        var palette = resources.Palettes.Evaluate(set, hour).Values;
        var stage = inside ? resources.RoomStage : resources.OutdoorStage;
        var baseline = inside ? AcnhLightingPreset.Room : AcnhLightingPreset.Day;
        var direction = resources.SunOrbit.DirectDirection(hour, inside, stage.MinimumLightAngle);

        // The preset's viewer stand-ins, which its exposure was matched with: inside, the house shades the sun out at the
        // room's shadow strength, which the viewer has no house to do.
        float directIntensity = inside ? palette.DirectIntensity * (1 - palette.ShadowIntensity) : palette.DirectIntensity;
        return new(set, palette.DirectColor, directIntensity, direction, palette.SkyColor, palette.GroundColor, palette.HemisphereIntensity, stage.IndirectIntensity)
        {
            PreviewIndirectLight = baseline.PreviewIndirectLight,
            User0 = User0(baseline, palette, stage, resources.SkyScatter, resources.SunOrbit.SkyDirection(hour, inside), inside),
            Environment = baseline.Environment,
            LocalLights = inside ? RoomLights(resources.VillagerRoomLight, palette) : [],
        };
    }

    // Keep unrelated shared-block inputs until their own producers are recovered. Replace only established fields.
    private static Dictionary<int, Vector4> User0(AcnhLightingPreset baseline, AcnhEnvironmentPalette palette,
        AcnhStageEnvironment stage, AcnhSkyScatterParameters sky, Vector3 skyDirection, bool inside)
    {
        var user0 = baseline.User0.ToDictionary();
        // The room preset raises the captured .27563 to .6 against its fill; scale the evaluated strength the same way.
        user0[4] = new(palette.WorldAoIntensity * (inside ? baseline.User0[4].X / .27563f : 1), 0, 0, 0);
        // SkyScatter update 710155b970 -> RuntimeUBO upload 710152d700, declarations 4–7 and 10.
        user0[6] = new(sky.VirtualCameraHeight, palette.RayleighIntensity, palette.MieIntensity, sky.EarthColorIntensity);
        user0[8] = new(new Vector3(palette.SunColor.X, palette.SunColor.Y, palette.SunColor.Z) * palette.SkyLightIntensity, 0);
        user0[9] = new(AcnhSkyScattering.SunScatterColor(sky, palette, skyDirection), 0);
        user0[10] = new(skyDirection, 0);
        // Declarations 37–38 share row 24 with unrelated values; preserve its other components.
        user0[24] = user0.GetValueOrDefault(24) with { X = sky.SunEraserPower, Y = sky.SunHorizonOffset };
        user0[17] = user0[17] with { Y = stage.IndirectSkyIntensity, Z = stage.IndirectGroundIntensity, W = palette.ShadowIntensity };
        user0[18] = new(palette.EnvironmentMapTintIntensity, stage.AoIntensity, stage.DirectionalAoIntensity, palette.DirectIntensity);
        // SceneEnvMgr -> RuntimeUBO +0x6a4 -> declaration 84 at GPU byte 0x490.
        user0[73] = palette.FieldGIOverrideColor;
        return user0;
    }

    private static List<AcnhLocalLight> RoomLights(AcnhRoomLightParameters room, AcnhEnvironmentPalette palette)
    {
        // Current room previews recenter the game's 32×5-unit room origin on (0,0,0).
        var origin = new Vector3(160, 0, 160);
        // The chosen color's alpha is not radiance.
        static Vector3 Rgb(Vector4 color) => new(color.X, color.Y, color.Z);

        // The warm color is the light switch's on state.
        if (room.Radius <= room.Size) throw new InvalidDataException("Invalid room point-light falloff.");
        var lights = new List<AcnhLocalLight>
        {
            new(room.Position - origin, Rgb(room.WarmColor) * palette.RoomPointIntensity, room.Size, room.Radius - room.Size)
        };

        var spot = palette.RoomSpot;
        if (spot.Intensity > 0)
        {
            if (spot.Far <= spot.Size || spot.Fovy <= 0) throw new InvalidDataException("Invalid room spotlight parameters.");
            // Divide the spot's radiance by FOV/(2π), and store half its FOV and inverse(far-size).
            var color = Rgb(spot.Color) * (spot.Intensity / (spot.Fovy / (2 * MathF.PI)));
            lights.Add(new(new Vector3(spot.Position, room.AlternateZ) - origin, color, spot.Size, spot.Far - spot.Size)
            {
                Direction = spot.Direction,
                Cone = spot.Fovy * .5f
            });
        }
        return lights;
    }
}

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

        // Inside, the room's walls, ceiling and window pieces cast the shadows that leave only the sun through its windows.
        return new(set, palette.DirectColor, palette.DirectIntensity, direction, palette.SkyColor, palette.GroundColor, palette.HemisphereIntensity, stage.IndirectIntensity)
        {
            // Inside, the environment probe gives the room the indirect light the preset's fill stands in for.
            PreviewIndirectLight = inside ? Vector3.Zero : baseline.PreviewIndirectLight,
            User0 = User0(baseline, palette, stage, resources.SkyScatter, resources.SunOrbit.SkyDirection(hour, inside)),
            Environment = baseline.Environment,
            LocalLights = inside ? RoomLights(resources.VillagerRoomLight, palette) : [],
            // Stage_Inside has no DynamicLightMgr file, so the room keeps the constructor's grid.
            LightGrid = inside ? new(resources.RoomLightGrid, Vector2.Zero) : null,
            // The villager room's EnvRoom grading; the outdoor Env grading is disabled.
            ColorGrading = inside && resources.RoomColorCorrection.Filters is { } filters ? new(resources.ColorCorrectionMap, filters) : null,
            // envinside.baglcube's capture height and clip range. CubeMapMgr (71014e5f0c) moves the capture at run time, so
            // its authored (40, 20, 40) is not kept; the room's middle is the origin here.
            EnvironmentProbe = inside ? new(new(0, 20, 0), 1, 10000, resources.Panorama) : null,
            ProjectionShadow = inside ? resources.InsideProjectionShadow : null,
            Bloom = resources.Bloom?.WithPalette(palette),
            ScreenOcclusion = new(resources.AmbientOcclusion, inside ? resources.AmbientOcclusion.Inside : resources.AmbientOcclusion.Outside),
        };
    }

    /// <summary>
    /// A building interior (Idr), lit by its EnvPaletteIdrSelector entry <paramref name="idrName"/> the way 710153a9e4 picks it:
    /// the entry's palette set, or else IdrInside with the entry's fixed palette, or plain IdrInside when it has neither.
    /// </summary>
    public static AcnhLightingPreset EvaluateIdr(AcnhEnvironmentResources resources, float clockHour, string idrName)
    {
        float hour = AcnhEnvironmentTime.EffectiveHour(clockHour, AcnhEnvironmentTime.SeasonalOffset(PreviewDate));
        var palettes = resources.Palettes;
        var entry = resources.Idr.Find(idrName);
        var sample = entry switch
        {
            { UsesPaletteSet: true } when palettes.HasSet(entry.PaletteSetName) => palettes.Evaluate(entry.PaletteSetName, hour),
            { UsesPaletteSet: false } when palettes.HasPalette(entry.PaletteName) => palettes.EvaluatePalette(AcnhIdrEnvironment.DefaultSet, entry.PaletteName),
            _ => palettes.Evaluate(AcnhIdrEnvironment.DefaultSet, hour),
        };
        var palette = sample.Values;
        var stage = resources.IdrStage;
        var baseline = AcnhLightingPreset.Room;
        var direction = resources.SunOrbit.DirectDirection(hour, true, stage.MinimumLightAngle);
        // Without an entry the lookup leaves mColorCorrectType at cDefault, the Env grading.
        var grading = resources.ColorCorrection(entry?.ColorCorrection ?? "env");

        return new(sample.Set, palette.DirectColor, palette.DirectIntensity, direction, palette.SkyColor, palette.GroundColor, palette.HemisphereIntensity, stage.IndirectIntensity)
        {
            // The building's own capture (its CubeMapMgr) isn't placed yet, so the preview fill stands in for the probe.
            PreviewIndirectLight = baseline.PreviewIndirectLight,
            User0 = User0(baseline, palette, stage, resources.SkyScatter, resources.SunOrbit.SkyDirection(hour, true)),
            Environment = baseline.Environment,
            LocalLights = SceneLights(resources.SceneLights(idrName), resources.DayNight.IndoorLightBlend(hour)),
            // The game centres Stage_Idr's grid on the scene's focus as it moves; the preview's focus is the building's origin.
            LightGrid = new(resources.IdrLightGrid, Vector2.Zero),
            ColorGrading = grading.Filters is { } filters ? new(resources.ColorCorrectionMap, filters) : null,
            Bloom = resources.Bloom?.WithPalette(palette),
            ScreenOcclusion = new(resources.AmbientOcclusion, resources.AmbientOcclusion.Inside),
        };
    }

    // Keep unrelated shared-block inputs until their own producers are recovered. Replace only established fields.
    private static Dictionary<int, Vector4> User0(AcnhLightingPreset baseline, AcnhEnvironmentPalette palette,
        AcnhStageEnvironment stage, AcnhSkyScatterParameters sky, Vector3 skyDirection)
    {
        var user0 = baseline.User0.ToDictionary();
        user0[4] = new(palette.WorldAoIntensity, 0, 0, 0);
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
        // Only the water programs read row 69, and only its x.
        user0[69] = new(palette.WaterSaturation, 0, 0, 0);
        return user0;
    }

    // A building interior's PBC is laid 32 collision cells of 5 units past its own offset (71008471c0), so the model's origin
    // is the stage's (160, 0, 160), like the villager room's. The scene's light files are in stage space.
    private static readonly Vector3 SceneOrigin = new(160, 0, 160);

    /// <summary>
    /// A scene's lights as 71014eb250 packs them, points before spots. DynamicLightMgr adds its enabled scene lights (71014edd30);
    /// one bound to a model follows that model's bone instead, which the preview has no model for, so it is left out.
    /// IdrLightMgr makes every dual light from its first setting and switches it on (710148d4d8, 710148d818), whatever the
    /// file's mIsEnable, and blends it towards the second by <paramref name="blend"/> each frame (710148cfe0).
    /// </summary>
    private static List<AcnhLocalLight> SceneLights(AcnhSceneLights scene, float blend)
    {
        // Which manager adds its lights first isn't recovered; DynamicLightMgr's are put first.
        var points = scene.Points.Where(light => light.Enabled && !IsBound(light.BindModel))
            .Concat(scene.DualPoints.Select(light => AcnhPointLightParameters.Blend(light.Day, light.Night, blend)));
        var spots = scene.Spots.Where(light => light.Enabled && !IsBound(light.BindModel))
            .Concat(scene.DualSpots.Select(light => AcnhSpotLightParameters.Blend(light.Day, light.Night, blend)));
        static Vector3 Rgb(Vector4 color) => new(color.X, color.Y, color.Z);

        // Some files give a radius or far no larger than the size, such as night-only lights that are all zero by day;
        // 71014eb250 packs their falloff inverse as it comes out, infinite or negative.
        var lights = new List<AcnhLocalLight>();
        foreach (var light in points)
        {
            lights.Add(new(light.Position - SceneOrigin, Rgb(light.Color) * light.Intensity, light.Size, light.Radius - light.Size));
        }
        foreach (var light in spots)
        {
            lights.Add(new(light.Position - SceneOrigin, Rgb(light.Color) * (light.Intensity / (light.Fovy / (2 * MathF.PI))), light.Size, light.Far - light.Size)
            {
                Direction = light.Direction,
                Cone = light.Fovy * .5f
            });
        }
        return lights;
    }

    // 71014edd30 only checks the name's first character.
    private static bool IsBound(string model) => model.Length > 0 && model[0] != '\0';

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

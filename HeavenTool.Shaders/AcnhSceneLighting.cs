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
            // CubeMapMgr captures a house at its indoor height over a point that follows the actors, which the preview keeps at
            // the room's middle; envinside.baglcube's clip range.
            EnvironmentProbe = inside ? new(new(0, AcnhCubeMapStage.IndoorHeight, 0), 1, 10000, resources.Panorama) : null,
            ProjectionShadow = inside ? resources.InsideProjectionShadow : null,
            Bloom = resources.Bloom?.WithPalette(palette),
            ScreenOcclusion = new(resources.AmbientOcclusion, inside ? resources.AmbientOcclusion.Inside : resources.AmbientOcclusion.Outside),
        };
    }

    /// <summary>
    /// A building interior (Idr), lit by its EnvPaletteIdrSelector entry <paramref name="idrName"/> the way the game picks it:
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
            PreviewIndirectLight = Vector3.Zero,
            User0 = User0(baseline, palette, stage, resources.SkyScatter, resources.SunOrbit.SkyDirection(hour, true)),
            Environment = baseline.Environment,
            LocalLights = SceneLights(resources.SceneLights(idrName), resources.DayNight.IndoorLightBlend(hour)),
            // The game centres Stage_Idr's grid on the scene's focus as it moves; the preview's focus is the building's origin.
            LightGrid = new(resources.IdrLightGrid, Vector2.Zero),
            ColorGrading = grading.Filters is { } filters ? new(resources.ColorCorrectionMap, filters) : null,
            // The building's pinned capture position, or else the indoor height over the preview's focus, the building's origin.
            EnvironmentProbe = new(resources.CapturePosition(idrName) - SceneOrigin ?? new(0, AcnhCubeMapStage.IndoorHeight, 0), 1, 10000, resources.Panorama),
            Bloom = resources.Bloom?.WithPalette(palette),
            ScreenOcclusion = new(resources.AmbientOcclusion, resources.AmbientOcclusion.Inside),
        };
    }

    // Rows whose producers aren't recovered keep the baseline capture's values.
    private static Dictionary<int, Vector4> User0(AcnhLightingPreset baseline, AcnhEnvironmentPalette palette,
        AcnhStageEnvironment stage, AcnhSkyScatterParameters sky, Vector3 skyDirection)
    {
        var user0 = baseline.User0.ToDictionary();
        user0[4] = new(palette.WorldAoIntensity, 0, 0, 0);
        user0[6] = new(sky.VirtualCameraHeight, palette.RayleighIntensity, palette.MieIntensity, sky.EarthColorIntensity);
        user0[8] = new(new Vector3(palette.SunColor.X, palette.SunColor.Y, palette.SunColor.Z) * palette.SkyLightIntensity, 0);
        user0[9] = new(AcnhSkyScattering.SunScatterColor(sky, palette, skyDirection), 0);
        user0[10] = new(skyDirection, 0);
        // Row 24's z and w hold other values.
        user0[24] = user0.GetValueOrDefault(24) with { X = sky.SunEraserPower, Y = sky.SunHorizonOffset };
        user0[17] = new(stage.EnvironmentMapShadowIntensity, stage.IndirectSkyIntensity, stage.IndirectGroundIntensity, palette.ShadowIntensity);
        user0[18] = new(palette.EnvironmentMapTintIntensity, stage.AoIntensity, stage.DirectionalAoIntensity, palette.DirectIntensity);
        user0[52] = new(palette.EnvironmentMapTintIntensityLow, 0, 0, 0);
        // y is how far through the year the date is; x isn't recovered.
        user0[55] = user0.GetValueOrDefault(55) with { Y = AcnhEnvironmentTime.YearFraction(PreviewDate) };
        // The game scales the light distance by a whole-number factor whose setting isn't recovered; every capture shows 1.
        user0[66] = ParkUniforms.LightFade(stage.LightAttenuationDistance);
        user0[69] = new(palette.EnvironmentMapTintIntensityHigh, 0, 0, 0);
        // The moonlight rim light. Outdoors the game also raises [76].y as the camera turns to face the moon, which an
        // isolated preview has no moon for.
        user0[72] = palette.EdgeColor * palette.EdgeIntensity;
        user0[76] = new(1, stage.MoonLightEdgeModelParameter, palette.EdgeMoonParameter, stage.MoonLightIntensity);
        user0[77] = new(stage.MoonLightModelEdgeSqueeze, stage.MoonLightCloudIntensity, stage.MoonLightCloudEdgeParameter, stage.MoonLightSpecialCloudIntensity);
        user0[73] = palette.FieldGIOverrideColor;
        return user0;
    }

    // A building interior's PBC is laid 32 collision cells of 5 units past its own offset, so the model's origin is the stage's
    // (160, 0, 160), like the villager room's. The scene's light and capture files are in stage space.
    private static readonly Vector3 SceneOrigin = new(160, 0, 160);

    /// <summary>
    /// A scene's lights in the game's list order, points before spots. DynamicLightMgr adds its enabled scene lights; one bound
    /// to a model follows that model's bone instead, which the preview has no model for, so it is left out. IdrLightMgr makes
    /// every dual light from its first setting and switches it on, whatever the file's mIsEnable, and blends it towards the
    /// second by <paramref name="blend"/> each frame.
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
        // the game packs their falloff inverse as it comes out, infinite or negative.
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

    // Only the name's first character counts.
    private static bool IsBound(string model) => model.Length > 0 && model[0] != '\0';

    private static List<AcnhLocalLight> RoomLights(AcnhRoomLightParameters room, AcnhEnvironmentPalette palette)
    {
        // The chosen color's alpha is not radiance.
        static Vector3 Rgb(Vector4 color) => new(color.X, color.Y, color.Z);

        // The warm color is the light switch's on state.
        if (room.Radius <= room.Size) throw new InvalidDataException("Invalid room point-light falloff.");
        var lights = new List<AcnhLocalLight>
        {
            new(room.Position - SceneOrigin, Rgb(room.WarmColor) * palette.RoomPointIntensity, room.Size, room.Radius - room.Size)
        };

        var spot = palette.RoomSpot;
        if (spot.Intensity > 0)
        {
            if (spot.Far <= spot.Size || spot.Fovy <= 0) throw new InvalidDataException("Invalid room spotlight parameters.");
            var color = Rgb(spot.Color) * (spot.Intensity / (spot.Fovy / (2 * MathF.PI)));
            lights.Add(new(new Vector3(spot.Position, room.AlternateZ) - SceneOrigin, color, spot.Size, spot.Far - spot.Size)
            {
                Direction = spot.Direction,
                Cone = spot.Fovy * .5f
            });
        }
        return lights;
    }
}

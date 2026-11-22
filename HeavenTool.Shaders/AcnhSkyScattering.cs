using HeavenTool.IO.AcnhEnvironment;
using System.Numerics;

namespace HeavenTool.Shaders;

/// <summary>The game's five-sample atmospheric evaluation and its distinct SkyScatter LUT integral.</summary>
public static class AcnhSkyScattering
{
    private const float EarthRadius = 6360, OuterRadiusSquared = 42497360;
    private static readonly Vector3 Wavelength = new(4.676967f, 10.928214f, 26.680214f);

    /// <summary>Dimensionless native LUT value, before the material applies sun radiance and phase functions.</summary>
    public static Vector3 LookupIntegral(AcnhSkyScatterParameters sky, AcnhEnvironmentPalette palette, Vector3 skyDirection, Vector3 viewDirection) =>
        Integrate(sky, palette, skyDirection, viewDirection, true);

    private static Vector3 Integrate(AcnhSkyScatterParameters sky, AcnhEnvironmentPalette palette, Vector3 skyDirection, Vector3 viewDirection, bool lut)
    {
        if (!float.IsFinite(viewDirection.LengthSquared()) || viewDirection.LengthSquared() <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewDirection));
        var ray = Vector3.Normalize(viewDirection);
        var origin = new Vector3(0, EarthRadius + sky.VirtualCameraHeight, 0);
        float a = Vector3.Dot(ray, ray), b = 2 * Vector3.Dot(origin, ray);

        Vector3 Intersection(float radiusSquared, bool near)
        {
            float discriminant = b * b - 4 * (Vector3.Dot(origin, origin) - radiusSquared) * a;
            if (discriminant < .0001f) return default;
            float root = MathF.Sqrt(discriminant);
            float distance = (near ? -b - root : root - b) * .5f / a;
            return origin + ray * distance;
        }

        var end = Intersection(EarthRadius * EarthRadius, true);
        bool hitsEarth = end != Vector3.Zero && Vector3.Dot(ray, end - origin) > 0;
        if (!hitsEarth) end = Intersection(OuterRadiusSquared, false);
        var segment = end - origin;
        float stepLength = segment.Length() / 5;
        var direction = Vector3.Normalize(segment);
        float startHeight = origin.Length();
        float startDensity = MathF.Exp((EarthRadius - startHeight) * .025157232f);
        float startScale = Scale(1 - Vector3.Dot(origin, direction) / (hitsEarth ? -startHeight : startHeight));
        var point = origin + direction * (stepLength * .5f);
        var integral = Vector3.Zero;
        var rayleighExtinction = new Vector3(.0155f, .03f, .1f) * palette.RayleighIntensity * 12.566371f;
        var mieExtinction = new Vector3(palette.MieIntensity * .0015f * 12.566371f);
        // The executable's CPU helper scales both terms by wavelength. The native LUT scales only Rayleigh.
        var extinction = lut ? rayleighExtinction * Wavelength + mieExtinction : (rayleighExtinction + mieExtinction) * Wavelength;
        for (int i = 0; i < 5; i++)
        {
            float height = point.Length();
            float density = MathF.Exp((EarthRadius - height) * .025157232f);
            float lightScale = Scale(1 - Vector3.Dot(point, -skyDirection) / height);
            float cameraScale = Scale(1 - Vector3.Dot(direction, point) / (hitsEarth ? -height : height));
            float opticalDepth = startDensity * startScale * .25f + (lightScale * .25f - cameraScale * .25f) * density;
            var exponent = -extinction * opticalDepth;
            integral += new Vector3(MathF.Exp(exponent.X), MathF.Exp(exponent.Y), MathF.Exp(exponent.Z)) * (stepLength * .006289308f * density);
            point += direction * stepLength;
        }
        return integral * (hitsEarth ? sky.EarthColorIntensity : 1);
    }

    /// <summary>SceneEnvMgr's sun-facing scattered RGB.</summary>
    public static Vector3 SunScatterColor(AcnhSkyScatterParameters sky, AcnhEnvironmentPalette palette, Vector3 skyDirection)
    {
        var view = -Vector3.Normalize(skyDirection);
        var integral = Integrate(sky, palette, skyDirection, view, false);
        float cosine = Vector3.Dot(view, -skyDirection), squarePlusOne = cosine * cosine + 1;
        float rayleighPhase = squarePlusOne * .75f;
        float miePhase = squarePlusOne * .05038761f / MathF.Pow(MathF.Abs(cosine * -1.9f + 1.9024999f), 1.5f);
        var radiance = new Vector3(palette.SunColor.X, palette.SunColor.Y, palette.SunColor.Z) * palette.SkyLightIntensity;
        var rayleigh = integral * (new Vector3(.0155f, .03f, .1f) * palette.RayleighIntensity) * radiance * Wavelength;
        var mie = integral * (palette.MieIntensity * .0015f) * radiance;
        return rayleigh * rayleighPhase + mie * miePhase;
    }

    private static float Scale(float angle) => MathF.Exp((((angle * 5.25f - 6.8f) * angle + 3.83f) * angle + .459f) * angle - .00287f);
}

using HeavenTool.IO.FileFormats.BARS.MINF.Sections;

namespace HeavenTool.IO.Audio;

/// <summary>Associates known ACNH resource families with their sequence readers.</summary>
public static class AcnhPerformanceContext
{
    public static PerformanceSequenceFormat ForAsset(string assetName)
    {
        if (assetName.StartsWith("BGM_DJ_", StringComparison.Ordinal))
            return PerformanceSequenceFormat.AcnhDj;

        if (assetName.StartsWith("BGM_KappeiSong_", StringComparison.Ordinal))
            return PerformanceSequenceFormat.AcnhKappn;

        if (assetName.StartsWith("BGM_Audio_", StringComparison.Ordinal)
            || assetName.StartsWith("BGM_Live_", StringComparison.Ordinal)
            || assetName.StartsWith("BGM_Demo_", StringComparison.Ordinal)
            || assetName.StartsWith("BGM_OneRoom_", StringComparison.Ordinal)
            || assetName == "BGM_RadioExercise"
            || assetName.StartsWith("BGM_RadioExercise_", StringComparison.Ordinal))
            return PerformanceSequenceFormat.AcnhMusic;

        // If the asset name doesn't match any known patterns, return Unknown.
        return PerformanceSequenceFormat.Unknown;
    }
}
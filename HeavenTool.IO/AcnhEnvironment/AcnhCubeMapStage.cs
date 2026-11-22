#nullable enable
using AeonSake.NintendoTools.FileFormats.Byml;
using System.Numerics;
using static HeavenTool.IO.AcnhEnvironment.AcnhEnvironmentParameters;

namespace HeavenTool.IO.AcnhEnvironment;

/// <summary>
/// Where CubeMapMgr places a scene's environment capture. A scene's Param/Gfx/&lt;scene&gt;/CubeMapMgr_0.byml (CubeMapStageParam)
/// can pin it to mCapturePos. Otherwise an indoor stage captures at <see cref="IndoorHeight"/> over a point the game moves at
/// run time; in a house it is set from an actor's position, such as the player's.
/// </summary>
public static class AcnhCubeMapStage
{
    public const float IndoorHeight = 8;

    /// <summary>The scene's fixed capture position in its world space, or null when it doesn't pin one.</summary>
    public static Vector3? CapturePosition(Func<string, IBymlNode?> read, string scene)
    {
        if (read($"Param/Gfx/{scene}/CubeMapMgr_0.byml") is not BymlDictionary root || !root.TryGetValue(Hash("CubeMapStageParam"), out var node))
            return null;
        var p = Dictionary(node);
        // A field the file leaves out keeps its default, which doesn't pin the capture.
        if (!p.TryGetValue(Hash("mIsEnableOverrideCapturePos.bool"), out _) || !Flag(p, "mIsEnableOverrideCapturePos")) return null;
        return Position3(p, "mCapturePos");
    }
}

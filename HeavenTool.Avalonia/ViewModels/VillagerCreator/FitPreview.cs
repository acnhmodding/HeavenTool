using HeavenTool.IO.AcnhEnvironment;
using HeavenTool.IO.FileFormats.BFRES;
using HeavenTool.ModManager.Villagers;
using HeavenTool.Rendering.BFRES;
using HeavenTool.Shaders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace HeavenTool.Avalonia.ViewModels.VillagerCreator;

/// <summary><paramref name="Feel"/> is the face bone the emotion points are measured from, as shown.</summary>
public sealed record FitScene(BfresRenderer.PreparedScene Geometry, IReadOnlyDictionary<BfresMesh, BfresMeshShaders> Shaders,
    IReadOnlyList<string> Warnings, AcnhLightingPreset? Lighting, Matrix4x4? Feel);

public sealed record FitHeldItem(string Tool, string ModelPath, float Scale, string Size);

/// <summary>How the villager is shown: standing, on furniture whose ItemParam type is a chair (1 to 9 but 3) or stool (3), or in bed.</summary>
public enum FitStance { Standing, Chair, Stool, Bed }

public sealed class FitPreview(VillagerGameData data, Lazy<SwitchShaderArchive?> shaders)
{
    private const string HeadBone = "Head";
    private const string HandBone = "Hand_R";
    // The bone FUN_71015ffdc0 places emotion effects on.
    private const string FeelBone = "Feel";
    // Shown or hidden by a hat's ear setting (FUN_7101f15c30).
    private const string CapVisMaterial = "mCapVis";

    // CommonToolHand (CharaCommon.root.asb) and CommonToolHand20000 (CharaCommon20000.root.asb) for non-Human species, with
    // their flag switches (talking, sitting) at their default case.
    private static string HeldPoseName(string tool, string letter, string species) => tool switch
    {
        "Scoop" or "CliffMaker" or "RiverMaker" => "ToolScoop_APose",
        "Umbrella" => $"HandTool_Umbrella_APose_Npc{letter}",
        "Axe" => "ToolAxe_APose",
        "Net" => $"ToolNet_APose_Npc{letter}",
        "FishingRod" => letter == "S" ? "ToolPole_APose_NpcS" : "ToolPole_APose_NpcM",
        "Watering" => letter == "L" ? "ToolWateringCan_APose" : $"ToolWateringCan_APose_Npc{letter}",
        "Ladder" => "ToolLadder_APose",
        "GroundMaker" => "ToolLine_APose",
        "FenceMaker" => "ToolFence_APose",
        "ChangeStick" => "ToolStick_APose",
        "Book" or "SmallBook" => letter == "S" ? "Act_Book_APose_NpcS" : "Act_Book_APose_NpcM",
        "Coffee" or "Canister" or "Cop" or "Smoothie" or "PrTe" or "Cnut" or "GlDr" =>
            species == "kgr" && tool is not "PrTe" ? "Act_Drinking_APose_Talk" : "Act_Drinking_APose",
        "Ice" or "Candy" or "Sandwich" or "Doughnut" => $"Act_Eating_APose_Npc{letter}",
        "Broom" => "ToolBroomBamboo_APose_Npc",
        "BagHand" or "CoolBagHand" or "FruitBasket" or "CuteBagHand" or "LowBagHand" or "HighBagHand" =>
            letter == "S" ? "Act_Bag_APose_NpcS" : "Act_Bag_APose_NpcM",
        "SoapBubble" => "NpcPartial_SpendBubble",
        "Slingshot" => "HandTool_Apose",
        "BlowStick" => "HandTool_Blowouts_Apose",
        "Ocarina" => "Instrument_Ocarina_Apose",
        "Panpipe" => "Instrument_Panflute_Apose",
        "Tambourine" => species == "pbr" ? "Instrument_Tambourine_Apose_Npc_Pbr" : "Instrument_Tambourine_Apose_Npc",
        "JumpStick" => "ToolLongStick_APose",
        "RcmTourFlag" => "Rct_Flag_APose_10300",
        "Firewood" => $"Act_Firewood_APose_Npc{letter}",
        "PinataStick" => "Event_Birthday_PinataApose",
        "Dumbbell" or "LightDumbbell" => "Act_Workout_APose",
        "Duster" => "Act_Duster_APose",
        "HandLens" => "Act_Observe_APose",
        "SmartPhone" => "Etc_SmartPhone_Apose_Npc",
        "Sketchbook" => "Hgc_Sketchbook_APose",
        "Hammer" => "Etc_DiyCreating_Apose_Npc",
        "Sprayer" => species switch { "sza" => "HandTool_Apose", "pbr" or "brd" or "duk" => "HandTool_Apose_Npc_Spray", _ => "HandTool_Apose_Npc" },
        "Bouquet" => "Label_BK_APose",
        "SincerityTowel" => "ToolTowel_APose",
        "MChart" => "Act_MedicalRecord_APose",
        "Tr" => "Act_Tray_APose",
        "Fpan" => "Act_Fryingpan_APose",
        "Mcas" => "Act_Maracas_APose",
        "FAndK" => "Act_Forks_APose",
        "Trwl" => "Act_Trowel_APose",
        "DStk" => "Act_DishStack_APose",
        "Pitc" => "Act_Pitcher_APose",
        "SpnR" => "Act_Spanner_APose",
        "Pkax" => "Mol_Pickaxe_Apose",
        "Cane" => "TtlA_Cane_Apose",
        "Canvas" => "Wrl_Canvas_Apose",
        "EBa" => "HandTool_Handbag_Apose",
        // CommonToolHand20000's default.
        "Dh" => "HandTool_Apose",
        _ => "HandTool_Apose_Npc",
    };
    // The ASB's ArmR bone group, the only bones the held pose plays on.
    private static readonly IReadOnlySet<string> HeldPoseBones = new HashSet<string>
    {
        "Clavicle_R", "Arm_1_R", "Arm_1_R_Sub", "Arm_2_R", "Wrist_R_Sub", "Wrist_R", "Hand_R",
    };
    // CommonSitWait with NpcActivityAIEnable set (node 0x2da), where HasBackrestChair is false only for a stool, and
    // CommonBedWait, whose cases all play Bed_SleepWait. The head-look layer CommonSitWait adds is left off.
    private static string? StancePoseName(FitStance stance, string species) => stance switch
    {
        FitStance.Chair => species is "pgn" or "owl" or "ows" ? "Chair_SitdownWait_LegBackrest" : "Chair_SitdownWait_Backrest",
        FitStance.Stool => species is "pgn" or "owl" or "ows" ? "Chair_SitdownWait_LegStool" : "Chair_SitdownWait_Stool",
        FitStance.Bed => "Bed_SleepWait",
        _ => null,
    };
    // CommonToolHand switches these on FastLowerBodyWaitAnimType, whose value on furniture isn't known.
    private static readonly IReadOnlySet<string> LowerBodyTools = new HashSet<string> { "Watering", "Ladder" };
    private static readonly string[] PoseFiles = ["CharactorAnimation", "CharactorAnimation20000"];

    private readonly Lazy<IReadOnlyDictionary<string, BfresSkeletalAnimation>> poses = new(() => ReadPoses(data.Romfs));

    // Villager animations are retargeted from PlayerBody's skeleton.
    private readonly Lazy<IReadOnlyList<BfresBone>?> poseHost = new(() => Read(Path.Combine(data.Romfs, "Model", "PlayerBody.Nin_NX_NVN.zs"))?.Bones);

    private readonly RecentCache<string, BfresModel?> models = new(6);

    /// <summary>The model of the first item held as <paramref name="tool"/>, or null when there's none.</summary>
    public string? HeldModelPath(string tool) => data.ItemsHeldAs(tool).Select(data.ItemModelPath).FirstOrDefault(path => path != null);

    /// <summary>The preview scene, or null when cancelled. <paramref name="hat"/> applies the slot's ear setting.</summary>
    /// <paramref name="offset"/> is the Gmo's offset for <paramref name="stance"/>, which the game adds to the body's root
    /// (ACNH_NpcActorBody +0x2e4).
    public Task<FitScene?> BuildAsync(string modelName, string? item, GmoAccessory? fit, bool hat, FitHeldItem? held, FitStance stance,
        GmoVector offset, CancellationToken cancellation) =>
        Task.Run(() =>
        {
            try { return Build(modelName, item, fit, hat, held, stance, offset, cancellation); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return null; }
        });

    /// <summary>Where a slot puts a hat or glasses: scale, rotate X then Y then Z, translate, in the head bone's space.</summary>
    public static Matrix4x4 Placement(GmoAccessory fit, Matrix4x4 head) =>
        Matrix4x4.CreateScale(fit.Scale.X, fit.Scale.Y, fit.Scale.Z)
        * Matrix4x4.CreateRotationX(fit.Rotation.X) * Matrix4x4.CreateRotationY(fit.Rotation.Y) * Matrix4x4.CreateRotationZ(fit.Rotation.Z)
        * Matrix4x4.CreateTranslation(fit.Position.X, fit.Position.Y, fit.Position.Z)
        * head;

    private BfresSkeletalAnimation? HeldPose(string tool, string size, string modelName)
    {
        var letter = size switch { "cSmall" => "S", "cLarge" => "L", _ => "M" };
        return poses.Value.GetValueOrDefault(HeldPoseName(tool, letter, Species(modelName)));
    }

    // NpcNmlKgr00 is kgr.
    private static string Species(string modelName) =>
        modelName.StartsWith("NpcNml", StringComparison.Ordinal) && modelName.Length >= 9 ? modelName[6..9].ToLowerInvariant() : "";

    /// <summary>Where an emotion point is: an offset in the Feel bone's space, as FUN_71015ffdc0 turns it by the bone's rotation
    /// and adds it to its position.</summary>
    public static Vector3 EmotionPoint(GmoVector point, Matrix4x4 feel) => Vector3.Transform(new Vector3(point.X, point.Y, point.Z), feel);

    /// <summary>A held item on the hand bone with no offset (ACNH_ActorBindToParentBoneBody).</summary>
    public static Matrix4x4 HeldPlacement(FitHeldItem held, Matrix4x4 hand) => Matrix4x4.CreateScale(held.Scale) * hand;

    /// <summary>Whether a hat with <paramref name="earSet"/> keeps <c>mCapVis</c> shown. 1 and 3 also stop the ear physics, which the preview has none of.</summary>
    public static bool ShowsCapVis(int earSet) => earSet is not (2 or 3);

    private FitScene? Build(string modelName, string? item, GmoAccessory? fit, bool hat, FitHeldItem? held, FitStance stance, GmoVector offset,
        CancellationToken cancellation)
    {
        var build = models.Begin();
        var warnings = new List<string>();
        AcnhLightingPreset? lighting = null;
        try
        {
            lighting = AcnhSceneLighting.Evaluate(data.Environment, 12) with { PreviewIndirectLight = Vector3.Zero };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warnings.Add($"Couldn't load the game's noon lighting: {ex.Message}");
        }

        var villager = models.Get(Path.Combine(data.Romfs, "Model", modelName + ".Nin_NX_NVN.zs"), Read);
        if (villager == null) warnings.Add($"The villager's model ({modelName}) isn't in the romfs.");
        var worn = item == null ? null : models.Get(item, Read);
        if (item != null && worn == null) warnings.Add($"The item's model ({Path.GetFileName(item)}) couldn't be read.");
        var holding = held == null ? null : models.Get(held.ModelPath, Read);
        if (held != null && holding == null) warnings.Add($"The held item's model ({Path.GetFileName(held.ModelPath)}) couldn't be read.");
        if (cancellation.IsCancellationRequested) return null;

        if (villager != null && (held != null || stance != FitStance.Standing))
        {
            try
            {
                var layers = new List<BfresPoseLayer>();
                if (StancePoseName(stance, Species(modelName)) is { } bodyName)
                {
                    if (poses.Value.GetValueOrDefault(bodyName) is { } body) layers.Add(new(body, 0));
                    else warnings.Add($"The game's pose {bodyName} isn't in the romfs.");
                }
                if (held != null)
                {
                    if (HeldPose(held.Tool, held.Size, modelName) is not { } pose) warnings.Add($"The game has no pose for holding {held.Tool}.");
                    else layers.Add(new(pose, 0, HeldPoseBones));
                    if (stance != FitStance.Standing && LowerBodyTools.Contains(held.Tool))
                        warnings.Add($"Seated, {held.Tool} may be held differently in game.");
                }
                if (layers.Count > 0)
                {
                    if (poseHost.Value is { } host) villager = villager.Posed(layers, host);
                    else warnings.Add("The player's model (PlayerBody) isn't in the romfs.");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                warnings.Add($"Couldn't pose the villager: {ex.Message}");
            }
        }
        var hidesCapVis = worn != null && fit != null && hat && !ShowsCapVis(fit.EarSet);
        var meshes = new List<BfresMesh>(villager?.Meshes.Where(mesh => !hidesCapVis || mesh.Material.Name != CapVisMaterial) ?? []);
        if (worn != null && fit != null && villager != null)
        {
            if (villager.Bones.FirstOrDefault(bone => bone.Name == HeadBone) is { } head)
            {
                var place = Placement(fit, head.WorldTransform);
                meshes.AddRange(worn.Meshes.Select(mesh => mesh.Transformed(place)));
            }
            else warnings.Add($"The villager's model has no {HeadBone} bone to put the item on.");
        }
        if (holding != null && held != null && villager != null)
        {
            if (villager.Bones.FirstOrDefault(bone => bone.Name == HandBone) is { } hand)
            {
                var place = HeldPlacement(held, hand.WorldTransform);
                meshes.AddRange(holding.Meshes.Select(mesh => mesh.Transformed(place)));
            }
            else warnings.Add($"The villager's model has no {HandBone} bone to hold the item with.");
        }

        Matrix4x4? shownFeel = villager?.Bones.FirstOrDefault(bone => bone.Name == FeelBone)?.WorldTransform;
        if (stance != FitStance.Standing && offset != default)
        {
            var root = Matrix4x4.CreateTranslation(offset.X, offset.Y, offset.Z);
            for (int i = 0; i < meshes.Count; i++) meshes[i] = meshes[i].Transformed(root);
            shownFeel *= root;
        }

        var geometry = BfresRenderer.PrepareScene(meshes, cancellation);
        IReadOnlyDictionary<BfresMesh, BfresMeshShaders> programs = new Dictionary<BfresMesh, BfresMeshShaders>();
        try
        {
            if (shaders.Value is { } archive)
            {
                var prepared = BfresScene.PrepareShaders(meshes, archive, cancellation);
                programs = prepared.Programs;
                warnings.AddRange(prepared.Errors.Select(error => $"{error.Key.Name}: {error.Value}"));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warnings.Add($"Couldn't load the game materials: {ex.Message}");
        }
        models.Trim(build);
        return new FitScene(geometry, programs, warnings, lighting, shownFeel);
    }

    private static Dictionary<string, BfresSkeletalAnimation> ReadPoses(string romfs)
    {
        var poses = new Dictionary<string, BfresSkeletalAnimation>();
        foreach (var file in PoseFiles)
        {
            var path = Path.Combine(romfs, "Model", file + ".Nin_NX_NVN.zs");
            if (!File.Exists(path)) continue;
            using var stream = File.OpenRead(path);
            foreach (var pose in BfresFileReader.Load(stream).SelectMany(asset => asset.Document.SkeletalAnimations)) poses.TryAdd(pose.Name, pose);
        }
        return poses;
    }

    private static BfresModel? Read(string path)
    {
        if (!File.Exists(path)) return null;
        using var stream = File.OpenRead(path);
        return BfresFileReader.Load(stream).SelectMany(asset => asset.Document.Models).FirstOrDefault();
    }
}

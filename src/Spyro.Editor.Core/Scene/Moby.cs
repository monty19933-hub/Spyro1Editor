using Spyro.Editor.Core.Primitives;
using System.Text.Json.Serialization;

namespace Spyro.Editor.Core.Scene;

public sealed class Moby
{
    public const string FlyInLandingControlKind = "fly-in-landing";

    public int Index { get; init; }
    public int TrueIndex { get; init; } = -1;
    public int LegacyIndex { get; init; } = -1;
    public Vector3f Position { get; set; }
    public Vector3f OriginalPosition { get; init; }
    public int Type { get; set; }
    public int OriginalType { get; init; } = -1;
    public int State { get; set; }
    public int OriginalState { get; init; } = -1;
    public int YawByte { get; set; } = -1;
    public int OriginalYawByte { get; init; } = -1;
    public uint RuntimeAddress { get; init; }
    /// <summary>
    /// Native Moby <c>m_Props</c> pointer stored at record offset +0x00.
    /// Runtime captures contain a patched RAM address; source caches contain a
    /// scene-relative offset.
    /// </summary>
    public uint PropertiesPointer { get; init; }
    /// <summary>
    /// Legacy editor field read from record offset +0x08. This is retained for
    /// project/cache compatibility; it is not the native <c>m_Props</c> pointer.
    /// </summary>
    public uint SpecialDataPointer { get; init; }
    public int SourceByte36 { get; set; }
    public int OriginalSourceByte36 { get; init; } = -1;
    public int SourceByte37 { get; set; }
    public int OriginalSourceByte37 { get; init; } = -1;
    public int SourceByte4F { get; set; }
    public int OriginalSourceByte4F { get; init; } = -1;
    public int Flag4A { get; set; }
    public int OriginalFlag4A { get; init; } = -1;
    public int Flag4B { get; set; }
    public int OriginalFlag4B { get; init; } = -1;
    public ColorRgba Color { get; set; }
    [JsonIgnore]
    public ColorRgba? OriginalColor { get; set; }
    public string Label { get; set; } = "";
    public string OriginalLabel { get; set; } = "";
    public string PatchStatus { get; set; } = "";
    public string PatchLead { get; set; } = "";
    [JsonIgnore]
    public string OriginalPatchStatus { get; set; } = "";
    [JsonIgnore]
    public string OriginalPatchLead { get; set; } = "";
    public string CrossLevelTemplateId { get; set; } = "";
    public string CrossLevelFamily { get; set; } = "";
    public string CrossLevelSourceLevelKey { get; set; } = "";
    public string CrossLevelSourceLevelName { get; set; } = "";
    public int CrossLevelSourceTrueIndex { get; set; } = -1;
    public string CrossLevelRequiredExporterFeature { get; set; } = "";
    public string CrossLevelRecipeId { get; set; } = "";
    /// <summary>
    /// Stable identity for an imported special-chest root and every hidden
    /// companion/reward row that must travel with it as one editor operation.
    /// Empty on native retail objects and projects created before this field.
    /// </summary>
    public string SpecialChestBundleId { get; set; } = "";
    public string SpecialChestProfileId { get; set; } = "";
    public int SpecialChestVisibleRootTrueIndex { get; set; } = -1;
    public List<int> SpecialChestHiddenCompanionTrueIndexes { get; } = [];
    public int SpecialChestCapacity { get; set; }
    public string SpecialChestEvidenceStatus { get; set; } = "";
    public string SourceCloneLevelKey { get; set; } = "";
    public string SourceCloneLevelName { get; set; } = "";
    public int SourceCloneTrueIndex { get; set; } = -1;
    public string CandidateKind { get; set; } = "";
    public string Confidence { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string BehaviorNote { get; set; } = "";
    public string ZoneLabel { get; set; } = "";
    [JsonIgnore]
    public string OriginalCandidateKind { get; set; } = "";
    [JsonIgnore]
    public string OriginalConfidence { get; set; } = "";
    [JsonIgnore]
    public string OriginalEvidence { get; set; } = "";
    [JsonIgnore]
    public string OriginalBehaviorNote { get; set; } = "";
    [JsonIgnore]
    public string OriginalZoneLabel { get; set; } = "";
    public string EditorControlKind { get; init; } = "";
    public List<MobyLink> Links { get; } = new();
    [JsonIgnore]
    public List<MobyLink> DormantGemRelationshipLinks { get; } = new();
    public bool HasLoadedNativeEdit { get; set; }
    public string LoadedNativeEditSummary { get; set; } = "";
    public bool IsAdded { get; set; }
    public bool IsRemoved { get; set; }

    public bool IsEditorControl => !string.IsNullOrWhiteSpace(EditorControlKind);
    public bool IsFlyInLandingControl => string.Equals(
        EditorControlKind,
        FlyInLandingControlKind,
        StringComparison.OrdinalIgnoreCase);
    public string DisplayIndex => IsFlyInLandingControl
        ? "ENTRY"
        : TrueIndex >= 0
            ? $"T{TrueIndex}"
            : "NEW";
    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? $"0x{Type:X2}" : Label;
    public double YawDegrees => YawByte >= 0 ? YawByteToDegrees(YawByte) : 0;
    public string TechnicalSummary =>
        $"T{TrueIndex} / L{LegacyIndex}, native class 0x{SourceByte37:X2}{SourceByte36:X2}, " +
        $"render radius (+0x50) 0x{Type:X2}, was drawn (+0x51) 0x{State:X2}, " +
        $"yaw {(YawByte >= 0 ? $"{YawDegrees:0.#} deg" : "unknown")}, " +
        $"update distance (+0x52) 0x{Flag4A:X2}, drop Moby/class (+0x53) 0x{Flag4B:X2}, " +
        $"specular/metal type (+0x4F) 0x{SourceByte4F:X2}";
    public MobyVisualKind VisualKind
    {
        get
        {
            string displayText = DisplayLabel.ToLowerInvariant();
            string labelAndKindText = $"{DisplayLabel} {CandidateKind}".ToLowerInvariant();
            string text = $"{DisplayLabel} {CandidateKind} {BehaviorNote}".ToLowerInvariant();
            if (displayText == "whirlwind")
                return MobyVisualKind.Whirlwind;
            if (displayText.Contains("controller") ||
                displayText.Contains("helper") ||
                displayText.StartsWith("gem spawner", StringComparison.Ordinal) ||
                ContainsNonvisualControlText(displayText, Type))
                return MobyVisualKind.Control;
            if (displayText.Contains("butterfly"))
                return MobyVisualKind.Actor;
            if (IsChest)
                return MobyVisualKind.Chest;
            if (IsKey)
                return MobyVisualKind.Key;
            if (IsGemLike)
                return MobyVisualKind.Gem;
            if (Type == 0x18 && ContainsGemObjectText(text))
                return MobyVisualKind.Gem;
            if (ContainsFlightTargetText(text, Type))
                return MobyVisualKind.FlightTarget;
            if (ContainsNonvisualControlText(labelAndKindText, Type))
                return MobyVisualKind.Control;
            if (displayText.Contains("chicken cage"))
                return MobyVisualKind.Scenery;
            if (ContainsActorText(displayText))
                return MobyVisualKind.Actor;
            if (ContainsNonvisualControlText(text, Type))
                return MobyVisualKind.Control;
            if (text.Contains("dragon-eating plant") || text.Contains("dragon eating plant"))
                return MobyVisualKind.Actor;
            if (text.Contains("fairy cage") || text.Contains("cage prop") || text.Contains("cage scenery"))
                return MobyVisualKind.Scenery;
            if (ContainsNamedSceneryIconText(text))
                return MobyVisualKind.Scenery;
            if (ContainsNamedActorIconText(text))
                return MobyVisualKind.Actor;
            if (text.Contains("dragon egg", StringComparison.Ordinal))
                return MobyVisualKind.Actor;
            if (text.Contains("dragon"))
                return MobyVisualKind.Dragon;
            if (text.Contains("whirlwind"))
                return MobyVisualKind.Whirlwind;
            if (text.Contains("portal") || text.Contains("return-home"))
                return MobyVisualKind.Portal;
            if (ContainsExplicitSceneryText(text))
                return MobyVisualKind.Scenery;
            if (ContainsActorText(text))
                return MobyVisualKind.Actor;
            if (text.Contains("tree") || text.Contains("lamp") || text.Contains("torch") || text.Contains("flag") || text.Contains("flower") || text.Contains("grass") || text.Contains("scenery") || text.Contains("prop") || text.Contains("arch") || text.Contains("lighthouse") || text.Contains("boat") || text.Contains("train") || text.Contains("barrel") || text.Contains("balloon") || text.Contains("cannon"))
                return MobyVisualKind.Scenery;
            if (text.Contains("camera") || text.Contains("helper") || text.Contains("system") || text.Contains("nonvisual") || text.Contains("control") || Type is 0x0A or 0x33 or 0x52)
                return MobyVisualKind.Control;
            return MobyVisualKind.Unknown;
        }
    }

    private static bool ContainsNamedActorIconText(string text) =>
        text.Contains("banana boy")
        || text.Contains("strongarm")
        || text.Contains("strong arm")
        || text.Contains("winged fool")
        || text.Contains("winged guy")
        || text.Contains("winged guys")
        || text.Contains("armored fool")
        || text.Contains("armored guy")
        || text.Contains("armored guys")
        || text.Contains("lamp fool")
        || text.Contains("lamp guy")
        || text.Contains("devil cupid")
        || text.Contains("puppy")
        || text.Contains("devil puppy")
        || text.Contains("devil dog")
        || text.Contains("mutant turtle")
        || text.Contains("turtle")
        || text.Contains("mushroom");

    private static bool ContainsActorText(string text) =>
        text.Contains("enemy") ||
        text.Contains("gnorc") ||
        text.Contains("kamikaze") ||
        text.Contains("kamikazi") ||
        text.Contains("druid") ||
        text.Contains("wizard") ||
        text.Contains("ram") ||
        text.Contains("shepherd") ||
        text.Contains("thief") ||
        text.Contains("fodder") ||
        text.Contains("sheep") ||
        text.Contains("chicken") ||
        text.Contains("dog") ||
        text.Contains("bird") ||
        text.Contains("fairy") ||
        text.Contains("grenadier") ||
        text.Contains("shocker") ||
        text.Contains("toasty") ||
        text.Contains("copter") ||
        text.Contains("airplane") ||
        text.Contains("balloonist") ||
        text.Contains("baloonist") ||
        text.Contains("fat lady") ||
        text.Contains("fat momma");

    public bool HasPositionEdit => DistanceSquared(Position, OriginalPosition) > 0.0001f;
    public bool HasRotationEdit => OriginalYawByte >= 0 && YawByte >= 0 && YawByte != OriginalYawByte;
    public bool HasMetadataEdit => OriginalType >= 0 && Type != OriginalType
        || OriginalState >= 0 && State != OriginalState
        || HasRotationEdit
        || OriginalSourceByte36 >= 0 && SourceByte36 != OriginalSourceByte36
        || OriginalSourceByte37 >= 0 && SourceByte37 != OriginalSourceByte37
        || OriginalSourceByte4F >= 0 && SourceByte4F != OriginalSourceByte4F
        || OriginalFlag4A >= 0 && Flag4A != OriginalFlag4A
        || OriginalFlag4B >= 0 && Flag4B != OriginalFlag4B
        || SourceCloneTrueIndex >= 0
        || !string.IsNullOrWhiteSpace(CrossLevelTemplateId)
        || !string.IsNullOrWhiteSpace(SpecialChestBundleId)
        || !string.Equals(Label, OriginalLabel, StringComparison.Ordinal);
    public bool HasAnyEdit => IsAdded || IsRemoved || HasPositionEdit || HasMetadataEdit || HasLoadedNativeEdit;

    public void SetType(int type)
    {
        Type = Math.Clamp(type, 0, 255);
        Color = ColorForType(Type);
    }

    public void UndoEdits()
    {
        Position = OriginalPosition;
        if (OriginalType >= 0)
            SetType(OriginalType);
        if (OriginalState >= 0)
            State = OriginalState;
        if (OriginalYawByte >= 0)
            YawByte = OriginalYawByte;
        if (OriginalSourceByte36 >= 0)
            SourceByte36 = OriginalSourceByte36;
        if (OriginalSourceByte37 >= 0)
            SourceByte37 = OriginalSourceByte37;
        if (OriginalSourceByte4F >= 0)
            SourceByte4F = OriginalSourceByte4F;
        if (OriginalFlag4A >= 0)
            Flag4A = OriginalFlag4A;
        if (OriginalFlag4B >= 0)
            Flag4B = OriginalFlag4B;
        Label = OriginalLabel;
        Color = OriginalColor ?? (Gem != GemValue.Unknown ? Gem.Color : ColorForType(Type));
        PatchStatus = OriginalPatchStatus;
        PatchLead = OriginalPatchLead;
        CandidateKind = OriginalCandidateKind;
        Confidence = OriginalConfidence;
        Evidence = OriginalEvidence;
        BehaviorNote = OriginalBehaviorNote;
        ZoneLabel = OriginalZoneLabel;
        CrossLevelTemplateId = "";
        CrossLevelFamily = "";
        CrossLevelSourceLevelKey = "";
        CrossLevelSourceLevelName = "";
        CrossLevelSourceTrueIndex = -1;
        CrossLevelRequiredExporterFeature = "";
        CrossLevelRecipeId = "";
        SpecialChestBundleId = "";
        SpecialChestProfileId = "";
        SpecialChestVisibleRootTrueIndex = -1;
        SpecialChestHiddenCompanionTrueIndexes.Clear();
        SpecialChestCapacity = 0;
        SpecialChestEvidenceStatus = "";
        SourceCloneLevelKey = "";
        SourceCloneLevelName = "";
        SourceCloneTrueIndex = -1;
        HasLoadedNativeEdit = false;
        LoadedNativeEditSummary = "";
        IsRemoved = false;
    }

    public bool IsChest => ContainsLabelOrKind("chest") && !IsChestContent;
    public bool IsChestContent => Type == 0x00 && Flag4A == 0xFF && GemValue.TryFromIdByte(Flag4B, out _)
        || ContainsLabelOrKind("chest content", "contained gem", "contained red", "contained green", "contained blue", "contained yellow", "contained purple");
    public bool IsLockedChestShell => !IsChestContent &&
        ((SourceByte36 == 0xAE && Type is 0x18 or 0x20) ||
            ContainsLabelOrKind("key chest", "locked chest", "unlock chest", "locked/unlock chest"));
    public bool HasNativeKeyFingerprint =>
        SourceByte36 == 0xAD &&
        SourceByte37 == 0x00 &&
        SourceByte4F == 0x02 &&
        Flag4B == 0xFF &&
        (Type == 0x18 && Flag4A == 0x40 ||
            Type == 0x00 && Flag4A == 0x00);
    public bool IsKey => !IsChest && !IsChestContent &&
        (HasNativeKeyFingerprint || ContainsLabelOrKind("key collectible"));
    public bool IsHomeworldPortalControl =>
        Type == 0x00 &&
        (SourceByte36 is 0x01 or 0x8E ||
            SourceByte36 == 0x1E && Links.Any(link =>
                link.Kind.Equals("portal controls", StringComparison.OrdinalIgnoreCase))) &&
        Flag4A == 0x10 &&
        Flag4B == 0xFF;
    public bool SupportsTerrainSnap =>
        VisualKind != MobyVisualKind.Control || IsHomeworldPortalControl;
    public bool IsVisibleGem => !IsKey &&
        Type == 0x18 &&
        GemValue.TryFromEncoding(SourceByte36, SourceByte4F, out _);
    public bool IsGemLike => !IsKey && (IsChestContent || IsVisibleGem);
    public GemValue RewardGem => GemValue.TryFromIdByte(Flag4B, out GemValue gem) ? gem : GemValue.Unknown;
    public int TreasureValue => IsGemLike ? Gem.Value : RewardGem.Value;

    public GemValue Gem
    {
        get
        {
            if (IsChestContent && GemValue.TryFromIdByte(Flag4B, out GemValue containedGem))
                return containedGem;

            if (IsVisibleGem &&
                GemValue.TryFromEncoding(SourceByte36, SourceByte4F, out GemValue visibleGem))
                return visibleGem;

            return GemValue.Unknown;
        }
    }

    public void ApplyGem(GemValue gem)
    {
        if (gem == GemValue.Unknown)
            return;

        if (IsChestContent)
        {
            Flag4B = gem.IdByte;
        }
        else if (IsVisibleGem)
        {
            SourceByte36 = gem.IdByte;
            SourceByte4F = gem.ValueByte;
            if (Type == 0x18 && IsAdded)
            {
                Flag4A = 0x40;
                Flag4B = 0xFF;
            }
        }
        else
        {
            throw new InvalidOperationException($"{DisplayLabel} does not have the paired native loose-gem encoding required for a safe gem edit.");
        }

        Label = SuggestedGemLabel(gem);
        Color = gem.Color;
    }

    public string SuggestedGemLabel(GemValue gem) => SuggestedGemLabel(gem, Label);

    public string SuggestedGemLabel(GemValue gem, string? currentLabel)
    {
        string sourceLabel = currentLabel ?? "";
        string label = sourceLabel.Trim();
        if (gem == GemValue.Unknown)
            return sourceLabel;

        if (string.IsNullOrWhiteSpace(label))
            return IsChestContent
                ? $"Locked chest content: {gem.DisplayName}"
                : gem.DisplayName;

        return TryRewriteGeneratedGemLabel(label, gem, rewardCarrier: false, out string rewritten)
            ? rewritten
            : sourceLabel;
    }

    public string SuggestedRewardGemLabel(GemValue gem) => SuggestedRewardGemLabel(gem, Label);

    public string SuggestedRewardGemLabel(GemValue gem, string? currentLabel)
    {
        string sourceLabel = currentLabel ?? "";
        string label = sourceLabel.Trim();
        if (gem == GemValue.Unknown)
            return sourceLabel;

        if (string.IsNullOrWhiteSpace(label))
            return $"{GemColorName(gem)} reward";

        return TryRewriteGeneratedGemLabel(label, gem, rewardCarrier: true, out string rewritten)
            ? rewritten
            : sourceLabel;
    }

    public static bool IsGeneratedGemLabel(string? label, bool rewardCarrier)
    {
        string candidate = label?.Trim() ?? "";
        return string.IsNullOrWhiteSpace(candidate) ||
            TryRewriteGeneratedGemLabel(candidate, GemValue.Red, rewardCarrier, out _);
    }

    private static bool TryRewriteGeneratedGemLabel(
        string label,
        GemValue replacement,
        bool rewardCarrier,
        out string rewritten)
    {
        return rewardCarrier
            ? TryRewriteGeneratedRewardLabel(label, replacement, out rewritten)
            : TryRewriteGeneratedDirectGemLabel(label, replacement, out rewritten);
    }

    private static bool TryRewriteGeneratedDirectGemLabel(
        string label,
        GemValue replacement,
        out string rewritten)
    {
        foreach (GemValue known in GemValue.Known)
        {
            if (label.Equals(known.Name, StringComparison.OrdinalIgnoreCase) ||
                label.Equals(known.DisplayName, StringComparison.OrdinalIgnoreCase))
            {
                rewritten = replacement.DisplayName;
                return true;
            }

            const string liveTestedSuffix = " (live-tested)";
            if (label.Equals($"{known.Name}{liveTestedSuffix}", StringComparison.OrdinalIgnoreCase) ||
                label.Equals($"{known.DisplayName}{liveTestedSuffix}", StringComparison.OrdinalIgnoreCase))
            {
                rewritten = $"{replacement.DisplayName}{liveTestedSuffix}";
                return true;
            }

            if (label.Equals($"New {known.Name}", StringComparison.OrdinalIgnoreCase))
            {
                rewritten = $"New {replacement.Name}";
                return true;
            }

            if (label.Equals($"New {known.DisplayName}", StringComparison.OrdinalIgnoreCase))
            {
                rewritten = $"New {replacement.DisplayName}";
                return true;
            }

            if (label.Equals($"Copy of {known.Name}", StringComparison.OrdinalIgnoreCase) ||
                label.Equals($"Copy of {known.DisplayName}", StringComparison.OrdinalIgnoreCase))
            {
                rewritten = $"Copy of {replacement.DisplayName}";
                return true;
            }

            if (TryRewriteGemRoleLabel(
                    label,
                    "Locked chest content: ",
                    known,
                    replacement,
                    out rewritten) ||
                TryRewriteGemRoleLabel(
                    label,
                    "Toasty sheep-form contained ",
                    known,
                    replacement,
                    out rewritten) ||
                TryRewriteGemRoleLabel(
                    label,
                    "Contained ",
                    known,
                    replacement,
                    out rewritten))
            {
                return true;
            }
        }

        rewritten = label;
        return false;
    }

    private static bool TryRewriteGemRoleLabel(
        string label,
        string rolePrefix,
        GemValue known,
        GemValue replacement,
        out string rewritten)
    {
        if (label.Equals($"{rolePrefix}{known.Name}", StringComparison.OrdinalIgnoreCase) ||
            label.Equals($"{rolePrefix}{known.DisplayName}", StringComparison.OrdinalIgnoreCase))
        {
            rewritten = $"{rolePrefix}{replacement.DisplayName}";
            return true;
        }

        const string liveTestedSuffix = " (live-tested)";
        if (label.Equals($"{rolePrefix}{known.Name}{liveTestedSuffix}", StringComparison.OrdinalIgnoreCase) ||
            label.Equals($"{rolePrefix}{known.DisplayName}{liveTestedSuffix}", StringComparison.OrdinalIgnoreCase))
        {
            rewritten = $"{rolePrefix}{replacement.DisplayName}{liveTestedSuffix}";
            return true;
        }

        rewritten = label;
        return false;
    }

    private static bool TryRewriteGeneratedRewardLabel(
        string label,
        GemValue replacement,
        out string rewritten)
    {
        string replacementColor = GemColorName(replacement);
        foreach (GemValue known in GemValue.Known)
        {
            string knownColor = GemColorName(known);
            string reward = $"{knownColor} reward";
            if (label.Equals(reward, StringComparison.OrdinalIgnoreCase))
            {
                rewritten = $"{replacementColor} reward";
                return true;
            }

            string rewardSuffix = $" ({reward})";
            if (label.EndsWith(rewardSuffix, StringComparison.OrdinalIgnoreCase))
            {
                rewritten = $"{label[..^rewardSuffix.Length]} ({replacementColor} reward)";
                return true;
            }

            string phaseRewardMarker = $"phase-reward {known.Name} marker";
            int markerIndex = label.IndexOf(phaseRewardMarker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0 && markerIndex + phaseRewardMarker.Length == label.Length)
            {
                rewritten = $"{label[..markerIndex]}phase-reward {replacement.Name} marker";
                return true;
            }

            foreach (string noun in new[] { "Gem", "Gems" })
            {
                string thiefRewardPrefix = $"Treasure Thief {knownColor} {noun}";
                if (label.StartsWith(thiefRewardPrefix, StringComparison.OrdinalIgnoreCase) &&
                    (label.Length == thiefRewardPrefix.Length || char.IsWhiteSpace(label[thiefRewardPrefix.Length])))
                {
                    rewritten = $"Treasure Thief {replacementColor} {noun}{label[thiefRewardPrefix.Length..]}";
                    return true;
                }
            }
        }

        rewritten = label;
        return false;
    }

    private static string GemColorName(GemValue gem) =>
        gem.Name.EndsWith(" gem", StringComparison.OrdinalIgnoreCase)
            ? gem.Name[..^" gem".Length]
            : gem.Name;

    public static double YawByteToDegrees(int yawByte)
    {
        int byteValue = yawByte & 0xFF;
        int steps = (256 - byteValue) & 0xFF;
        return steps * (360.0 / 256.0);
    }

    public static int DegreesToYawByte(double degrees)
    {
        double normalized = degrees % 360.0;
        if (normalized < 0)
            normalized += 360.0;

        return (int)Math.Round((360.0 - normalized) * 256.0 / 360.0) & 0xFF;
    }

    public static (short Cos, short Sin) YawByteToMatrix(int yawByte)
    {
        double radians = YawByteToDegrees(yawByte) * Math.PI / 180.0;
        return (
            ClampToInt16(Math.Round(Math.Cos(radians) * 4096.0)),
            ClampToInt16(Math.Round(Math.Sin(radians) * 4096.0)));
    }

    public static byte[] YawByteToMatrixBytes(int yawByte)
    {
        (short cos, short sin) = YawByteToMatrix(yawByte);
        short negSin = ClampToInt16(-sin);
        byte[] bytes = new byte[18];
        WriteInt16LittleEndian(bytes, 0x00, cos);
        WriteInt16LittleEndian(bytes, 0x02, 0);
        WriteInt16LittleEndian(bytes, 0x04, sin);
        WriteInt16LittleEndian(bytes, 0x06, 0);
        WriteInt16LittleEndian(bytes, 0x08, 4096);
        WriteInt16LittleEndian(bytes, 0x0A, 0);
        WriteInt16LittleEndian(bytes, 0x0C, negSin);
        WriteInt16LittleEndian(bytes, 0x0E, 0);
        WriteInt16LittleEndian(bytes, 0x10, cos);
        return bytes;
    }

    public static bool TryMatrixToYawByte(short cos, short sin, out int yawByte)
    {
        if (cos == 0 && sin == 0)
        {
            yawByte = -1;
            return false;
        }

        double degrees = Math.Atan2(sin, cos) * 180.0 / Math.PI;
        yawByte = DegreesToYawByte(degrees);
        return true;
    }

    private static short ClampToInt16(double value) => (short)Math.Clamp(value, short.MinValue, short.MaxValue);

    private static void WriteInt16LittleEndian(byte[] bytes, int offset, short value)
    {
        bytes[offset] = (byte)(value & 0xFF);
        bytes[offset + 1] = (byte)((value >> 8) & 0xFF);
    }

    public static string FallbackLabel(int type)
    {
        return type switch
        {
            0x20 => "Actor/prop object?",
            0x30 => "Large scenery/object?",
            0x18 => "Gem/object?",
            0x33 => "Helper/system",
            0x0A or 0x52 => "Helper/system",
            _ => $"0x{type:X2}"
        };
    }

    public static ColorRgba ColorForType(int type)
    {
        return type switch
        {
            0x20 => ColorRgba.FromRgb(244, 212, 77),
            0x30 => ColorRgba.FromRgb(183, 140, 255),
            0x18 => ColorRgba.FromRgb(255, 140, 90),
            0x0A or 0x33 or 0x52 => ColorRgba.FromRgb(143, 166, 184),
            _ => ColorRgba.FromRgb(93, 173, 226)
        };
    }

    private static float DistanceSquared(Vector3f a, Vector3f b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    private bool ContainsAny(params string[] needles)
    {
        string text = $"{DisplayLabel} {CandidateKind} {BehaviorNote}";
        return needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private bool ContainsLabelOrKind(params string[] needles)
    {
        string text = $"{DisplayLabel} {CandidateKind}";
        return needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsGemObjectText(string text)
    {
        return text.Contains("red gem", StringComparison.Ordinal) ||
            text.Contains("green gem", StringComparison.Ordinal) ||
            text.Contains("blue gem", StringComparison.Ordinal) ||
            text.Contains("yellow gem", StringComparison.Ordinal) ||
            text.Contains("purple gem", StringComparison.Ordinal) ||
            text.Contains("gem collectible", StringComparison.Ordinal);
    }

    private static bool ContainsNonvisualControlText(string text, int type)
    {
        if (type is 0x0A or 0x33 or 0x52)
            return true;

        return text.Contains("nonvisual", StringComparison.Ordinal) ||
            text.Contains("sound emitter", StringComparison.Ordinal) ||
            text.Contains("placeholder", StringComparison.Ordinal) ||
            text.Contains("trigger", StringComparison.Ordinal) ||
            text.Contains("cutscene", StringComparison.Ordinal) ||
            text.Contains("camera", StringComparison.Ordinal) ||
            text.Contains("control link", StringComparison.Ordinal) ||
            text.Contains("control data", StringComparison.Ordinal) ||
            text.Contains("linked record", StringComparison.Ordinal) ||
            text.Contains("linked data", StringComparison.Ordinal) ||
            text.Contains("control marker", StringComparison.Ordinal) ||
            text.Contains("reward link", StringComparison.Ordinal) ||
            text.Contains("reward-linked", StringComparison.Ordinal) ||
            text.Contains("reward control", StringComparison.Ordinal) ||
            text.Contains("route marker", StringComparison.Ordinal) ||
            text.Contains("route/control", StringComparison.Ordinal) ||
            text.Contains("route control", StringComparison.Ordinal) ||
            text.Contains("scene control", StringComparison.Ordinal) ||
            text.Contains("scene link", StringComparison.Ordinal) ||
            text.Contains("scene/route control", StringComparison.Ordinal) ||
            text.Contains("support/control", StringComparison.Ordinal) ||
            text.Contains("system anchor", StringComparison.Ordinal) ||
            text.Contains("support marker", StringComparison.Ordinal) ||
            text.Contains("helper marker", StringComparison.Ordinal) ||
            (type == 0x00 && text.Contains("marker", StringComparison.Ordinal));
    }

    private static bool ContainsExplicitSceneryText(string text)
    {
        if (text.Contains("actor/prop", StringComparison.Ordinal))
            return false;

        return text.Contains("scenery", StringComparison.Ordinal) ||
            text.Contains(" prop", StringComparison.Ordinal) ||
            text.Contains("route prop", StringComparison.Ordinal) ||
            text.Contains("structural", StringComparison.Ordinal);
    }

    private static bool ContainsNamedSceneryIconText(string text) =>
        text.Contains("skinny tree", StringComparison.Ordinal) ||
        text.Contains("wide tree", StringComparison.Ordinal) ||
        text.Contains("tall 2-ball tree", StringComparison.Ordinal) ||
        text.Contains("tall two-ball tree", StringComparison.Ordinal) ||
        text.Contains("platform scenery", StringComparison.Ordinal) ||
        text.Contains("platform prop", StringComparison.Ordinal) ||
        text.Contains("dragon-platform scenery", StringComparison.Ordinal) ||
        text.Contains("dragon platform scenery", StringComparison.Ordinal) ||
        text.Contains("power pole", StringComparison.Ordinal) ||
        text.Contains("power pylon", StringComparison.Ordinal);

    private static bool ContainsFlightTargetText(string text, int type)
    {
        return text.Contains("flight target", StringComparison.Ordinal) ||
            text.Contains("flight record", StringComparison.Ordinal) ||
            text.Contains("flight timer", StringComparison.Ordinal) ||
            text.Contains("timer number", StringComparison.Ordinal) ||
            text.Contains("flight course", StringComparison.Ordinal) ||
            text.Contains("plane", StringComparison.Ordinal) ||
            text.Contains("aircraft", StringComparison.Ordinal) ||
            text.Contains("airplane", StringComparison.Ordinal) ||
            text.Contains("copter target", StringComparison.Ordinal) ||
            text.Contains("train piece", StringComparison.Ordinal) ||
            text.Contains("lighthouse", StringComparison.Ordinal) ||
            text.Contains("boat target", StringComparison.Ordinal) ||
            text.Contains("ring target", StringComparison.Ordinal) ||
            text.Contains("flight arch", StringComparison.Ordinal) ||
            (type == 0x50 && (text.Contains("arch", StringComparison.Ordinal) ||
                text.Contains("boat", StringComparison.Ordinal) ||
                text.Contains("ring", StringComparison.Ordinal)));
    }
}

public enum MobyVisualKind
{
    Unknown,
    Gem,
    Key,
    Chest,
    Dragon,
    Actor,
    FlightTarget,
    Portal,
    Scenery,
    Whirlwind,
    Control
}

public readonly record struct GemValue(string Name, int Value, int IdByte, int ValueByte, ColorRgba Color)
{
    public static GemValue Unknown { get; } = new("Unknown gem", 0, -1, -1, ColorRgba.FromRgb(180, 180, 180));
    public static GemValue Red { get; } = new("Red gem", 1, 0x53, 0x01, ColorRgba.FromRgb(232, 25, 28));
    public static GemValue Green { get; } = new("Green gem", 2, 0x54, 0x02, ColorRgba.FromRgb(0, 190, 72));
    public static GemValue Blue { get; } = new("Blue gem", 5, 0x55, 0x03, ColorRgba.FromRgb(24, 78, 232));
    public static GemValue Yellow { get; } = new("Yellow gem", 10, 0x56, 0x04, ColorRgba.FromRgb(226, 211, 42));
    public static GemValue Purple { get; } = new("Purple gem", 25, 0x57, 0x05, ColorRgba.FromRgb(176, 70, 230));

    public string DisplayName => Value > 0 ? $"{Name} ({Value})" : Name;

    public static IReadOnlyList<GemValue> Known { get; } = new[] { Red, Green, Blue, Yellow, Purple };

    public static bool TryFromIdByte(int value, out GemValue gem)
    {
        gem = Known.FirstOrDefault(item => item.IdByte == value);
        if (gem != default)
            return true;

        gem = Unknown;
        return false;
    }

    public static bool TryFromValueByte(int value, out GemValue gem)
    {
        gem = Known.FirstOrDefault(item => item.ValueByte == value);
        if (gem != default)
            return true;

        gem = Unknown;
        return false;
    }

    public static bool TryFromEncoding(int idByte, int valueByte, out GemValue gem)
    {
        if (TryFromIdByte(idByte, out GemValue idGem) && idGem.ValueByte == valueByte)
        {
            gem = idGem;
            return true;
        }

        gem = Unknown;
        return false;
    }

    public override string ToString()
    {
        return DisplayName;
    }
}

public sealed class MobyLink
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public bool LinkedMove { get; init; }
    public string Confidence { get; init; } = "";
    public string Reason { get; init; } = "";
    public IReadOnlyList<int> TrueIndexes { get; init; } = Array.Empty<int>();

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Kind : Name;
}

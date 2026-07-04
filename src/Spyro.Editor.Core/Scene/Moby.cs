using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Scene;

public sealed class Moby
{
    public int Index { get; init; }
    public int TrueIndex { get; init; } = -1;
    public int LegacyIndex { get; init; } = -1;
    public Vector3f Position { get; set; }
    public Vector3f OriginalPosition { get; init; }
    public int Type { get; set; }
    public int OriginalType { get; init; } = -1;
    public int State { get; set; }
    public int OriginalState { get; init; } = -1;
    public uint RuntimeAddress { get; init; }
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
    public string Label { get; set; } = "";
    public string OriginalLabel { get; set; } = "";
    public string PatchStatus { get; set; } = "";
    public string PatchLead { get; set; } = "";
    public string CrossLevelTemplateId { get; set; } = "";
    public string CrossLevelFamily { get; set; } = "";
    public string CrossLevelSourceLevelKey { get; set; } = "";
    public string CrossLevelSourceLevelName { get; set; } = "";
    public int CrossLevelSourceTrueIndex { get; set; } = -1;
    public string CrossLevelRequiredExporterFeature { get; set; } = "";
    public string CandidateKind { get; set; } = "";
    public string Confidence { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string BehaviorNote { get; set; } = "";
    public string ZoneLabel { get; set; } = "";
    public List<MobyLink> Links { get; } = new();
    public bool HasLoadedNativeEdit { get; set; }
    public string LoadedNativeEditSummary { get; set; } = "";
    public bool IsAdded { get; set; }
    public bool IsRemoved { get; set; }

    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? $"0x{Type:X2}" : Label;
    public string TechnicalSummary =>
        $"T{TrueIndex} / L{LegacyIndex}, type 0x{Type:X2}, state 0x{State:X2}, " +
        $"bytes 36/4A/4B/4F = 0x{SourceByte36:X2}/0x{Flag4A:X2}/0x{Flag4B:X2}/0x{SourceByte4F:X2}";
    public MobyVisualKind VisualKind
    {
        get
        {
            string displayText = DisplayLabel.ToLowerInvariant();
            string text = $"{DisplayLabel} {CandidateKind} {BehaviorNote}".ToLowerInvariant();
            if (displayText == "whirlwind")
                return MobyVisualKind.Whirlwind;
            if (IsChest)
                return MobyVisualKind.Chest;
            if (IsKey)
                return MobyVisualKind.Key;
            if (IsGemLike)
                return MobyVisualKind.Gem;
            if (ContainsNonvisualControlText(text, Type))
                return MobyVisualKind.Control;
            if (Type == 0x18 && ContainsGemObjectText(text))
                return MobyVisualKind.Gem;
            if (text.Contains("dragon-eating plant") || text.Contains("dragon eating plant"))
                return MobyVisualKind.Actor;
            if (text.Contains("fairy cage") || text.Contains("cage prop") || text.Contains("cage scenery"))
                return MobyVisualKind.Scenery;
            if (ContainsNamedSceneryIconText(text))
                return MobyVisualKind.Scenery;
            if (ContainsNamedActorIconText(text))
                return MobyVisualKind.Actor;
            if (text.Contains("dragon"))
                return MobyVisualKind.Dragon;
            if (ContainsFlightTargetText(text, Type))
                return MobyVisualKind.FlightTarget;
            if (text.Contains("whirlwind"))
                return MobyVisualKind.Whirlwind;
            if (text.Contains("portal") || text.Contains("return-home"))
                return MobyVisualKind.Portal;
            if (ContainsExplicitSceneryText(text))
                return MobyVisualKind.Scenery;
            if (text.Contains("enemy") || text.Contains("gnorc") || text.Contains("kamikaze") || text.Contains("kamikazi") || text.Contains("druid") || text.Contains("wizard") || text.Contains("ram") || text.Contains("shepherd") || text.Contains("thief") || text.Contains("fodder") || text.Contains("sheep") || text.Contains("chicken") || text.Contains("dog") || text.Contains("bird") || text.Contains("fairy") || text.Contains("grenadier") || text.Contains("shocker") || text.Contains("toasty") || text.Contains("copter") || text.Contains("airplane") || text.Contains("balloonist") || text.Contains("baloonist") || text.Contains("fat lady") || text.Contains("fat momma"))
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

    public bool HasPositionEdit => DistanceSquared(Position, OriginalPosition) > 0.0001f;
    public bool HasMetadataEdit => OriginalType >= 0 && Type != OriginalType
        || OriginalState >= 0 && State != OriginalState
        || OriginalSourceByte36 >= 0 && SourceByte36 != OriginalSourceByte36
        || OriginalSourceByte37 >= 0 && SourceByte37 != OriginalSourceByte37
        || OriginalSourceByte4F >= 0 && SourceByte4F != OriginalSourceByte4F
        || OriginalFlag4A >= 0 && Flag4A != OriginalFlag4A
        || OriginalFlag4B >= 0 && Flag4B != OriginalFlag4B
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
        CrossLevelTemplateId = "";
        CrossLevelFamily = "";
        CrossLevelSourceLevelKey = "";
        CrossLevelSourceLevelName = "";
        CrossLevelSourceTrueIndex = -1;
        CrossLevelRequiredExporterFeature = "";
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
    public bool IsKey => !IsChest && !IsChestContent &&
        (Type == 0x18 && SourceByte36 == 0xAD ||
            ContainsLabelOrKind("key collectible"));
    public bool IsVisibleGem => !IsKey &&
        (Type == 0x18 && (GemValue.TryFromIdByte(SourceByte36, out _) || GemValue.TryFromValueByte(SourceByte4F, out _)) ||
            IsAdded && ContainsLabelOrKind("gem") && GemValue.TryFromIdByte(SourceByte36, out _));
    public bool IsGemLike => !IsKey && (IsChestContent || IsVisibleGem);
    public GemValue RewardGem => GemValue.TryFromIdByte(Flag4B, out GemValue gem) ? gem : GemValue.Unknown;
    public int TreasureValue => IsGemLike ? Gem.Value : RewardGem.Value;

    public GemValue Gem
    {
        get
        {
            if (IsChestContent && GemValue.TryFromIdByte(Flag4B, out GemValue containedGem))
                return containedGem;

            if ((IsVisibleGem || Type == 0x18 && !IsKey) &&
                GemValue.TryFromIdByte(SourceByte36, out GemValue visibleGem))
                return visibleGem;

            return Type == 0x18 && !IsKey && GemValue.TryFromValueByte(SourceByte4F, out GemValue valueGem)
                ? valueGem
                : GemValue.Unknown;
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
        else
        {
            SourceByte36 = gem.IdByte;
            SourceByte4F = gem.ValueByte;
            if (Type == 0x18 && IsAdded)
            {
                Flag4A = 0x40;
                Flag4B = 0xFF;
            }
        }

        Label = IsChestContent
            ? $"Locked chest content: {gem.DisplayName}"
            : gem.DisplayName;
        Color = gem.Color;
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
            text.Contains("control link", StringComparison.Ordinal) ||
            text.Contains("linked record", StringComparison.Ordinal) ||
            text.Contains("control marker", StringComparison.Ordinal) ||
            text.Contains("route control", StringComparison.Ordinal) ||
            text.Contains("scene control", StringComparison.Ordinal) ||
            text.Contains("scene/route control", StringComparison.Ordinal) ||
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
        text.Contains("dragon platform scenery", StringComparison.Ordinal);

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

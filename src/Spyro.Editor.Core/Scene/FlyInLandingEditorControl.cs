using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Scene;

public static class FlyInLandingEditorControl
{
    public static bool SupportsLevel(LevelDefinition level) =>
        level.LevelId >= 0 && level.LevelId % 10 != 0;

    public static double HeadingByteToDegrees(int headingByte) =>
        (headingByte & 0xFF) * (360.0 / 256.0);

    public static int DegreesToHeadingByte(double degrees)
    {
        double normalized = degrees % 360.0;
        if (normalized < 0)
            normalized += 360.0;

        return (int)Math.Round(normalized * 256.0 / 360.0) & 0xFF;
    }

    public static Vector2f HeadingByteToWorldDirection(int headingByte)
    {
        // Level entry flight consumes its heading as native cos/sin, unlike mirrored moby model yaw.
        double radians = HeadingByteToDegrees(headingByte) * Math.PI / 180.0;
        return new Vector2f((float)Math.Cos(radians), (float)Math.Sin(radians));
    }

    public static Moby Create(LevelDefinition level, FlyInLandingData landing)
    {
        Vector3f position = new(landing.RawX / 16f, landing.RawY / 16f, landing.RawZ / 16f);
        return new Moby
        {
            Index = -1,
            TrueIndex = -1,
            LegacyIndex = -1,
            Position = position,
            OriginalPosition = position,
            Type = 0x00,
            OriginalType = 0x00,
            State = 0,
            OriginalState = 0,
            YawByte = landing.YawByte,
            OriginalYawByte = landing.YawByte,
            SourceByte36 = 0,
            OriginalSourceByte36 = 0,
            SourceByte37 = 0,
            OriginalSourceByte37 = 0,
            SourceByte4F = 0,
            OriginalSourceByte4F = 0,
            Flag4A = 0,
            OriginalFlag4A = 0,
            Flag4B = 0,
            OriginalFlag4B = 0,
            Color = ColorRgba.FromRgb(46, 191, 196),
            Label = "Fly-in Landing",
            OriginalLabel = "Fly-in Landing",
            CandidateKind = "portal level entry destination",
            Confidence = "source-proven",
            Evidence = "Exact homeworld-to-level landing XYZ decoded from the destination level entry block.",
            BehaviorNote = "Moving this marker changes where Spyro finishes the level fly-in. Return-home travel is separate and is not changed.",
            PatchStatus = "source-entry-patchable",
            PatchLead = $"WAD entry {level.SourceWadEntry}, destination-level entry XYZ at 0x{landing.WadOffset:X}.",
            EditorControlKind = Moby.FlyInLandingControlKind
        };
    }
}

namespace Spyro.Editor.Core.Generation;

/// <summary>How far Spyro moves, measured in the emulator (USA, 2026-10-12) in a level built by the editor: editor units,
/// per 60 Hz frame. The generator builds every gap, ledge and ramp inside these limits with a margin, and its
/// reachability check uses the same numbers.</summary>
public static class SpyroMoves
{
    /// <summary>Running speed (reached about 20 frames after starting) and charging speed on flat ground.</summary>
    public const float RunSpeed = 4.48f, ChargeSpeed = 7.66f;
    /// <summary>How high a jump rises when X is held (16 frames or more); a tap rises <see cref="TapJumpHeight"/>. A charge
    /// jump always rises the tap height.</summary>
    public const float JumpHeight = 81, TapJumpHeight = 30;
    /// <summary>A running held jump reaches its top this far forward (32 frames) and lands 185 further on flat ground.</summary>
    public const float JumpApexForward = 100, JumpFlatDistance = 185;
    /// <summary>A steady glide moves 6.27 forward and sinks 1.88 per frame: 3.3 forward for each unit down. A held jump
    /// and a glide from its top cross 391 units of flat ground.</summary>
    public const float GlideRatio = 3.3f, GlideFlatDistance = 391;
    /// <summary>The tallest ledge a standing held jump next to it clears (80 does not).</summary>
    public const float MaxLedge = 64;
    /// <summary>Walking climbs a ramp of up to 20° easily (25° slowly, 30° not at all); charging climbs up to about 30°.
    /// Walking cannot climb a vertical step at all, however small: walking floors join through ramps.</summary>
    public const float MaxWalkSlopeDegrees = 22, MaxChargeSlopeDegrees = 31;
    /// <summary>Spyro stands this far above the floor, and stops this far from a wall.</summary>
    public const float StandHeight = 22, Radius = 22;
    /// <summary>The headroom Spyro needs over a floor to stand and jump (a held jump's top plus his height).</summary>
    public const float Headroom = 130;

    /// <summary>Design limits: what the generator asks of the player, kept well inside the measured limits.</summary>
    public const float DesignLedge = 56, DesignJumpGap = 120, DesignRampRise = 36, DesignChargeRise = 64;

    /// <summary>The height of a held jump and glide <paramref name="forward"/> units out from where Spyro jumped (relative to
    /// the floor he jumped from), with <paramref name="margin"/> (0-1) taken off the distance for safety.</summary>
    public static float GlideHeight(float forward, float margin = 0.15f)
    {
        float d = forward / (1 - margin);
        if (d <= JumpApexForward) return JumpHeight * d / JumpApexForward;
        return JumpHeight - (d - JumpApexForward) / GlideRatio;
    }

    /// <summary>How far a held jump and glide carries Spyro to a floor <paramref name="drop"/> units below his take-off
    /// (negative: above it), with <paramref name="margin"/> taken off; 0 when it cannot reach that height.</summary>
    public static float GlideReach(float drop, float margin = 0.15f)
    {
        if (-drop > JumpHeight - 8) return 0;
        return (JumpApexForward + (JumpHeight + drop) * GlideRatio) * (1 - margin);
    }
}

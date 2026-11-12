using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Generation;

/// <summary>How two areas are joined, for the report and the in-game route checks: path or ramp (walked both ways),
/// bridge, stones (a chain of jumps), overlook (the higher area stands over the lower: down only), glide (from a high
/// place to a ledge too high to jump onto). <see cref="From"/>/<see cref="To"/> are where the move starts and ends.</summary>
public sealed record LevelLink(int A, int B, string Kind, Vector3f From, Vector3f To)
{
    /// <summary>Where the floor ends on the way from <see cref="From"/> (editor units along the line): where a jump starts.</summary>
    public float? JumpAt { get; init; }
}

/// <summary>What the level may have besides its areas and paths.</summary>
/// <param name="Rivers">Rivers across big rooms, with bridges.</param>
/// <param name="Houses">Houses round the rooms (a village or two).</param>
/// <param name="Walls">Castle walls: along rooms' edges, battlements on a castle's rock walls.</param>
internal sealed record FeatureSet(bool Caves, bool ChargePaths, bool Glides, bool Hazards, bool Bridges, bool Secrets, bool Rivers = false, bool Houses = false, bool Walls = false);

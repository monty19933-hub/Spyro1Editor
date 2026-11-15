using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Generation;

/// <summary>A part of one of the game's levels chosen to be cut out whole: the floor inside an outline drawn round it
/// (in the level's own coordinates) with every wall, tower and roof standing on it, between two heights. <see cref="Swap"/>:
/// pairs of textures, one a built level cannot carry (a frame of the game's moving pictures) and the still picture of
/// the same level most like it, shown in its place. <see cref="Fill"/>: the piece stands where the level has no ground
/// beside it (at a cliff's edge, on an island in the sky), so a band inside the outline is laid with floor wherever the
/// level has none at ground height or below: without it the pockets between the outline and the foot of the piece's
/// walls would be holes in the new level's ground.</summary>
internal sealed record CutDefinition(string Level, string Name, double[] Outline, double ZLow = double.MinValue, double ZHigh = double.MaxValue, int[]? Swap = null, bool Fill = false);

/// <summary>A part cut from one of the game's levels: its faces as the level has them, about the middle of its floor's
/// outer edge (x, y) and that edge's mean height (z), and the edge itself.</summary>
internal sealed class LevelCut
{
    public required string Name { get; init; }
    public required string Level { get; init; }
    public required IReadOnlyList<SourceFace> Faces { get; init; }
    /// <summary>The floor's outer edge, counter-clockwise seen from above: corners of the floor's own faces.</summary>
    public required Vector3f[] Rim { get; init; }
    /// <summary>How far the edge reaches from the middle.</summary>
    public double Radius { get; init; }
}

/// <summary>A cut stood in a new level: at a point, turned, its edge's mean height at <see cref="Z"/>.</summary>
internal sealed class PlacedCut
{
    public required LevelCut Cut { get; init; }
    public double X, Y, Z, Angle;
    private (double X, double Y, double Z, double Angle) _at = (double.NaN, 0, 0, 0);
    private Vector3f[] _rim = [];
    private (double X, double Y)[] _outline = [];

    /// <summary>A point of the cut where the new level has it; rounded as <see cref="LevelProps.Place"/> rounds the
    /// cut's faces, so the edge lies exactly on their corners.</summary>
    public Vector3f At(Vector3f v)
    {
        double c = SpyroPlanner.Cos(Angle), s = SpyroPlanner.Sin(Angle);
        return new((float)Math.Round(X + v.X * c - v.Y * s), (float)Math.Round(Y + v.X * s + v.Y * c), (float)Math.Round(Z - 0 + v.Z));
    }

    /// <summary>The floor's outer edge where the new level has it, counter-clockwise.</summary>
    public Vector3f[] Rim
    {
        get
        {
            if (_at != (X, Y, Z, Angle))
            {
                _rim = [.. Cut.Rim.Select(At)];
                _outline = [.. _rim.Select(v => ((double)v.X, (double)v.Y))];
                _at = (X, Y, Z, Angle);
            }
            return _rim;
        }
    }

    /// <summary>Signed distance from the edge (negative inside).</summary>
    public double Sdf(double x, double y)
    {
        _ = Rim;
        return TerrainHull.Distance(_outline, x, y);
    }

    /// <summary>The edge's height near a point: the edge's corners weighted by nearness.</summary>
    public double RimHeight(double x, double y)
    {
        double sum = 0, weights = 0;
        foreach (var v in Rim)
        {
            double d2 = (v.X - x) * (v.X - x) + (v.Y - y) * (v.Y - y) + 400;
            double w = 1 / (d2 * d2);
            sum += w * v.Z; weights += w;
        }
        return weights <= 0 ? Z : sum / weights;
    }

    /// <summary>The cut's faces where the new level has them.</summary>
    public IEnumerable<TerrainPiece> Faces() =>
        LevelProps.Place(new LevelProp(Cut.Faces, Cut.Radius, 0, Cut.Faces.Select(f => f.TextureId).ToHashSet()), X, Y, Z, Angle, 0);
}

/// <summary>Parts of the game's own levels, cut out whole and stood in new levels (the walled yards, keeps and hills
/// that make a level of the game what it is), read from the user's own disc (the editor's level cache). A new level
/// takes its cuts from the level whose look it has, so every texture is that level's. The generator gives a cut a room
/// of its own, sews the room's floor to the cut's floor edge all round (<see cref="TerrainMesher"/>) and leaves the
/// cut's inside as the game has it.</summary>
internal static class LevelCuts
{
    /// <summary>How far past a cut's floor edge the room's own floor stops: the seam between is made of faces joining
    /// the two edges corner to corner.</summary>
    public const double Margin = 110;
    /// <summary>Open floor all round a cut, beyond the seam, to walk round it to its ways in.</summary>
    public const double Walk = 160;
    /// <summary>How far in from its outline a cut that asks for it is laid with floor where the level has none (the
    /// pockets at the foot of walls are within it; further in, under a building, floor would only be hidden faces).</summary>
    public static readonly double FillDepth = double.TryParse(Environment.GetEnvironmentVariable("SPYRO_CUT_FILL_DEPTH"), out double depth) ? depth : 200;
    /// <summary>The smallest patch of such floor worth a face (square units).</summary>
    public static readonly double FillLeast = double.TryParse(Environment.GetEnvironmentVariable("SPYRO_CUT_FILL_LEAST"), out double least) ? least : 150;

    /// <summary>The cuts, outlined by hand on the game's levels and each looked at in pictures (2026-11-14; the study
    /// tools, every cut's pictures and the ones tried and left out: outputs/LevelCuts-20261114/README.md). An outline
    /// runs over ground-level floor all the way round (a level's ground is sewn to it at the height of the cut's floor
    /// there), holds no liquid, and is no bigger than a room can be (radius to about 1,250). Best of each level first.</summary>
    internal static readonly CutDefinition[] Table =
    [
        // Artisans: the double ring of hedges with its little gate spires and the round lawn inside (the old "round
        // hill", re-centred: its outline ran along the hedge tops)
        new("artisans", "the hedge maze", [4997, 4101, 4952, 4327, 4824, 4518, 4633, 4646, 4407, 4691, 4181, 4646, 3990, 4518, 3814, 4330, 3814, 4101, 3814, 3872, 3990, 3684, 4181, 3556, 4382, 3512, 4604, 3598, 4826, 3685, 4952, 3875]),
        // Artisans: the four six-sided stone stumps of the meadow, a stair of tops 45 to 160 high.
        new("artisans", "the stepping stones", [6500, 2620, 6456, 2785, 6335, 2906, 6170, 2950, 6005, 2906, 5884, 2785, 5840, 2620, 5884, 2455, 6005, 2334, 6170, 2290, 6335, 2334, 6456, 2455]),
        // Artisans: the low eight-sided stone dais of the meadow (a dragon's stand)
        new("artisans", "the pedestal", [4920, 3100, 4888, 3220, 4800, 3308, 4680, 3340, 4560, 3308, 4472, 3220, 4440, 3100, 4472, 2980, 4560, 2892, 4680, 2860, 4800, 2892, 4888, 2980]),
        // (Left out: "the castle bowl", an ellipse round (8280, 3170), 980 by 1230. Its moat is moving water, its outline ran
        // up the hills round it (a step of 466 in its edge) and the towers in it are joined to the castle by a high bridge.)
        // Stone Hill: the keep in its walled yard, its corner towers and its gates, the lower field before the south
        // gate as a forecourt. (Purple scrollwork bands 22, 23 are moving pictures: the plain purple stone 18, 17
        // with the same cream edges; 6 is a moving copy of the grass-topped bank 3. South-east corner cut off: it
        // held half a tower of the walled field.)
        new("stonehill", "the keep", [7570, 8900, 8764, 8900, 9320, 9330, 9320, 10580, 9001, 10580, 7570, 10580, 7570, 9310], Swap: [22, 18, 23, 17, 6, 3]),
        // Stone Hill: the squat round tower of the east field with its parapet, side turret and door porch, in its
        // grass hollow (scrollwork band 23 -> plain purple stone 17)
        new("stonehill", "the round tower", [12645, 9370, 12603, 9552, 12487, 9698, 12318, 9779, 12132, 9779, 11963, 9698, 11847, 9552, 11805, 9370, 11847, 9188, 11963, 9042, 12132, 8961, 12318, 8961, 12487, 9042, 12603, 9188], Swap: [23, 17]),
        // (Left out: "the walled field", [6880, 8300, 7000, 7600, 7400, 7180, 8200, 7060, 8900, 7180, 9400, 7600, 9560, 8300, 9420, 8980, 9020, 9260,
        // 7400, 9260, 7020, 9020], 1000, Swap: [22, 18, 23, 17, 6, 3]. It is 2,940 units across and 1,040 faces: it asks for a room of
        // 1,740 where rooms run to 1,150, and it cannot be made smaller: its north side is the keep's own wall.)
        // Dark Hollow: the round stone platform with the two grass-topped pillars that step up to it.
        new("darkhollow", "the stepping pillars", [1379, 3498, 1407, 3202, 1424, 3115, 1473, 3041, 1547, 2992, 1903, 2872, 2050, 2843, 2197, 2872, 2322, 2956, 2406, 3081, 2435, 3228, 2430, 3265, 2406, 3375, 2322, 3500, 2197, 3584, 2071, 3617, 1693, 3708, 1606, 3725, 1519, 3708, 1445, 3659, 1396, 3585]),
        // Dark Hollow: the two tall spired towers with the blue stairs between them; the rim is the lower court on
        // the west, the upper on the east (moving scrollwork 2 -> its still copy 26)
        new("darkhollow", "the gate towers", [3880, 2842, 4325, 2842, 4325, 3562, 3880, 3562], Swap: [2, 26]),
        // Dark Hollow: the round floor of twelve purple scrollwork tiles round a sand-coloured middle (moving
        // scrollwork 2 -> its still copy 26)
        new("darkhollow", "the scrollwork ring", [2566, 4020, 2568, 4150, 2568, 4284, 2553, 4314, 2500, 4401, 2473, 4439, 2386, 4549, 2256, 4549, 2135, 4499, 2042, 4406, 1992, 4285, 1992, 4155, 2042, 4034, 2135, 3941, 2256, 3891, 2386, 3891, 2507, 3941], Swap: [2, 26]),
        // Town Square: the west square's round paving with its tiered fountain (the three moving water jets 1 -> pale
        // stone 65; the small basin keeps its still water)
        new("townsquare", "the fountain square", [7125, 8650, 7110, 8900, 6975, 9020, 6800, 9088, 6600, 9088, 6440, 8990, 6335, 8800, 6310, 8540, 6400, 8380, 6480, 8270, 6950, 8205, 7090, 8380], ZLow: 500, Swap: [1, 65]),
        // Town Square: the paved court at the south end with broad grass steps curling up its east side; the rim is
        // low on the west, high at the top lawn.
        new("townsquare", "the terraced court", [7515, 7190, 7555, 6590, 7600, 5975, 7735, 5905, 7990, 5900, 8185, 5985, 8375, 6232, 8408, 6525, 8410, 6860, 8408, 7190, 8110, 7193, 7810, 7193], ZLow: 500, Fill: true),
        // Peace Keepers: two small brick towers with spiked roofs joined by an archway, on a low mound of sand.
        new("peacekeepers", "the gate towers", [3190, 5880, 3157, 5997, 3066, 6091, 2933, 6143, 2787, 6143, 2654, 6091, 2563, 5997, 2530, 5880, 2563, 5763, 2654, 5669, 2787, 5617, 2933, 5617, 3066, 5669, 3157, 5763]),
        // Peace Keepers: the top of the tar pit's island: a brick watchtower and a second twin-tower gate (cut inside
        // the island's edge: no tar)
        new("peacekeepers", "the guard post", [3095, 8130, 3100, 8250, 3050, 8350, 2905, 8450, 2720, 8560, 2595, 8595, 2320, 8525, 2130, 8340, 2052, 8190, 2085, 8040, 2150, 7962, 3080, 8115], ZLow: 3300),
        // Peace Keepers: the natural rock arch, walked under.
        new("peacekeepers", "the rock arch", [4545, 6170, 4512, 6313, 4421, 6428, 4288, 6492, 4142, 6492, 4009, 6428, 3918, 6313, 3885, 6170, 3918, 6027, 4009, 5912, 4142, 5848, 4288, 5848, 4421, 5912, 4512, 6027]),
        // Peace Keepers: the great rock ring with the twin-tower gate on its middle platform and two rock needles at
        // its foot; way in through the north gap (its tar pool 26 -> cracked earth 17: tar would be walked on)
        new("peacekeepers", "the crater", [4250, 6990, 4198, 7250, 4052, 7472, 3800, 7640, 3070, 7461, 2933, 7254, 2880, 6990, 2933, 6726, 3000, 6500, 3080, 6245, 3330, 6270, 3570, 6300, 3834, 6353, 4058, 6502, 4207, 6726], ZLow: 3305, Swap: [26, 17], Fill: true),
        // Dry Canyon: the brick hall with skylights in its flat roof, two spiked hut-towers, a doorway and the rock
        // terraces that climb to the roof.
        new("drycanyon", "the fort", [7670, 5700, 7420, 6030, 7100, 6200, 6800, 6215, 6648, 6183, 6549, 6101, 6401, 5972, 6334, 5763, 6315, 5576, 6361, 5350, 6600, 4990, 6900, 4850, 7150, 4880, 7410, 5070, 7655, 5440], ZLow: 850, Fill: true),
        // Dry Canyon: the small square brick hut with the spiked pyramid roof.
        new("drycanyon", "the spiked hut", [9310, 8140, 9284, 8233, 9212, 8308, 9108, 8350, 8992, 8350, 8888, 8308, 8816, 8233, 8790, 8140, 8816, 8047, 8888, 7972, 8992, 7930, 9108, 7930, 9212, 7972, 9284, 8047]),
        // Dry Canyon: six great stone blocks stepping down from a high ledge to the canyon floor between two spiked
        // huts; the rim is the ledge on one side, the floor on the other (tar beside the lower steps 24 -> rock 27)
        new("drycanyon", "the giant steps", [9294, 5167, 9055, 5095, 8935, 5000, 8880, 4770, 8935, 4600, 9330, 4468, 9530, 4610, 9475, 4840, 9412, 5047], ZHigh: 1302, Swap: [24, 27]),
        // Dry Canyon: grand: the bridge from a strip of cliff top to the round battlemented bastion and the slab on
        // to the six-sided hut-tower (tar pools 24 -> rock 27)
        new("drycanyon", "the round bastion", [11074, 6935, 11074, 7065, 10596, 7666, 10390, 7826, 10143, 7881, 9896, 7826, 9608, 7744, 9320, 7658, 8750, 7483, 8745, 7305, 8750, 7127, 9045, 7028, 9350, 6931, 9657, 6834, 9964, 6739, 10194, 6713, 10900, 6808, 10990, 6835], Swap: [24, 27], Fill: true),
        // Cliff Town: the oval adobe keep on its rock: leaning walls with a spiked crown, arched doorway, a small
        // tower at each tip.
        new("clifftown", "the oval keep", [8214, 2688, 8322, 2551, 8556, 2302, 8700, 2204, 8870, 2132, 9056, 2092, 9248, 2092, 9434, 2132, 9604, 2206, 9748, 2297, 10008, 2542, 10090, 2688, 10008, 2834, 9866, 3000, 9748, 3112, 9606, 3202, 9340, 3300, 8960, 3300, 8670, 3140, 8524, 3048, 8420, 2936, 8322, 2825], ZLow: 940),
        // Cliff Town: the row of joined flat-roofed adobe houses with the pyramid-roofed tower at its end.
        new("clifftown", "the long house", [8960, 4240, 9500, 4234, 10070, 4240, 10160, 4345, 10152, 4540, 10052, 4806, 9960, 5020, 9460, 5026, 8960, 5020, 8952, 4830, 8952, 4430], ZLow: 940),
        // Cliff Town: three flat-roofed adobe houses of the north town on flat ground.
        new("clifftown", "the adobe houses", [8600, 6500, 8930, 6270, 9720, 6270, 9900, 6450, 9900, 7450, 9820, 7570, 8750, 7570, 8580, 7350]),
        // Cliff Town: the squat round adobe tower with its skirted base (one step of 94 in the rim where a ledge
        // ends)
        new("clifftown", "the round tower", [7130, 5450, 7087, 5610, 6970, 5727, 6810, 5770, 6650, 5727, 6533, 5610, 6490, 5450, 6533, 5290, 6650, 5173, 6810, 5130, 6970, 5173, 7087, 5290]),
        // Ice Cavern: the tapering eight-sided stone tower on its ice floor, open on three sides under a lintel
        // (ZHigh drops the cave wall over it)
        new("icecavern", "the eight-sided tower", [4100, 2315, 4390, 2272, 4570, 2480, 4640, 2640, 4590, 2790, 4480, 2900, 4290, 2945, 4060, 2930, 3900, 2760, 3870, 2560, 3960, 2400], ZHigh: 1870),
        // Ice Cavern: the round ice platform with five ice battlement blocks and the ice column in its middle (cut
        // off where it met the cave roof)
        new("icecavern", "the ice bastion", [6960, 4847, 6914, 4725, 6916, 4600, 6958, 4470, 7052, 4376, 7178, 4327, 7310, 4327, 7435, 4378, 7528, 4470, 7580, 4585, 7632, 4780, 7590, 4930, 7480, 5037, 7220, 4942], ZLow: 1100, ZHigh: 1500, Fill: true),
        // Ice Cavern: a square stone pillar with a pyramid cap and eight ice spikes on its patch of ice.
        new("icecavern", "the spiked pillar", [9310, 3400, 9272, 3540, 9170, 3642, 9030, 3680, 8890, 3642, 8788, 3540, 8750, 3400, 8788, 3260, 8890, 3158, 9030, 3120, 9170, 3158, 9272, 3260], ZLow: 1200, ZHigh: 1600),
        // Magic Crafters: the round marble tower with the pink dome and its doorway landing (it stands in a pool: the
        // ring of pool floor 52 left inside the outline -> white marble 28)
        new("magiccrafters", "the domed tower", [6883, 2124, 6927, 2021, 7016, 1955, 7126, 1941, 7228, 1985, 7294, 2072, 7316, 2210, 7279, 2297, 7204, 2355, 7066, 2366, 6963, 2322, 6897, 2234], Swap: [52, 28]),
        // Magic Crafters: a marble portal arch on its lawn (one face of twinkling flowers 57 -> plain grass 17)
        new("magiccrafters", "the stone arch", [7650, 6850, 7610, 6980, 7500, 7075, 7350, 7110, 7200, 7075, 7090, 6980, 7050, 6850, 7090, 6720, 7200, 6625, 7350, 6590, 7500, 6625, 7610, 6720], Swap: [57, 17]),
        // Magic Crafters: the round floor of dark flagstones with the marble arch that stands on it (ZHigh drops the
        // cave's dome)
        new("magiccrafters", "the round court", [7495, 3545, 7490, 3406, 7532, 3282, 7614, 3182, 7725, 3115, 7851, 3090, 7979, 3110, 8091, 3172, 8176, 3268, 8223, 3388, 8226, 3516, 8185, 3639, 8106, 3740, 7996, 3808, 7870, 3834, 7741, 3815, 7619, 3747, 7540, 3656], ZHigh: 1898),
        // High Caves: a lawn climbing round a raised round bed and two kerbed basins, held by a low wall: one step of
        // 192 in the rim (the three pools' still water 67 -> lawn 69: water would be walked on)
        new("highcaves", "the walled garden", [10160, 5450, 10115, 5650, 10040, 5800, 9960, 5920, 9850, 6010, 9720, 6080, 9320, 6080, 9110, 5990, 9000, 5900, 8920, 5800, 8865, 5700, 8850, 5600, 8860, 5540, 9800, 4860, 9960, 4900, 10080, 5050, 10150, 5250], ZLow: 2480, Swap: [67, 69]),
        // Wizard Peak: the eight-sided hall with the purple barrel roof, turret bays and onion spire; closed all
        // round. It stands on a rock pillar with no ground beside it: Fill.
        new("wizardpeak", "the wizard hall", [2829, 1902, 2937, 1902, 3080, 1975, 3299, 2176, 3373, 2831, 3373, 2939, 3298, 3088, 3091, 3278, 2937, 3351, 2829, 3351, 2403, 2938, 2403, 2319, 2478, 2176, 2685, 1975], ZLow: 1700, Fill: true),
        // Wizard Peak: the compact half of the terraced garden: the upper round bed, the lawn ramp round it, the kerb
        // wall and a trough (pool water 67 -> lawn 31)
        new("wizardpeak", "the upper garden", [7965, 5225, 8560, 5225, 8700, 5290, 8800, 5450, 8790, 6250, 7965, 6140], ZLow: 1150, Swap: [67, 31]),
        // Misty Bog: grand: the round grass island with its curved timber wall and the stone-walled causeway that
        // climbs to it, steps at one end, an arch at the other.
        new("mistybog", "the walled causeway", [5020, 9450, 5020, 8900, 5120, 8780, 6347, 8537, 6752, 8705, 7035, 9032, 6916, 9555, 6842, 9651, 6233, 9826, 5120, 9570]),
        // Misty Bog: a low round timber platform with a small thatched hut on posts.
        new("mistybog", "the bog hut", [12680, 5483, 12629, 5673, 12490, 5812, 12300, 5863, 12110, 5812, 11971, 5673, 11920, 5483, 11971, 5293, 12110, 5154, 12300, 5103, 12490, 5154, 12629, 5293]),
        // Misty Bog: grand: the plank boardwalk that curves and climbs to a thatched open shelter on stilts.
        new("mistybog", "the boardwalk shelter", [7937, 4700, 8050, 4320, 8300, 4310, 9150, 4730, 9330, 4950, 9430, 5300, 9480, 5700, 9260, 6320, 9040, 6370, 8690, 6150, 7960, 5010]),
        // Misty Bog: a low sandy rise with a horseshoe of ruined wall round a patch of grass.
        new("mistybog", "the horseshoe ruin", [3962, 9013, 4057, 8835, 4225, 8731, 4847, 8739, 4989, 8858, 5015, 9219, 4987, 9403, 4755, 9619, 4582, 9652, 4408, 9604, 4073, 9382, 3969, 9215]),
        // Tree Tops: two six-sided huts with pointed roofs on the big island's lawn (the outline clips the top of the
        // supercharge ramp: its moving arrow grass 6, 7, 8, 10 -> plain grass 21)
        new("treetops", "the twin huts", [4636, 3800, 4734, 3776, 4900, 3790, 5395, 3905, 5562, 4223, 5562, 4577, 5519, 4758, 5406, 4906, 5270, 4893, 4625, 4040], Swap: [6, 21, 7, 21, 8, 21, 10, 21]),
        // Tree Tops: the small paved island with one big six-sided hut and a tiny one.
        new("treetops", "the stone hut", [2155, 5500, 2202, 5393, 2336, 5233, 2414, 5143, 2530, 5110, 2660, 5101, 2777, 5160, 2858, 5262, 2901, 5379, 2905, 5500, 2821, 5712, 2759, 5816, 2655, 5885, 2530, 5875, 2423, 5828, 2188, 5611]),
        // Tree Tops: the round wooden deck with seven stone pillars in a ring round a taller one.
        new("treetops", "the pillar deck", [4457, 7438, 4588, 7268, 4781, 7193, 4970, 7125, 5182, 7137, 5331, 7289, 5470, 7440, 5495, 7650, 5480, 7860, 5188, 8177, 4970, 8190, 4535, 8085, 4457, 7862]),
        // Tree Tops: the same ring of pillars on grass (ZLow drops the walkway under it)
        new("treetops", "the pillar lawn", [6225, 3020, 6301, 2863, 6401, 2741, 6517, 2627, 6680, 2580, 6981, 2719, 7031, 2875, 7060, 3020, 7045, 3171, 7002, 3342, 6843, 3413, 6529, 3385, 6400, 3300, 6301, 3177], ZLow: 2250),
        // Dream Weavers: the slender round tower with the green cone roof, 830 tall, and its patch of lawn.
        new("dreamweavers", "the round tower", [3461, 1119, 3573, 1170, 3617, 1285, 3566, 1398, 3452, 1447, 3160, 1570, 3040, 1520, 3010, 1330, 3180, 1235, 3347, 1161], ZLow: 800),
        // Dream Weavers: the free-standing teal arch with the spiked crest, walked through.
        new("dreamweavers", "the dragon arch", [4725, 4200, 4702, 4265, 4637, 4317, 4542, 4346, 4438, 4346, 4343, 4317, 4278, 4265, 4255, 4200, 4278, 4135, 4343, 4083, 4438, 4054, 4542, 4054, 4637, 4083, 4702, 4135], ZLow: 700),
        // Dark Passage: nothing. It is tunnels and cave rooms round two pits: every wall is the cave itself, and what
        // stands free (the spires of the east pit) stands in the glowing liquid.
        // Lofty Castle: the twin-spired pink tower with orange roofs and six flying buttresses, the lawn spiralling
        // round it.
        new("loftycastle", "the spiral tower", [2366, 3286, 2335, 3313, 2148, 3381, 1948, 3381, 1761, 3313, 1609, 3184, 1509, 3012, 1474, 2816, 1509, 2620, 1609, 2448, 1761, 2319, 1948, 2251, 2148, 2251, 2335, 2319, 2366, 2346, 2366, 2503, 2366, 2660, 2366, 2816, 2366, 2972, 2366, 3129], ZLow: 1700, Fill: true),
        // Haunted Towers: the ring-shaped vaulted gallery round an open paved middle, four gold-trimmed gateways
        // (moving flagstones 44 -> cream marble 25, scrollwork 21, 23 -> 20, pearl band 22 -> 27)
        new("hauntedtowers", "the round court", [3626, 6269, 3558, 6434, 3431, 6561, 3266, 6629, 3088, 6629, 2923, 6561, 2796, 6434, 2728, 6269, 2728, 6091, 2796, 5926, 2923, 5799, 3088, 5731, 3266, 5731, 3431, 5799, 3558, 5926, 3626, 6091], ZLow: 1500, Swap: [44, 25, 21, 20, 22, 27, 23, 20]),
        // Haunted Towers: the small castle with the teal yard, flat orange roofs and four pointed towers, a lawn
        // along its front (moving window 40 -> 39, eaves 41 -> 15). It floats in the sky in the game: Fill.
        new("hauntedtowers", "the courtyard keep", [8248, 8106, 8388, 8150, 8529, 8195, 8669, 8240, 8809, 8285, 8949, 8330, 9089, 8375, 9110, 8509, 9024, 8781, 8978, 8880, 8884, 9044, 8803, 9085, 8654, 9128, 8407, 9049, 8188, 8888, 8035, 8775, 8113, 8528, 8192, 8281], ZLow: 1700, Swap: [41, 15, 40, 39], Fill: true),
        // Haunted Towers: the round pavilion of white ribs on a low plinth with one porch (moving flagstones 44 ->
        // cream marble 25)
        new("hauntedtowers", "the ribbed dome", [5247, 8216, 5209, 8341, 5126, 8442, 5011, 8503, 4882, 8516, 4757, 8478, 4656, 8395, 4595, 8280, 4582, 8151, 4620, 8026, 4703, 7925, 4817, 7864, 4947, 7851, 5072, 7889, 5173, 7972, 5234, 8087], ZLow: 1700, Swap: [44, 25]),
        // Twilight Harbor: the square steel deck with two gantries of tall six-sided pylons.
        new("twilightharbor", "the pylon deck", [4461, 6534, 5105, 6534, 5105, 7290, 4461, 7290], ZLow: 600),
        // Twilight Harbor: the round stepped look-out tower with a domed cap on its twelve-sided deck (turning radar
        // screens 11 -> porthole 36)
        new("twilightharbor", "the radar tower", [3531, 5005, 3660, 4876, 3837, 4828, 4013, 4876, 4143, 5005, 4190, 5182, 4143, 5358, 4013, 5488, 3837, 5535, 3660, 5488, 3531, 5358, 3483, 5182], ZLow: 500, Swap: [11, 36]),
        // Twilight Harbor: the riveted blockhouse with the six-sided turret roof, two stacks and two doorways (moving
        // grating 25 -> 20); a fin of the cliff it leant on stays at one corner. It stands on a narrow deck: Fill.
        new("twilightharbor", "the square bunker", [5604, 4516, 6448, 4758, 6557, 4954, 6326, 5755, 5828, 5613, 5330, 5469, 5466, 4992], ZLow: 440, Swap: [25, 20], Fill: true),
    ];

    /// <summary>Every cut of a level that can be read out of it.</summary>
    public static List<LevelCut> For(LevelPropSource source, Func<int, bool> usable) =>
        [.. Table.Where(d => d.Level == source.LevelKey).Select(d => Extract(source, d, usable)).OfType<LevelCut>()];

    private static double Up(SourceFace f)
    {
        double nx = 0, ny = 0, nz = 0; var p = f.Corners;
        for (int k = 0; k < p.Length; k++)
        {
            var a = p[k]; var b = p[(k + 1) % p.Length];
            nx += (a.Y - b.Y) * (double)(a.Z + b.Z); ny += (a.Z - b.Z) * (double)(a.X + b.X); nz += (a.X - b.X) * (double)(a.Y + b.Y);
        }
        double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        return len <= 0 ? 0 : (f.Flip ? -nz : nz) / len;
    }

    /// <summary>The cut, or null when its outline is not convex, it holds too little floor or too much of it cannot be
    /// carried. The floor is trimmed to the outline exactly (a tile across it is cut along it, its paint carried to the
    /// new corners), so the cut's edge is the outline: the corners of the trimmed floor along it, and between them, where
    /// no floor reaches the outline (under a wall), the outline's own corners at the height of the floor either side.
    /// Everything else stands in the cut when its middle is inside the outline.</summary>
    public static LevelCut? Extract(LevelPropSource source, CutDefinition def, Func<int, bool> usable)
    {
        bool debug = Environment.GetEnvironmentVariable("SPYRO_RANDOM_LEVEL_DEBUG") == "cut";
        var outline = new (double X, double Y)[def.Outline.Length / 2];
        for (int k = 0; k < outline.Length; k++) outline[k] = (def.Outline[2 * k], def.Outline[2 * k + 1]);
        int n = outline.Length;
        double turn = 0;
        for (int k = 0; k < n; k++) turn += outline[k].X * outline[(k + 1) % n].Y - outline[(k + 1) % n].X * outline[k].Y;
        if (turn < 0) Array.Reverse(outline);
        for (int k = 0; k < n; k++)
            if (Cross(outline[k], outline[(k + 1) % n], outline[(k + 2) % n]) < 0) { if (debug) Console.Error.WriteLine($"cut {def.Name}: outline not convex"); return null; }
        // How far round the outline each of its corners is.
        var at = new double[n + 1];
        for (int k = 0; k < n; k++) at[k + 1] = at[k] + Dist(outline[k], outline[(k + 1) % n]);
        double around = at[n];
        // How far round the outline a point on it is (-1: not on it).
        double Along(double x, double y)
        {
            for (int k = 0; k < n; k++)
            {
                var a = outline[k]; var b = outline[(k + 1) % n];
                double len = at[k + 1] - at[k], t = ((x - a.X) * (b.X - a.X) + (y - a.Y) * (b.Y - a.Y)) / (len * len);
                if (t < -1e-6 || t > 1 + 1e-6) continue;
                double px = a.X + (b.X - a.X) * t - x, py = a.Y + (b.Y - a.Y) * t - y;
                if (px * px + py * py < 0.01) return (at[k] + len * Math.Clamp(t, 0, 1)) % around;
            }
            return -1;
        }
        var swap = new Dictionary<int, int>();
        for (int k = 0; def.Swap != null && k + 1 < def.Swap.Length; k += 2) swap[def.Swap[k]] = def.Swap[k + 1];
        var faces = new List<SourceFace>();
        var marks = new List<(double T, Vector3f P)>();
        // What the floor is like at each of its corners on the outline: its texture and its paint there.
        var skin = new Dictionary<Vector3f, (int Texture, ColorRgba Paint)>();
        var covered = new List<(double From, double To)>();
        int all = 0, dropped = 0, floors = 0;
        var refused = new SortedDictionary<int, int>();
        foreach (var face in source.Faces)
        {
            var f = swap.TryGetValue(face.TextureId, out int still) ? face with { TextureId = still } : face;
            double cx = f.Corners.Average(v => v.X), cy = f.Corners.Average(v => v.Y), cz = f.Corners.Average(v => v.Z);
            if (cz < def.ZLow || cz > def.ZHigh) continue;
            if (Up(f) < 0.72)
            {
                if (TerrainHull.Distance(outline, cx, cy) >= 0) continue;
                all++;
                if (usable(f.TextureId)) faces.Add(Distinct(f)); else { dropped++; refused[f.TextureId] = refused.GetValueOrDefault(f.TextureId) + 1; }
                continue;
            }
            if (f.Corners.All(v => TerrainHull.Distance(outline, v.X, v.Y) <= 0))
            {
                all++; floors++;
                if (usable(f.TextureId)) faces.Add(Distinct(f)); else { dropped++; refused[f.TextureId] = refused.GetValueOrDefault(f.TextureId) + 1; }
                continue;
            }
            // A tile across the outline: the part inside, in triangles.
            var part = Clip(f, outline);
            if (part.Count < 3) continue;
            all++; floors++;
            if (!usable(f.TextureId)) { dropped++; refused[f.TextureId] = refused.GetValueOrDefault(f.TextureId) + 1; continue; }
            var corners = part.Select(p => new Vector3f((float)p.X, (float)p.Y, (float)p.Z)).ToArray();
            var paint = part.Select(p => new ColorRgba((byte)Math.Clamp(Math.Round(p.R), 0, 255), (byte)Math.Clamp(Math.Round(p.G), 0, 255), (byte)Math.Clamp(Math.Round(p.B), 0, 255))).ToArray();
            for (int k = 1; k + 1 < corners.Length; k++)
                faces.Add(f with { Corners = [corners[0], corners[k], corners[k + 1]], Near = [paint[0], paint[k], paint[k + 1]] });
            // Its corners on the outline, and the stretches of outline its edges run along.
            var on = part.Select(p => Along(p.X, p.Y)).ToArray();
            for (int k = 0; k < part.Count; k++)
            {
                if (on[k] < 0) continue;
                marks.Add((on[k], corners[k]));
                skin[corners[k]] = (f.TextureId, paint[k]);
                int next = (k + 1) % part.Count;
                if (on[next] < 0) continue;
                // The stretch of outline between this corner and the next (the short way round).
                covered.Add(Gap(on[k], on[next]) <= around / 2 ? (on[k], on[next]) : (on[next], on[k]));
            }
        }
        if (debug) Console.Error.WriteLine($"cut {def.Name}: {all} faces, {floors} floor, {dropped} with textures that cannot be carried{(refused.Count > 0 ? " (" + string.Join(", ", refused.Select(p => $"texture {p.Key}: {p.Value}")) + ")" : "")}, {marks.Count} floor corners on the outline");
        if (floors < 12 || dropped > 0.08 * all || marks.Count < 4) return null;
        // The edge: the floor's corners on the outline in order round it (one at a point: the lowest); between two
        // with no floor along the outline between them, the outline's corners and a point every so often, their height
        // running evenly from the one to the other.
        marks.Sort((a, b) => a.T.CompareTo(b.T));
        var edge = new List<(double T, Vector3f P)>();
        foreach (var m in marks)
        {
            if (edge.Count > 0 && (Math.Abs(edge[^1].T - m.T) < 0.5 || Math.Round(edge[^1].P.X) == Math.Round(m.P.X) && Math.Round(edge[^1].P.Y) == Math.Round(m.P.Y)))
            {
                if (m.P.Z < edge[^1].P.Z) edge[^1] = m;
                continue;
            }
            edge.Add(m);
        }
        if (edge.Count > 1 && (Math.Abs(edge[0].T + around - edge[^1].T) < 0.5)) { if (edge[^1].P.Z < edge[0].P.Z) edge[0] = edge[^1]; edge.RemoveAt(edge.Count - 1); }
        bool Covered(double from, double to)
        {
            double mid = from + Gap(from, to) / 2;
            return covered.Any(c => Gap(c.From, mid % around) <= Gap(c.From, c.To) + 1e-6);
        }
        double Gap(double from, double to) => ((to - from) % around + around) % around;
        var rim = new List<Vector3f>();
        // For each point of the edge: the floor's texture and paint there (between two floor corners: the first one's).
        var skins = new List<(int Texture, ColorRgba Paint)>();
        for (int k = 0; k < edge.Count; k++)
        {
            var (t0, p0) = edge[k]; var (t1, p1) = edge[(k + 1) % edge.Count];
            rim.Add(p0);
            double gap = edge.Count == 1 ? around : Gap(t0, t1);
            bool floored = gap < 1 || Covered(t0, t1);
            skins.Add(skin[p0]);
            if (floored) continue;
            // Under a wall, or a gap in the floor: the outline's corners and a point every 120 units.
            var between = new List<double>();
            for (int c = 0; c < n; c++) { double g = Gap(t0, at[c]); if (g > 1 && g < gap - 1) between.Add(g); }
            int steps = (int)(gap / 120);
            for (int q = 1; q <= steps; q++) between.Add(gap * q / (steps + 1));
            foreach (double g in between.Distinct().OrderBy(g => g))
            {
                var (x, y) = PointAt((t0 + g) % around);
                rim.Add(new Vector3f((float)Math.Round(x), (float)Math.Round(y), (float)(p0.Z + (p1.Z - p0.Z) * g / gap)));
                skins.Add(skin[p0]);
            }
        }
        (double X, double Y) PointAt(double t)
        {
            for (int k = 0; k < n; k++)
                if (t <= at[k + 1] || k == n - 1)
                {
                    double u = (t - at[k]) / (at[k + 1] - at[k]);
                    return (outline[k].X + (outline[(k + 1) % n].X - outline[k].X) * u, outline[k].Y + (outline[(k + 1) % n].Y - outline[k].Y) * u);
                }
            return outline[0];
        }
        // Points that ended up on top of each other once rounded.
        var clean = new List<Vector3f>();
        var skinAt = new List<(int Texture, ColorRgba Paint)>();
        for (int k = 0; k < rim.Count; k++)
        {
            var p = rim[k];
            if (clean.Count == 0 || Math.Abs(clean[^1].X - p.X) + Math.Abs(clean[^1].Y - p.Y) >= 2) { clean.Add(p); skinAt.Add(skins[k]); }
            else skinAt[^1] = skins[k];
        }
        if (clean.Count > 2 && Math.Abs(clean[0].X - clean[^1].X) + Math.Abs(clean[0].Y - clean[^1].Y) < 2) { clean.RemoveAt(clean.Count - 1); skinAt.RemoveAt(skinAt.Count - 1); }
        if (clean.Count < 4) return null;
        // The cut's middle: the middle of the smallest circle round its outline (not the mean of the outline's corners,
        // which lies towards wherever the corners are many and makes the cut ask for more room than it needs).
        var (mx, my) = Middle(outline);
        if (def.Fill)
        {
            // Floor for the pockets a convex outline leaves at the foot of walls that have no ground beside them: from
            // every stretch of the edge a strip towards the cut's middle (FillDepth deep, its inner side nearer the
            // edge's mean height), less whatever floor the cut has there at ground height or below (the ground itself,
            // a yard, a step down); what is left is laid with the floor's texture and paint at the stretch's start.
            // (Under a solid building it is hidden.)
            var ground = faces.Where(g => Up(g) >= 0.72).Select(g =>
            {
                var shape = g.Corners.Select(v => ((double)v.X, (double)v.Y, (double)v.Z)).ToList();
                double area = 0;
                for (int k = 0; k < shape.Count; k++) area += shape[k].Item1 * shape[(k + 1) % shape.Count].Item2 - shape[(k + 1) % shape.Count].Item1 * shape[k].Item2;
                if (area < 0) shape.Reverse();
                if (Math.Abs(area) < 2) shape.Clear();
                return (Shape: shape, Z: g.Corners.Average(v => v.Z), X0: g.Corners.Min(v => v.X), X1: g.Corners.Max(v => v.X), Y0: g.Corners.Min(v => v.Y), Y1: g.Corners.Max(v => v.Y));
            }).ToList();
            int laid = 0;
            double level = clean.Average(v => v.Z);
            for (int k = 0; k < clean.Count; k++)
            {
                var like = skinAt[k];
                var a = clean[k]; var b = clean[(k + 1) % clean.Count];
                if (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) < 4) continue;
                (double, double, double) In(Vector3f v)
                {
                    double reach = Math.Sqrt((mx - v.X) * (mx - v.X) + (my - v.Y) * (my - v.Y)), part = Math.Min(1, FillDepth / Math.Max(reach, 1));
                    return (v.X + (mx - v.X) * part, v.Y + (my - v.Y) * part, v.Z + (level - v.Z) * part);
                }
                var strip = new List<(double X, double Y, double Z)> { (a.X, a.Y, a.Z), (b.X, b.Y, b.Z), In(b), In(a) };
                double x0 = strip.Min(q => q.X), x1 = strip.Max(q => q.X), y0 = strip.Min(q => q.Y), y1 = strip.Max(q => q.Y), top = Math.Max(a.Z, b.Z) + 60;
                var left = new List<List<(double X, double Y, double Z)>> { strip };
                foreach (var g in ground)
                {
                    if (g.Shape.Count < 3 || g.Z > top || g.X1 <= x0 || g.X0 >= x1 || g.Y1 <= y0 || g.Y0 >= y1) continue;
                    left = [.. left.SelectMany(piece => Without(piece, g.Shape))];
                    if (left.Count == 0) break;
                }
                foreach (var piece in left)
                {
                    // (A sliver is not worth a face: nobody can stand in it.)
                    if (Area(piece) < FillLeast) continue;
                    // Its corners on whole units, those on a straight line between their neighbours left out; then
                    // four-cornered faces fanned from the first corner (a three-cornered one where four would not be sound).
                    var corners = new List<Vector3f>();
                    foreach (var q in piece)
                    {
                        var v = new Vector3f((float)Math.Round(q.X), (float)Math.Round(q.Y), (float)Math.Round(q.Z));
                        if (corners.Count == 0 || corners[^1] != v) corners.Add(v);
                    }
                    if (corners.Count > 1 && corners[0] == corners[^1]) corners.RemoveAt(corners.Count - 1);
                    static double Bend(Vector3f u, Vector3f v, Vector3f w) => (v.X - u.X) * (double)(w.Y - u.Y) - (v.Y - u.Y) * (double)(w.X - u.X);
                    for (int c = 0; corners.Count > 3 && c < corners.Count; c++)
                        if (Bend(corners[(c + corners.Count - 1) % corners.Count], corners[c], corners[(c + 1) % corners.Count]) < 4) { corners.RemoveAt(c); c--; }
                    if (corners.Count < 3 || Bend(corners[0], corners[1], corners[2]) < 4) continue;
                    for (int c = 1; c + 1 < corners.Count;)
                    {
                        bool four = c + 2 < corners.Count && Bend(corners[0], corners[c], corners[c + 1]) >= 4 && Bend(corners[0], corners[c + 1], corners[c + 2]) >= 4;
                        Vector3f[] face = four ? [corners[0], corners[c], corners[c + 1], corners[c + 2]] : [corners[0], corners[c], corners[c + 1]];
                        if (four || Bend(face[0], face[1], face[2]) >= 4)
                        {
                            faces.Add(new SourceFace(face, like.Texture, [.. face.Select(_ => like.Paint)], false, false));
                            laid++;
                        }
                        c += four ? 2 : 1;
                    }
                }
            }
            if (debug) Console.Error.WriteLine($"cut {def.Name}: {laid} faces of floor laid inside the outline where the level has none");
        }
        double ox = Math.Round(mx), oy = Math.Round(my), oz = Math.Round(clean.Average(v => v.Z));
        Vector3f Rel(Vector3f v) => new((float)(v.X - ox), (float)(v.Y - oy), (float)(v.Z - oz));
        var rel = clean.Select(Rel).ToArray();
        return new LevelCut
        {
            Name = def.Name, Level = def.Level, Rim = rel,
            Faces = [.. faces.Select(f => f with { Corners = [.. f.Corners.Select(Rel)] })],
            Radius = rel.Max(v => Math.Sqrt(v.X * (double)v.X + v.Y * (double)v.Y)),
        };
    }

    /// <summary>A face with one corner stored twice (a triangle kept as four corners) as the triangle it is: a face
    /// with two corners in one place is left out when a level is built (<see cref="LevelProps.Place"/>), which left a
    /// hole in the floor of a cut.</summary>
    private static SourceFace Distinct(SourceFace f)
    {
        if (f.Corners.Length != 4) return f;
        for (int k = 0; k < 4; k++)
        {
            int next = (k + 1) % 4;
            if (f.Corners[k] != f.Corners[next]) continue;
            int[] keep = [.. Enumerable.Range(0, 4).Where(i => i != next)];
            return f with { Corners = [.. keep.Select(i => f.Corners[i])], Near = [.. keep.Select(i => f.Near[i])] };
        }
        return f;
    }

    /// <summary>The middle of the smallest circle round some points: of the middles of every two of them and of the
    /// circles through every three, the one the furthest point is nearest to.</summary>
    internal static (double X, double Y) Middle((double X, double Y)[] p)
    {
        var best = (X: p.Average(q => q.X), Y: p.Average(q => q.Y));
        double reach = p.Max(q => Dist(q, best));
        void Try((double X, double Y) c)
        {
            double r = p.Max(q => Dist(q, c));
            if (r < reach - 1e-9) { reach = r; best = c; }
        }
        for (int i = 0; i < p.Length; i++)
            for (int j = i + 1; j < p.Length; j++)
            {
                Try(((p[i].X + p[j].X) / 2, (p[i].Y + p[j].Y) / 2));
                for (int k = j + 1; k < p.Length; k++)
                {
                    double d = 2 * Cross(p[i], p[j], p[k]);
                    if (Math.Abs(d) < 1e-6) continue;
                    double a2 = p[i].X * p[i].X + p[i].Y * p[i].Y, b2 = p[j].X * p[j].X + p[j].Y * p[j].Y, c2 = p[k].X * p[k].X + p[k].Y * p[k].Y;
                    Try(((a2 * (p[j].Y - p[k].Y) + b2 * (p[k].Y - p[i].Y) + c2 * (p[i].Y - p[j].Y)) / d,
                         (a2 * (p[k].X - p[j].X) + b2 * (p[i].X - p[k].X) + c2 * (p[j].X - p[i].X)) / d));
                }
            }
        return best;
    }

    private static double Area(List<(double X, double Y, double Z)> p)
    {
        double area = 0;
        for (int k = 0; k < p.Count; k++) area += p[k].X * p[(k + 1) % p.Count].Y - p[(k + 1) % p.Count].X * p[k].Y;
        return Math.Abs(area) / 2;
    }

    /// <summary>The part of a shape on one side of the line from a to b (its left when <paramref name="left"/>), heights
    /// carried along the sides it crosses.</summary>
    private static List<(double X, double Y, double Z)> Side(List<(double X, double Y, double Z)> p, (double X, double Y) a, (double X, double Y) b, bool left)
    {
        var part = new List<(double X, double Y, double Z)>();
        for (int k = 0; k < p.Count; k++)
        {
            var u = p[k]; var v = p[(k + 1) % p.Count];
            double su = (b.X - a.X) * (u.Y - a.Y) - (b.Y - a.Y) * (u.X - a.X), sv = (b.X - a.X) * (v.Y - a.Y) - (b.Y - a.Y) * (v.X - a.X);
            if (!left) { su = -su; sv = -sv; }
            if (su >= 0) part.Add(u);
            if (su > 0 && sv < 0 || su < 0 && sv > 0)
            {
                double t = su / (su - sv);
                part.Add((u.X + (v.X - u.X) * t, u.Y + (v.Y - u.Y) * t, u.Z + (v.Z - u.Z) * t));
            }
        }
        return part;
    }

    /// <summary>A flat shape (convex, counter-clockwise) less the part of it under another (convex, counter-clockwise):
    /// convex pieces. The shape itself when the two do not overlap.</summary>
    private static List<List<(double X, double Y, double Z)>> Without(List<(double X, double Y, double Z)> shape, List<(double X, double Y, double Z)> other)
    {
        var both = shape;
        for (int k = 0; k < other.Count && both.Count >= 3; k++) both = Side(both, (other[k].X, other[k].Y), (other[(k + 1) % other.Count].X, other[(k + 1) % other.Count].Y), true);
        if (both.Count < 3 || Area(both) < 1) return [shape];
        var pieces = new List<List<(double X, double Y, double Z)>>();
        var rest = shape;
        for (int k = 0; k < other.Count && rest.Count >= 3; k++)
        {
            var a = (other[k].X, other[k].Y); var b = (other[(k + 1) % other.Count].X, other[(k + 1) % other.Count].Y);
            var outside = Side(rest, a, b, false);
            if (outside.Count >= 3 && Area(outside) >= 1) pieces.Add(outside);
            rest = Side(rest, a, b, true);
        }
        return pieces;
    }

    private static double Cross((double X, double Y) a, (double X, double Y) b, (double X, double Y) c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    private static double Dist((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>The part of a face inside a convex outline (counter-clockwise), corner by corner with height and paint
    /// carried along each edge it crosses; in the face's own order.</summary>
    internal static List<(double X, double Y, double Z, double R, double G, double B)> Clip(SourceFace f, (double X, double Y)[] outline)
    {
        var poly = new List<(double X, double Y, double Z, double R, double G, double B)>(
            f.Corners.Select((v, k) => ((double)v.X, (double)v.Y, (double)v.Z, (double)f.Near[k].R, (double)f.Near[k].G, (double)f.Near[k].B)));
        for (int e = 0; e < outline.Length && poly.Count > 0; e++)
        {
            var a = outline[e]; var b = outline[(e + 1) % outline.Length];
            double Side((double X, double Y, double Z, double R, double G, double B) p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            var next = new List<(double X, double Y, double Z, double R, double G, double B)>();
            for (int k = 0; k < poly.Count; k++)
            {
                var p = poly[k]; var q = poly[(k + 1) % poly.Count];
                double sp = Side(p), sq = Side(q);
                if (sp >= 0) next.Add(p);
                if (sp >= 0 != sq >= 0)
                {
                    double t = sp / (sp - sq);
                    next.Add((p.X + (q.X - p.X) * t, p.Y + (q.Y - p.Y) * t, p.Z + (q.Z - p.Z) * t, p.R + (q.R - p.R) * t, p.G + (q.G - p.G) * t, p.B + (q.B - p.B) * t));
                }
            }
            poly = next;
        }
        // Corners closer together than a unit become one.
        var clean = new List<(double X, double Y, double Z, double R, double G, double B)>();
        foreach (var p in poly)
            if (clean.Count == 0 || Math.Abs(clean[^1].X - p.X) + Math.Abs(clean[^1].Y - p.Y) + Math.Abs(clean[^1].Z - p.Z) >= 1) clean.Add(p);
        if (clean.Count > 1 && Math.Abs(clean[0].X - clean[^1].X) + Math.Abs(clean[0].Y - clean[^1].Y) + Math.Abs(clean[0].Z - clean[^1].Z) < 1) clean.RemoveAt(clean.Count - 1);
        return clean;
    }

    /// <summary>Triangles filling the ground between an outer edge (counter-clockwise) and an edge inside it
    /// (counter-clockwise too, the hole): corners as indices into the outer edge's points followed by the inner edge's.
    /// Null when the two cross or the space cannot be filled.</summary>
    internal static List<(int A, int B, int C)>? Sew(IReadOnlyList<(double X, double Y)> outer, IReadOnlyList<(double X, double Y)> inner)
    {
        int no = outer.Count, ni = inner.Count;
        var pts = outer.Concat(inner).ToList();
        // Every inner corner inside the outer edge, every outer corner outside the inner one, and no edges crossing.
        var o = outer.ToArray(); var h = inner.ToArray();
        if (h.Any(p => TerrainHull.Distance(o, p.X, p.Y) >= -0.5) || o.Any(p => TerrainHull.Distance(h, p.X, p.Y) <= 0.5)) return null;
        static bool Crosses((double X, double Y) a, (double X, double Y) b, (double X, double Y) c, (double X, double Y) d)
        {
            double d1 = Cross(c, d, a), d2 = Cross(c, d, b), d3 = Cross(a, b, c), d4 = Cross(a, b, d);
            return (d1 > 0 && d2 < 0 || d1 < 0 && d2 > 0) && (d3 > 0 && d4 < 0 || d3 < 0 && d4 > 0);
        }
        // A bridge from the inner edge's rightmost corner to the nearest outer corner it can see.
        int m = 0;
        for (int k = 1; k < ni; k++) if (h[k].X > h[m].X || h[k].X == h[m].X && h[k].Y > h[m].Y) m = k;
        int bridge = -1;
        foreach (int k in Enumerable.Range(0, no).OrderBy(k => (o[k].X - h[m].X) * (o[k].X - h[m].X) + (o[k].Y - h[m].Y) * (o[k].Y - h[m].Y)))
        {
            bool clear = true;
            for (int j = 0; j < no && clear; j++)
            {
                int j1 = (j + 1) % no;
                if (j == k || j1 == k) continue;
                if (Crosses(h[m], o[k], o[j], o[j1])) clear = false;
            }
            for (int j = 0; j < ni && clear; j++)
            {
                int j1 = (j + 1) % ni;
                if (j == m || j1 == m) continue;
                if (Crosses(h[m], o[k], h[j], h[j1])) clear = false;
            }
            if (clear) { bridge = k; break; }
        }
        if (bridge < 0) return null;
        // One polygon: round the outer edge to the bridge, round the inner edge the other way, back over the bridge.
        var poly = new List<int>();
        for (int k = 0; k <= bridge; k++) poly.Add(k);
        for (int k = 0; k <= ni; k++) poly.Add(no + (m - k + ni) % ni);
        for (int k = bridge; k < no; k++) poly.Add(k);
        var tris = new List<(int, int, int)>();
        bool Inside(int p, int a, int b, int c)
        {
            var q = pts[p];
            if (q == pts[a] || q == pts[b] || q == pts[c]) return false;
            return Cross(pts[a], pts[b], q) >= 0 && Cross(pts[b], pts[c], q) >= 0 && Cross(pts[c], pts[a], q) >= 0;
        }
        int guard = 0;
        while (poly.Count > 3 && guard++ < 100000)
        {
            bool cut = false;
            for (int k = 0; k < poly.Count && !cut; k++)
            {
                int a = poly[(k - 1 + poly.Count) % poly.Count], b = poly[k], c = poly[(k + 1) % poly.Count];
                if (Cross(pts[a], pts[b], pts[c]) <= 1e-6) continue;
                bool ear = true;
                foreach (int p in poly) if (p != a && p != b && p != c && Inside(p, a, b, c)) { ear = false; break; }
                if (!ear) continue;
                tris.Add((a, b, c)); poly.RemoveAt(k); cut = true;
            }
            if (cut) continue;
            // No ear: a corner on a straight line (or a doubled one) goes without a triangle.
            int flat = -1;
            for (int k = 0; k < poly.Count && flat < 0; k++)
            {
                int a = poly[(k - 1 + poly.Count) % poly.Count], b = poly[k], c = poly[(k + 1) % poly.Count];
                if (Math.Abs(Cross(pts[a], pts[b], pts[c])) <= 1e-6) flat = k;
            }
            if (flat < 0) return null;
            poly.RemoveAt(flat);
        }
        if (poly.Count == 3 && Cross(pts[poly[0]], pts[poly[1]], pts[poly[2]]) > 1e-6) tris.Add((poly[0], poly[1], poly[2]));
        return tris;
    }
}

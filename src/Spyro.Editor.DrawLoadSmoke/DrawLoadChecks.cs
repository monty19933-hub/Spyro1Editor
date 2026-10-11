using System.Buffers.Binary;
using System.Numerics;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

/// <summary>Disc-free checks of the draw-buffer model, the terrain plan and the runtime guard.</summary>
internal static class DrawLoadChecks
{
    private static readonly AuthoredMaterial Material = new(10, 66);
    private static readonly ColorRgba Grey = new(128, 128, 128);
    private static string Id() => Guid.NewGuid().ToString("N");
    private static AuthoredSurface Face(params Vector3f[] v) => new(Id(), v, Grey, Material, [.. v.Select(_ => Grey)], [.. v.Select(_ => Grey)]);

    private static List<NativeDrawLoad.Sector> Sectors(SceneDocument doc)
    {
        var materials = doc.Surfaces.Where(s => s.Material != null).Select(s => s.Material!).Distinct().ToArray();
        bool was = NativeAuthoredMeshRenderer.DrawPlanDisabled;
        NativeAuthoredMeshRenderer.DrawPlanDisabled = true;
        try { return NativeDrawLoad.Parse(NativeAuthoredMeshRenderer.Compile(doc, materials, []).Environment); }
        finally { NativeAuthoredMeshRenderer.DrawPlanDisabled = was; }
    }

    /// <summary>One wall (x = 3000, y 2000..2100, z 1000..1100) facing -x, seen from chosen cameras.</summary>
    internal static void Model(Action<bool, string> check)
    {
        var wall = Face(new(3000, 2000, 1000), new(3000, 2000, 1100), new(3000, 2100, 1100), new(3000, 2100, 1000));
        var doc = SceneDocument.New("WALL") with { Surfaces = [wall] };
        var sectors = Sectors(doc);
        check(sectors.Count == 1 && sectors[0].NearFaces == 1 && sectors[0].FarFaces == 1, "the wall compiles to one sector, its far copy its own face");
        NativeDrawLoad.Load At(float x, double heading, bool twoSided = false, float near = NativeDrawLoad.NearDistance)
        {
            var s = twoSided ? Sectors(doc with { Surfaces = [wall with { TwoSided = true }] }) : sectors;
            return NativeDrawLoad.Frame(s, NativeDrawLoad.Pose.Looking(new Vector3(x, 2050, 1050), heading, 0), near);
        }
        var front = At(2000, 0);
        check(front.Near == 0x34 && front.Far == 0 && front.Subdivided == 0, $"1,000 units in front: one textured quad drawn whole, 0x34 bytes ({front})");
        check(At(2000, Math.PI).Total == 0, "looking away: nothing");
        check(At(2000, Math.PI / 2).Total == 0, "looking along it from the side: its sector is out of view");
        check(At(4000, Math.PI).Total == 0, "from behind: its back is not drawn");
        check(At(4000, Math.PI, twoSided: true).Near == 0x34, "from behind, two-sided: drawn");
        var far = At(500, 0);
        check(far.Near == 0 && far.Far == 0x24, $"2,500 units away: its far copy only, 0x24 bytes ({far})");
        var close = At(2700, 0);
        check(close.Subdivided == 4 * 0x34 && close.NearQueue == 1 && close.Near == 0, $"300 units away: four subdivided pieces ({close})");
        var shortNear = At(1500, 0, near: 1024);
        check(shortNear.Near == 0 && shortNear.Far == 0x24, $"near distance 1,024, 1,500 away: drawn far ({shortNear})");
        check(At(2600, 0, near: 1024).Near == 0x34 || At(2600, 0, near: 1024).Subdivided > 0, "near distance 1,024, 400 away: drawn near");
        // The game's camera (retail Wizard Peak, measured): forward along the third row, editor axes (x, -y, -z) of its columns.
        var game = NativeDrawLoad.Pose.FromGame([3749, 0, -1649, -401, 3973, -911, 1599, 995, 3636], 16 * 1000, 16 * 2000, 16 * 300);
        check(Math.Abs(game.Forward.X - 0.888f) < 0.01 && Math.Abs(game.Forward.Y + 0.390f) < 0.01 && Math.Abs(game.Forward.Z + 0.243f) < 0.01 &&
            game.Down.Z < -0.9f && Math.Abs(Vector3.Dot(game.Forward, game.Right)) < 0.01, $"the game's camera matrix reads as forward {game.Forward}, down {game.Down}");
        check(game.Eye == new Vector3(1000, 2000, 300), "the game's camera position is in sixteenths of a unit");
    }

    /// <summary>A plain textured floor of <paramref name="n"/> x <paramref name="n"/> quads of <paramref name="size"/> units.</summary>
    private static SceneDocument Floor(int n, float size, float x0 = 2000, float y0 = 2000, float z = 1000)
    {
        List<AuthoredSurface> faces = [];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                float x = x0 + i * size, y = y0 + j * size;
                faces.Add(Face(new(x, y, z), new(x + size, y, z), new(x + size, y + size, z), new(x, y + size, z)));
            }
        return SceneDocument.New("FLOOR") with { Surfaces = faces, Spawn = new(x0 + n * size / 2, y0 + n * size / 2, z + 32) };
    }

    internal static void Plan(Action<bool, string> check)
    {
        var materials = new[] { Material };
        // A small level: surveyed, nothing changed.
        var small = Floor(4, 128);
        var smallEnv = NativeAuthoredMeshRenderer.Compile(small, materials, []).Environment;
        var plan = NativeAuthoredMeshRenderer.LastDrawPlan;
        check(plan is { NearDistance: 2048, FarFaceCap: null } && !plan.OverBudget && plan.Spots > 0 && plan.Cameras > 0,
            $"a small floor keeps the retail near distance and its far tables ({plan?.Describe()})");
        check(smallEnv.AsSpan().SequenceEqual(Raw(small)), "the survey changes nothing in a level under the budget");
        // A wide textured floor (56 x 56 quads of 64 units): its distant faces fill the buffer from the middle.
        var wide = Floor(56, 64);
        var raw = NativeDrawLoad.Parse(Raw(wide));
        var cameras = NativeDrawLoad.Cameras(NativeDrawLoad.Spots(wide)).ToList();
        var before = NativeDrawLoad.Survey(raw, cameras)[0];
        var env = NativeAuthoredMeshRenderer.Compile(wide, materials, []).Environment;
        plan = NativeAuthoredMeshRenderer.LastDrawPlan!;
        var after = NativeDrawLoad.Survey(NativeDrawLoad.Parse(env), cameras, plan.NearDistance)[0];
        check(before.Load.Whole > NativeDrawLoad.TerrainBudget, $"the wide floor's heaviest view is over the budget before the plan ({before.Load})");
        check(plan.FarFaceCap != null && !plan.OverBudget && after.Load.Whole == plan.Worst.Whole && after.Load.Whole <= NativeDrawLoad.TerrainBudget,
            $"the plan coarsens its distant meshes until every view fits ({plan.Describe()}; resurveyed {after.Load.Whole})");
        var parsed = NativeDrawLoad.Parse(env);
        check(parsed.All(s => s.FarFaces <= plan.FarFaceCap && s.FarVertices > 0 && s.FarFaces > 0) && parsed.Sum(s => s.NearFaces) == 56 * 56,
            "every sector's far table is within the cap and not empty; every near face is kept");
        // Tiny scattered triangles (imported sectors of 56): no merging grid leaves them faces, yet no far table may be
        // empty (the far pass would loop through the scratchpad into the I/O registers, as in Sunny Villa).
        List<AuthoredSurface> specks = [];
        for (int i = 0; i < 77; i++) for (int j = 0; j < 88; j++)
        {
            float x = 2000 + i * 56, y = 2000 + j * 56;
            specks.Add(Face(new(x, y, 1000), new(x + 5, y, 1000), new(x + 5, y + 5, 1000)) with { Sector = (i / 7) * 11 + j / 8 });
        }
        var dust = SceneDocument.New("SPECKS") with { Surfaces = specks, Spawn = new(4350, 4350, 1032) };
        var dustRaw = NativeDrawLoad.Parse(Raw(dust));
        var dustEnv = NativeDrawLoad.Parse(NativeAuthoredMeshRenderer.Compile(dust, materials, []).Environment);
        var dustPlan = NativeAuthoredMeshRenderer.LastDrawPlan!;
        check(dustRaw.All(s => s.FarVertices > 0 && s.FarFaces > 0) && dustEnv.All(s => s.FarVertices > 0 && s.FarFaces > 0),
            $"scattered specks keep a far table of at least one face in every sector, before and after the plan ({dustPlan.Describe()})");
        // A block of two-sided cards (20 x 20 x 8, imported sectors of 40) next to the start: too much near terrain even at 1,024.
        List<AuthoredSurface> cards = [];
        for (int i = 0; i < 20; i++) for (int j = 0; j < 20; j++) for (int k = 0; k < 8; k++)
        {
            float x = 3000 + i * 50, y = 3000 + j * 50, z = 1000 + k * 40;
            cards.Add(Face(new(x, y, z), new(x, y, z + 30), new(x, y + 30, z + 30), new(x, y + 30, z)) with { TwoSided = true, Sector = (i * 20 + j) / 5 });
        }
        var pad = Face(new(2600, 3400, 1000), new(2800, 3400, 1000), new(2800, 3600, 1000), new(2600, 3600, 1000));
        var heavy = SceneDocument.New("CARDS") with { Surfaces = [.. cards, pad], Spawn = new(2700, 3500, 1032) };
        string? refusal = null;
        try { NativeAuthoredMeshRenderer.Compile(heavy, materials, []); }
        catch (InvalidDataException e) { refusal = e.Message; }
        check(refusal != null && refusal.Contains("Too much terrain in view") && refusal.Contains("1,024 units"), $"terrain over the limit even at 1,024 units is refused: {refusal}");
    }

    private static byte[] Raw(SceneDocument doc)
    {
        bool was = NativeAuthoredMeshRenderer.DrawPlanDisabled;
        NativeAuthoredMeshRenderer.DrawPlanDisabled = true;
        try { return NativeAuthoredMeshRenderer.Compile(doc, doc.Surfaces.Where(s => s.Material != null).Select(s => s.Material!).Distinct().ToArray(), []).Environment; }
        finally { NativeAuthoredMeshRenderer.DrawPlanDisabled = was; }
    }

    private const uint NearWord = 0x800785CC, LevelIdWord = 0x8007596C, BufferEndWord = 0x80075780;
    private const int T1 = 9, T5 = 13, T6 = 14, V0 = 2, A0 = 4, At = 1, S0 = 16, Gp = 28, Sp = 29, Fp = 30, Ra = 31;

    internal static void Guard(Action<bool, string> check)
    {
        int reserve = NativeDrawLoad.AfterTerrainReserve + NativeDrawBufferGuard.Slack;
        var stubs = NativeDrawBufferGuard.Stubs(reserve);
        RenderProbeCpu Cpu(int level, byte[] table)
        {
            var cpu = new RenderProbeCpu();
            var bytes = new byte[stubs.Length * 4];
            for (int i = 0; i < stubs.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 * i), stubs[i]);
            cpu.Copy(NativeDrawBufferGuard.Cave, bytes);
            cpu.Copy(NativeDrawBufferGuard.NearTable, table);
            cpu.Put(LevelIdWord, (uint)level); cpu.Put(BufferEndWord, 0x801BFBB0); cpu.Put(NearWord, 0x5555);
            return cpu;
        }
        var table = new byte[64]; table[33 - 10] = 1792 / 256; table[65 - 10] = 2048 / 256;
        uint limit = 0x801BFBB0 - (uint)reserve, queueLimit = unchecked(limit + NativeDrawBufferGuard.QueueConstant);
        // Frame stub: the built level gets its near distance and the limit; any other level the retail near distance, no limit.
        foreach (var (level, near, limited) in new[] { (33, 0x7000u, true), (65, 0x8000u, true), (13, 0x8000u, false), (5, 0x8000u, false), (80, 0x8000u, false) })
        {
            var cpu = Cpu(level, table);
            cpu.Regs[A0] = 0xFFFFFFFF; cpu.Regs[S0] = 0x1234; cpu.Regs[Ra] = 0x8002BA58;
            bool ran = Try(() => cpu.Run(NativeDrawBufferGuard.FrameStub, NativeDrawBufferGuard.TerrainEntry + 8));
            check(ran && cpu.Get(NearWord) == near && cpu.Get(NativeDrawBufferGuard.Limit) == (limited ? limit : 0) &&
                cpu.Get(NativeDrawBufferGuard.QueueLimit) == (limited ? queueLimit : 0) && cpu.Regs[At] == 0x80077DD8 &&
                cpu.Regs[A0] == 0xFFFFFFFF && cpu.Regs[S0] == 0x1234 && cpu.Regs[Ra] == 0x8002BA58,
                $"frame stub, level {level}: near distance 0x{cpu.Get(NearWord):X}, limit 0x{cpu.Get(NativeDrawBufferGuard.Limit):X8}, back at the entry's third word with at restored");
        }
        // Far face stub: on below the limit (with the two replaced words done), to the near pass from it; always on unguarded.
        foreach (var (lim, fp, expectFull) in new[] { (limit, limit - 4, false), (limit, limit, true), (limit, limit + 0x100, true), (0u, 0x801BFF00u, false) })
        {
            var cpu = Cpu(33, table); cpu.Put(NativeDrawBufferGuard.Limit, lim);
            cpu.Regs[Fp] = fp; cpu.Regs[T5] = 0xABCD1234;
            uint stop = expectFull ? NativeDrawBufferGuard.FarPassEnd : NativeDrawBufferGuard.FarFace + 8;
            bool ran = Try(() => cpu.Run(NativeDrawBufferGuard.FarStub, stop));
            check(ran && (expectFull || cpu.Regs[T1] == ((0xABCD1234u >> 23) & 0x1F8)), $"far stub, packet 0x{fp:X8} limit 0x{lim:X8}: {(expectFull ? "ends the far pass" : "draws on")}");
        }
        // Near face stub: the queued faces' worst output counts against the limit.
        foreach (var (normal, close, room, expectFull) in new[] { (0u, 0u, 4L, false), (0u, 0u, 0L, true), (10u, 3u, 320 * 10 + 1280 * 3 + 4L, false), (10u, 3u, 320 * 10 + 1280 * 3L, true), (126u, 0u, -0x1000L, true) })
        {
            var cpu = Cpu(33, table); cpu.Put(NativeDrawBufferGuard.QueueLimit, queueLimit);
            cpu.Regs[Gp] = NativeDrawBufferGuard.NormalQueue + 4 * normal; cpu.Regs[Sp] = NativeDrawBufferGuard.CloseQueue + 4 * close;
            cpu.Regs[Fp] = (uint)(limit - room); cpu.Regs[T6] = 0x87654321;
            uint stop = expectFull ? NativeDrawBufferGuard.NearPassEnd : NativeDrawBufferGuard.NearFace + 8;
            bool ran = Try(() => cpu.Run(NativeDrawBufferGuard.NearStub, stop));
            check(ran && (expectFull || cpu.Regs[V0] == 0x87654321u >> 14) && cpu.Regs[Gp] == NativeDrawBufferGuard.NormalQueue + 4 * normal,
                $"near stub, {normal} normal and {close} close queue entries, {room} bytes before the limit: {(expectFull ? "subdivides what is queued" : "draws on")}");
        }
        {
            var cpu = Cpu(13, table); cpu.Put(NativeDrawBufferGuard.QueueLimit, 0);
            cpu.Regs[Gp] = NativeDrawBufferGuard.NormalQueue + 400; cpu.Regs[Sp] = NativeDrawBufferGuard.CloseQueue + 400; cpu.Regs[Fp] = 0x801BFFF0;
            check(Try(() => cpu.Run(NativeDrawBufferGuard.NearStub, NativeDrawBufferGuard.NearFace + 8)), "near stub without a limit (a retail level) always draws on");
        }
        // Installing refuses an executable that is not the USA game's where the guard goes.
        var exe = new byte[417792];
        check(Throws(() => NativeDrawBufferGuard.PatchExe(exe, new Dictionary<int, int> { [33] = 2048 })), "an executable without the retail hook words is refused");
        foreach (var (at, word) in new (uint, uint)[] { (0x800258F0, 0x3C018007), (0x800258F4, 0x24217DD8), (0x80026398, 0x000D4DC2), (0x8002639C, 0x312901F8), (0x80026B7C, 0x000E1382), (0x80026B80, 0x000E1982) })
            BinaryPrimitives.WriteUInt32LittleEndian(exe.AsSpan((int)(at - 0x8000F800)), word);
        check(Throws(() => NativeDrawBufferGuard.PatchExe(exe, new Dictionary<int, int> { [33] = 2048 })) && !NativeDrawBufferGuard.IsInstalled(exe),
            "an executable whose code areas differ from the game's is refused and left as it was");
        check(Throws(() => NativeDrawBufferGuard.PatchExe(exe, new Dictionary<int, int> { [33] = 1000 })) && Throws(() => NativeDrawBufferGuard.PatchExe(exe, new Dictionary<int, int> { [5] = 2048 })),
            "near distances off the 256-unit steps and level ids outside 10..73 are refused");
    }

    /// <summary>The guard installed into the retail executable (read from the user's disc, nothing written).</summary>
    internal static void Source(Action<bool, string> check, string image)
    {
        var original = Spyro.Editor.Core.Universal.UniversalDiscImage.Read(image).Exe.ToArray();
        var exe = original.ToArray();
        NativeDrawBufferGuard.PatchExe(exe, new Dictionary<int, int> { [33] = 1792, [65] = 2048 });
        check(NativeDrawBufferGuard.IsInstalled(exe) && NativeDrawBufferGuard.CoveredLevels(exe).Count == 2 && NativeDrawBufferGuard.CoveredLevels(exe)[33] == 1792,
            "installed in the USA executable for levels 33 (1,792) and 65");
        var once = exe.ToArray();
        NativeDrawBufferGuard.PatchExe(exe, new Dictionary<int, int> { [21] = 1536 });
        check(NativeDrawBufferGuard.CoveredLevels(exe).Count == 3 && Diff(once, exe).All(o => o == (int)(NativeDrawBufferGuard.NearTable - 0x8000F800) + 11),
            "installing again adds a level and changes only its table byte");
        check(Throws(() => NativeDrawBufferGuard.PatchExe(exe, new Dictionary<int, int> { [21] = 1536 }, 0x2000)), "an installed guard with another reserve is refused");
        int Off(uint a) => (int)(a - 0x8000F800);
        var allowed = new List<(int From, int To)>
        {
            (Off(NativeDrawBufferGuard.Cave), Off(NativeDrawBufferGuard.CaveEnd)), (Off(NativeDrawBufferGuard.DataCave), Off(NativeDrawBufferGuard.DataCaveEnd)),
            (Off(0x800258F0), Off(0x800258F8)), (Off(0x80026398), Off(0x800263A0)), (Off(0x80026B7C), Off(0x80026B80)),
        };
        check(Diff(original, exe).All(o => allowed.Any(r => o >= r.From && o < r.To)), "only the hooks and the two unreferenced code areas change");
    }

    private static IEnumerable<int> Diff(byte[] a, byte[] b) => Enumerable.Range(0, a.Length).Where(i => a[i] != b[i]);
    private static bool Try(Action a) { try { a(); return true; } catch (Exception e) { Console.WriteLine("  (" + e.Message + ")"); return false; } }
    private static bool Throws(Action a) { try { a(); return false; } catch (InvalidDataException) { return true; } }
}

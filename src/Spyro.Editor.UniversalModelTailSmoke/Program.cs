using System.Buffers.Binary;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

int checks = 0;
void Check(bool okay, string message) { if (!okay) throw new InvalidOperationException(message); checks++; }
void Refuse(Action action, string message) { bool refused = false; try { action(); } catch (InvalidDataException) { refused = true; } Check(refused, message); }
uint I(int op, int rs, int rt, int imm) => UniversalMips.EncodeI(op, rs, rt, imm);
uint first = UniversalOverlay.Base + 0x100, read = first + 16;
uint[] words = [I(15, 0, 9, 0x8007), I(9, 9, 9, 0x6378), I(5, 5, 2, 12), I(0, 20, 0, 0x2821),
    I(33, 20, 3, 0x36), I(9, 0, 2, 0xFF), I(40, 20, 2, 0x52), 0x00031880, 0x00691821,
    I(35, 3, 2, 0), I(9, 0, 6, 8), I(36, 2, 4, 5), UniversalMips.EncodeJ(3, 0x80055A78), I(9, 20, 7, 0x54), 0, 0];
UniversalOverlay Overlay(uint[] code)
{
    byte[] bytes = new byte[0x400];
    for (int n = 0; n < code.Length; n++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x100 + 4 * n), code[n]);
    var bindings = new Dictionary<uint, UniversalExeBinding>();
    foreach (uint global in new[] { UniversalExeBindings.InitGlobal, UniversalExeBindings.UpdateGlobal, UniversalExeBindings.CopyBufferGlobal }) bindings[global] = new(global, 0, 0, first);
    return new(bytes, bindings);
}
Dictionary<uint, HashSet<uint>> Graph() => Enumerable.Range(1, words.Length - 1).ToDictionary(n => first + (uint)n * 4, n => new HashSet<uint> { first + (uint)(n - 1) * 4 });
bool Prove(uint[] code, Dictionary<uint, HashSet<uint>>? graph = null) => UniversalNativeModelTailOwnership.Prove(Overlay(code), read, 20, graph ?? Graph());
Check(Prove(words), "literal base survives conditional fallthrough into second native model lookup");
foreach (int basis in new[] { 6, 9, 10, 11, 12 })
    foreach (bool delayedA2 in new[] { false, true })
    {
        var variant = words.ToArray(); variant[0] = I(15, 0, basis, 0x8007); variant[1] = I(9, basis, basis, 0x6378);
        variant[8] = (uint)((3 << 21) | (basis << 16) | (3 << 11) | 0x21);
        if (delayedA2) (variant[10], variant[13]) = (variant[13], variant[10]);
        Check(Prove(variant), "native base and helper argument order " + basis + "/" + delayedA2);
    }
uint[] local = [0, 0, 0, I(9, 0, 2, 0xFF), I(33, 20, 3, 0x36), I(15, 0, 9, 0x8007), I(9, 9, 9, 0x6378),
    I(40, 20, 2, 0x52), 0x00031880, 0x00691821, I(35, 3, 2, 0), I(9, 0, 6, 8), I(36, 2, 4, 5),
    UniversalMips.EncodeJ(3, 0x80055A78), I(9, 20, 7, 0x54), 0];
Check(Prove(local), "model table materialized within the native class-load delay path");
for (int n = 3; n < 15; n++)
{
    var changed = local.ToArray(); changed[n] ^= 1;
    Check(!Prove(changed), "local literal lookup authenticates every native instruction " + n);
    var graph = Graph(); graph[first + (uint)n * 4].Add(UniversalOverlay.Base + 0x80);
    if (n > 3) Check(!Prove(local, graph), "local literal producer cannot be bypassed " + n);
}
for (int n = 4; n < 14; n++)
{
    var changed = words.ToArray(); changed[n] ^= 1;
    Check(!Prove(changed), "each native class/model/argument/call/delay preimage is exact " + n);
}
foreach (var (at, word) in new (int, uint)[] { (0, I(15, 0, 9, 0x8008)), (1, I(9, 9, 9, 0x637C)),
    (1, I(9, 8, 9, 0x6378)), (2, I(9, 0, 9, 1)), (2, UniversalMips.EncodeJ(3, 0x80055A78)),
    (3, UniversalMips.EncodeJ(2, read + 4)) })
{
    var changed = words.ToArray(); changed[at] = word;
    Check(!Prove(changed), "unproved literal/control transfer rejected " + at + "/" + word);
}
for (int n = 1; n < 14; n++)
{
    var graph = Graph(); graph[first + (uint)n * 4].Add(UniversalOverlay.Base + 0x80);
    Check(!Prove(words, graph), "alternate entry cannot bypass a producer or native consumer " + n);
}
var clobbered = Graph(); clobbered[read] = [read + 8];
Check(!Prove(words, clobbered), "cyclic path without a proved base is rejected");
Check(!UniversalNativeModelTailOwnership.Prove(Overlay(words), read, 19, Graph()), "current actor identity exact");
Refuse(() => UniversalNativeModelTailOwnership.RequireHelper(null), "missing executable cannot certify callee liveness");
Refuse(() => UniversalNativeModelTailOwnership.RequireHelper(new byte[0x66000]), "unproved executable cannot certify callee liveness");
Console.WriteLine($"UNIVERSAL_MODEL_TAIL_FREE_OK checks={checks}");
if (args.Length == 0) return;
if (args.Length != 3 || args[0] != "--source") throw new ArgumentException("--source <USA.bin> <workspace>");
var library = new UniversalSourceLibrary(args[1], LevelCatalog.Load(args[2]));
UniversalNativeModelTailOwnership.RequireHelper(library.Disc.Exe);
// Execute the actual native prefix with an address taint in v1. Resolve the real
// a2=8 branch and nested call, including delay slots; reject any read or escape
// before the first overwrite. This oracle does not invoke the production proof.
uint[] registers = Enumerable.Range(0, 32).Select(n => 0x11110000u + (uint)n).ToArray();
registers[0] = 0; registers[6] = 8; registers[3] = 0x80076B6C; registers[29] = 0x801FF000;
uint cursor = 0x80055A78; uint? delayedTarget = null; bool killed = false;
for (int steps = 0; steps < 64 && !killed; steps++)
{
    uint w = BinaryPrimitives.ReadUInt32LittleEndian(library.Disc.Exe.AsSpan((int)(cursor - UniversalExeBindings.ExeBase)));
    int op = (int)(w >> 26), rs = (int)(w >> 21 & 31), rt = (int)(w >> 16 & 31), rd = (int)(w >> 11 & 31), fn = (int)(w & 63);
    int immediate = (short)w; uint? target = null;
    bool reads = op switch { 0 => rs == 3 || rt == 3, 9 => rs == 3, 43 or 4 or 5 => rs == 3 || rt == 3, 15 or 3 => false, _ => throw new InvalidOperationException("Unexpected native liveness opcode") };
    Check(!reads, "native prefix never reads the residual class-index address");
    switch (op)
    {
        case 0 when fn == 0x21: registers[rd] = unchecked(registers[rs] + registers[rt]); killed = rd == 3; break;
        case 9: registers[rt] = unchecked(registers[rs] + (uint)immediate); killed = rt == 3; break;
        case 15: registers[rt] = (uint)(ushort)w << 16; killed = rt == 3; break;
        case 43: break;
        case 4: if (registers[rs] == registers[rt]) target = unchecked(cursor + 4 + (uint)(immediate * 4)); break;
        case 3: registers[31] = cursor + 8; target = (cursor & 0xF0000000) | ((w & 0x03FFFFFF) << 2); break;
        default: throw new InvalidOperationException("Unexpected native liveness instruction");
    }
    uint next = delayedTarget ?? cursor + 4; delayedTarget = target;
    if (killed) Check(cursor == 0x80056DC8 && registers[3] == 0, "actual nested native owner kills v1 before its first use");
    cursor = next;
}
Check(killed, "bounded native prefix reaches the proved overwrite");
foreach (var (address, length) in new (uint, int)[] { (0x80055A78, 0x94), (0x80056DC4, 8) })
    for (int n = 0; n < length; n++)
    {
        var changed = library.Disc.Exe.ToArray(); changed[(int)(address - UniversalExeBindings.ExeBase) + n] ^= 1;
        Refuse(() => UniversalNativeModelTailOwnership.RequireHelper(changed), "full native helper byte authentication");
    }
int proved = 0;
foreach (var level in LevelCatalog.Load(args[2]).Levels.Where(l => l.SourceWadEntry > 0))
{
    var owner = library.Get(level.Key).Overlay;
    var sw = UniversalDispatchAnalysis.FindUpdateSwitch(owner);
    var (code, tables) = UniversalDispatchAnalysis.Closure(owner, sw.Function, [sw.Function], new HashSet<uint>());
    var part = new UniversalAsmPart(); foreach (uint pc in code.Order()) part.Emit(owner.Word(pc), orig: pc);
    var before = part.Items.Select(i => i.Word).ToArray();
    var sites = UniversalNativeModelTailOwnership.MarkOwned(part, owner, library.Disc.Exe);
    Check(part.Items.Select(i => i.Word).SequenceEqual(before), "proof adds no bytes or instructions " + level.Key);
    foreach (uint pc in sites)
    {
        proved++;
        Check(part.Items.Single(i => i.Orig == pc).ClassReadOwned, "native class read marked " + level.Key);
        var pruned = UniversalBehaviorBundleBuilder.BuildPrunedUpdate(owner, [0x186], "model_tail_probe", new Dictionary<int, int> { [0x186] = 0x1FD }).Part;
        var prunedWords = pruned.Items.Select(i => i.Word).ToArray();
        Check(UniversalNativeModelTailOwnership.MarkOwned(pruned, owner, library.Disc.Exe).Contains(pc), "actual pruned update retains the complete model witness " + level.Key);
        Check(pruned.Items.Select(i => i.Word).SequenceEqual(prunedWords), "actual pruned update byte length and words unchanged " + level.Key);
        var altered = new UniversalAsmPart(); foreach (uint at in code.Order()) altered.Emit(owner.Word(at) ^ (at == pc + 24 ? 1u : 0), orig: at);
        Check(!UniversalNativeModelTailOwnership.MarkOwned(altered, owner, library.Disc.Exe).Contains(pc), "changed final native consumer cannot inherit original proof");
        var duplicate = new UniversalAsmPart(); foreach (uint at in code.Order()) duplicate.Emit(owner.Word(at), orig: at);
        duplicate.Emit(owner.Word(pc + 24) ^ 1, sourcePc: pc + 24);
        Check(!UniversalNativeModelTailOwnership.MarkOwned(duplicate, owner, library.Disc.Exe).Contains(pc), "one altered copied witness refuses all occurrences");
        Console.WriteLine($"MODEL_TAIL_SOURCE level={level.Key} pc={pc:X8}");
    }
}
Check(proved > 0, "actual native model tail family proved");
Console.WriteLine($"UNIVERSAL_MODEL_TAIL_SOURCE_OK checks={checks} sites={proved}");

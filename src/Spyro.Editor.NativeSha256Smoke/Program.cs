using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks = 0, cases = 0;
void Check(bool okay, string why) { checks++; if (!okay) throw new Exception(why); }
void Refuse(Action action, string why)
{
    try { action(); }
    catch (ArgumentException) { checks++; return; }
    catch (InvalidDataException) { checks++; return; }
    throw new Exception("Expected refusal: " + why);
}
const uint Code = 0x80110000, Input = 0x80120000, Digest = 0x80121000,
    Workspace = 0x80122000, Stack = 0x801FF000, Stop = 0x80010000;
var space = new NativeAddressSpace(NativeAddressKind.LevelData, "sha256-detached-smoke", NativeAddressRevision.Final, 0x10000);
var capacity = new NativeByteExtent(space, 0x100, 0x2000);
var kernel = NativeSha256Kernel.Compile(capacity, Code, Input, 1024, Digest, Workspace);
byte[] bytes = kernel.CopyBytes();
Check(kernel.Owner.Space == space && kernel.Owner.Offset == 0x100 && kernel.Owner.ByteLength == bytes.Length && bytes.Length <= 0x2000,
    "bounded exact owner includes code and constants");
Check(kernel.CodeCapacityByteLength == 0x2000, "whole conservative code capacity remains visible to composing callers");
Check(kernel.EntryPc == Code && kernel.CallerInputAddress == Input && kernel.MaximumInputBytes == 1024 &&
    kernel.DigestAddress == Digest && kernel.WorkspaceAddress == Workspace && kernel.WorkspaceByteLength == 288 && kernel.StackByteLength == 64,
    "fixed runtime ranges and bounded ABI");
Check(!kernel.NativeResidenceIssued && !kernel.PublicationApproved && !kernel.CardIoApproved, "kernel issues no runtime or publication authority");
bytes[0] ^= 1; Check(!bytes.SequenceEqual(kernel.CopyBytes()), "immutable code snapshot"); bytes = kernel.CopyBytes();
byte[] Read(RenderProbeCpu cpu, uint at, int n) => Enumerable.Range(0, n).Select(i => (byte)cpu.Get(at + (uint)i, 1)).ToArray();
byte[] poison = Enumerable.Range(0, 1056).Select(i => (byte)(i * 31 + 9)).ToArray();
RenderProbeCpu Cpu(byte[] input)
{
    var cpu = new RenderProbeCpu { StrictSignedArithmetic = true };
    for (int i = 1; i < 32; i++) cpu.Regs[i] = 0xB3000000u + (uint)i * 0x10101;
    cpu.Hi = 0x13245678; cpu.Lo = 0x87654321;
    cpu.Copy(Code - 16, Enumerable.Repeat((byte)0xE7, bytes.Length + 32).ToArray()); cpu.Copy(Code, bytes);
    cpu.Copy(Input - 16, poison); cpu.Copy(Input, input);
    cpu.Copy(Digest - 16, Enumerable.Repeat((byte)0xD5, 64).ToArray());
    cpu.Copy(Workspace - 16, Enumerable.Repeat((byte)0xC4, kernel.WorkspaceByteLength + 32).ToArray());
    cpu.Copy(Stack - 80, Enumerable.Repeat((byte)0xB3, 96).ToArray());
    cpu.Regs[4] = Input; cpu.Regs[5] = (uint)input.Length; cpu.Regs[29] = Stack; cpu.Regs[31] = Stop;
    return cpu;
}
int[] preserved = [4, 5, 16, 17, 18, 19, 20, 21, 22, 23, 26, 27, 28, 29, 30, 31];
void Validate(byte[] input, string label, string? golden = null)
{
    cases++;
    var cpu = Cpu(input); uint[] before = cpu.Regs.ToArray();
    byte[] source = Read(cpu, Input - 16, 1056);
    cpu.Run(Code, Stop);
    byte[] actual = Read(cpu, Digest, 32), expected = SHA256.HashData(input);
    Check(cpu.Regs[2] == 0 && actual.SequenceEqual(expected), label + " native digest matches independent .NET oracle");
    if (golden != null) Check(Convert.ToHexString(actual) == golden, label + " standard digest golden");
    Check(preserved.All(r => before[r] == cpu.Regs[r]) && cpu.Hi == 0x13245678 && cpu.Lo == 0x87654321,
        label + " argument/callee/platform/HI/LO preservation");
    Check(Read(cpu, Input - 16, 1056).SequenceEqual(source) && Read(cpu, Code, bytes.Length).SequenceEqual(bytes), label + " input and code immutable");
    Check(cpu.Calls.Count == 0 && cpu.Writes.All(a => a >= Digest && a < Digest + 32 ||
        a >= Workspace && a < Workspace + kernel.WorkspaceByteLength || a >= Stack - 64 && a < Stack), label + " complete write footprint and no helpers");
    foreach (var (at, length, value) in new (uint, int, byte)[] {
        (Code - 16, 16, 0xE7), (Code + (uint)bytes.Length, 16, 0xE7),
        (Digest - 16, 16, 0xD5), (Digest + 32, 16, 0xD5),
        (Workspace - 16, 16, 0xC4), (Workspace + (uint)kernel.WorkspaceByteLength, 16, 0xC4),
        (Stack - 80, 16, 0xB3), (Stack, 16, 0xB3) })
        Check(Read(cpu, at, length).All(b => b == value), label + " canary at " + at.ToString("X8"));
    Check(cpu.Reads.Where(a => a >= Input && a < Input + 1024).SequenceEqual(
        Enumerable.Range(0, input.Length).Select(i => Input + (uint)i)), label + " exact ordered input reads and no over-read");
    Check(cpu.Reads.All(a => a >= Input && a < Input + input.Length ||
        a >= Workspace && a + 4 <= Workspace + kernel.WorkspaceByteLength ||
        a >= Stack - 64 && a + 4 <= Stack || a >= Code && a + 4 <= Code + bytes.Length), label + " complete data read footprint");
    Check(cpu.Reads.Count(a => a >= Code && a < Code + bytes.Length) == ((input.Length + 72) / 64) * 64,
        label + " all 64 owned round constants read by native loop per padded block");
}
Validate([], "empty", "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855");
Validate(Encoding.ASCII.GetBytes("abc"), "abc", "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD");
Validate(Encoding.ASCII.GetBytes("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq"), "standard multiblock",
    "248D6A61D20638B8E5C026930C3E6039A33CE45964FF2167F6ECEDD419DB06C1");
var random = new Random(721946);
foreach (int length in new[] { 1, 2, 7, 31, 55, 56, 63, 64, 65, 119, 120, 127, 128, 131, 227, 656, 1023, 1024 })
{
    byte[] data = new byte[length]; random.NextBytes(data); Validate(data, "random length " + length);
}
foreach (byte fill in new byte[] { 0, 0x80, 0xFF }) Validate(Enumerable.Repeat(fill, 656).ToArray(), "envelope repeated " + fill);
for (int i = 0; i < 12; i++) { byte[] data = new byte[random.Next(1025)]; random.NextBytes(data); Validate(data, "random case " + i); }
foreach (var (a0, a1, sp) in new (uint, uint, uint)[] {
    (Input + 4, 3, Stack), (0, 3, Stack), (0xA0120000, 3, Stack), (Input, 1025, Stack),
    (Input, uint.MaxValue, Stack), (Input, 0x80000000, Stack),
    (Input, 3, 0), (Input, 3, 0x80000038), (Input, 3, 0x80200008), (Input, 3, Stack + 4),
    (Input, 3, Code + 64), (Input, 3, Code + 0x2000), (Input, 3, Input + 64), (Input, 3, Input + 1024),
    (Input, 3, Digest + 32), (Input, 3, Workspace + 64), (Input, 3, Workspace + 288) })
{
    var cpu = Cpu([1, 2, 3]); cpu.Regs[4] = a0; cpu.Regs[5] = a1; cpu.Regs[29] = sp;
    uint[] before = cpu.Regs.ToArray(); cpu.Run(Code, Stop);
    Check(cpu.Regs[2] == 1 && cpu.Writes.Count == 0 && cpu.Reads.Count == 0, "invalid runtime arguments refuse before any data access");
    Check(preserved.All(r => cpu.Regs[r] == before[r]) && cpu.Hi == 0x13245678 && cpu.Lo == 0x87654321,
        "invalid argument register preservation");
}
foreach (var (code, input, max, digest, work) in new (uint, uint, int, uint, uint)[] {
    (Code + 1, Input, 1024, Digest, Workspace), (Code, Input + 1, 1024, Digest, Workspace),
    (Code, Input, 1024, Digest + 1, Workspace), (Code, Input, 1024, Digest, Workspace + 1),
    (0x7FFFFFFC, Input, 1024, Digest, Workspace), (0x801FF000, Input, 1024, Digest, Workspace),
    (Code, 0x801FFC04, 1024, Digest, Workspace), (Code, Input, 1024, 0x801FFFF0, Workspace),
    (Code, Input, 1024, Digest, 0x801FFF00), (Code, uint.MaxValue - 3, 1024, Digest, Workspace),
    (Code, Input, 0, Digest, Workspace), (Code, Input, -1, Digest, Workspace), (Code, Input, 1025, Digest, Workspace),
    (Code, Code, 1024, Digest, Workspace), (Code, Input, 1024, Code, Workspace),
    (Code, Input, 1024, Digest, Code), (Code, Input, 1024, Input, Workspace),
    (Code, Input, 1024, Digest, Input), (Code, Input, 1024, Digest, Digest) })
    Refuse(() => NativeSha256Kernel.Compile(capacity, code, input, max, digest, work), "hostile compile geometry");
Refuse(() => NativeSha256Kernel.Compile(null!, Code, Input, 1024, Digest, Workspace), "null capacity");
Refuse(() => NativeSha256Kernel.Compile(new(space, 0, 4), Code, Input, 1024, Digest, Workspace), "insufficient code capacity");
Refuse(() => NativeSha256Kernel.Compile(new(space, 1, 0x2000), Code, Input, 1024, Digest, Workspace), "misaligned owner");
Refuse(() => NativeSha256Kernel.Compile(new(new(NativeAddressKind.LevelData, "old", NativeAddressRevision.Original, 0x2000), 0, 0x2000),
    Code, Input, 1024, Digest, Workspace), "original owner");
Refuse(() => NativeSha256Kernel.Compile(new(new(NativeAddressKind.WadFile, "wrong", NativeAddressRevision.Final, 0x2000), 0, 0x2000),
    Code, Input, 1024, Digest, Workspace), "wrong address domain");
// Relocation, minimal input capacity and exact RAM/stack adjacency use new
// emitted code. No native instruction is rewritten to make these cases pass.
foreach (var (code, input, max, digest, workspace, stack) in new (uint, uint, int, uint, uint, uint)[] {
    (0x80000000, 0x80004000, 1, 0x80005000, 0x80006000, 0x80200000),
    (0x801FE000, 0x80000040, 1024, 0x80000020, 0x80001000, 0x80000020),
    (0x80110000, 0x80120000, 55, 0x80121000, 0x801FFEE0, 0x801FFEE0),
    (0x80110000, 0x80120000, 56, 0x801FFF00, 0x80122000, 0x801FFF60),
    (0x80110000, 0x801FFC00, 1024, 0x80121000, 0x80122000, 0x801FFC00) })
{
    // The second frame deliberately falls below RAM and must fail without
    // writes even though every compiled range itself is valid.
    var relocated = NativeSha256Kernel.Compile(capacity, code, input, max, digest, workspace);
    var cpu = new RenderProbeCpu { StrictSignedArithmetic = true }; byte[] data = new byte[max]; random.NextBytes(data);
    cpu.Copy(code, relocated.CopyBytes()); cpu.Copy(input, data);
    cpu.Regs[4] = input; cpu.Regs[5] = (uint)max; cpu.Regs[29] = stack; cpu.Regs[31] = Stop;
    cpu.Run(code, Stop);
    if (stack < 0x80000040) Check(cpu.Regs[2] == 1 && cpu.Writes.Count == 0, "underflowing frame refusal with valid compiled edge ranges");
    else Check(cpu.Regs[2] == 0 && Read(cpu, digest, 32).SequenceEqual(SHA256.HashData(data)) && cpu.Regs[29] == stack,
        "native relocation and adjacent bounded memory ranges");
}
Console.WriteLine($"NATIVE_SHA256_OK checks={checks} nativeCases={cases} codeBytes={bytes.Length} workspaceBytes={kernel.WorkspaceByteLength} stackBytes={kernel.StackByteLength} sourceReads=0 imageWrites=0 cardWrites=0");

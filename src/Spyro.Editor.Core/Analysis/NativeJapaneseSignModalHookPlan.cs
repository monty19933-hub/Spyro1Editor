using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeJapaneseSignModalHookSmoke")]

namespace Spyro.Editor.Core.Analysis;

internal enum NativeSignModalHookRole
{
    DrawUnknownStateGate, UpdateUnknownStateGate, ExistingModalDrawCall,
    FirstFrameBackdropDrain, LaterFrameUiQueueSeal, PauseRestoreDrain, InventoryRestoreDrain
}
internal sealed record NativeSignModalEvidence(string Role, uint Pc, int ByteLength, string Sha256);
internal sealed record NativeSignFontTransferRequirement(NativeSignFontProtocolPhase Phase,
    uint NativeService, NativeJapaneseFontResidentOwner CpuOwner, int WordX, int Y, int WordWidth, int Height)
{
    internal int ByteLength => checked(WordWidth * Height * 2);
    internal bool NativeCallInstalled => false;
}
internal sealed class NativeSignModalHookSite
{
    private readonly byte[] _before;
    internal NativeSignModalHookSite(NativeSignModalHookRole role, uint pc, byte[] before, uint ordinaryTarget, uint fallthrough)
    { Role = role; Pc = pc; _before = before.ToArray(); OrdinaryTarget = ordinaryTarget; Fallthrough = fallthrough; }
    internal NativeSignModalHookRole Role { get; }
    internal uint Pc { get; }
    internal uint OrdinaryTarget { get; }
    internal uint Fallthrough { get; }
    internal byte[] CopyBeforeBytes() => _before.ToArray();
    internal string BeforeSha256 => Convert.ToHexStringLower(SHA256.HashData(_before));
}

/// <summary>
/// Source-bound candidate boundaries, not emitted hooks or a native modal owner.
/// The resident atlas/backup issuer is retained by identity. Initial SDK table
/// bindings are evidence, not proof of the current hardware/runtime table.
/// No caller may turn this into an installation by claiming a DrawSync request
/// completed, or by treating the tested abstract protocol as native control flow.
/// </summary>
internal sealed class NativeJapaneseSignModalHookPlan
{
    private const uint ExeBase = 0x8000F800;
    private const string ExeHash = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private readonly byte[] _executable;
    private NativeJapaneseSignModalHookPlan(byte[] executable, NativeJapaneseSignFontResidentPlan? resident, bool sourceBound)
    {
        if (executable.Length != 0x66000 || sourceBound != (resident is not null))
            throw new InvalidDataException("Modal hook capture needs the complete EXE and actual resident source ancestry.");
        _executable = executable.ToArray(); Resident = resident; SourceBound = sourceBound;
        if (sourceBound)
        {
            if (Hash(executable) != ExeHash || resident is not { SourceBound: true } ||
                resident.UsaSourceImageSha256 != NativeMobySourceInventory.OriginalImageSha256 ||
                resident.Atlas.FinalExtent.ByteLength != 0xA000 || resident.Backup.FinalExtent.ByteLength != 0xA000 ||
                resident.Atlas.ModeledNormalRamEnd != resident.Backup.ModeledNormalRamStart ||
                resident.Backup.ModeledNormalRamEnd != resident.Memory.FinalSceneStart)
                throw new InvalidDataException("Modal source and explicit nonregistered atlas/backup owners differ.");
            resident.RequireFinal(resident.CopyLevelData());
            foreach (var evidence in Evidence)
                if (Hash(Slice(evidence.Pc, evidence.ByteLength)) != evidence.Sha256)
                    throw new InvalidDataException("Native modal/GPU beforeimage changed: " + evidence.Role);
        }
        var specs = new (NativeSignModalHookRole Role, uint Pc, uint First, uint Delay, uint Target, uint Fallthrough)[]
        {
            (NativeSignModalHookRole.DrawUnknownStateGate,0x8001EF40,0x14620080,0,0x8001F144,0x8001EF48),
            (NativeSignModalHookRole.UpdateUnknownStateGate,0x80033A14,0x14620086,0,0x80033C30,0x80033A1C),
            (NativeSignModalHookRole.ExistingModalDrawCall,0x8001EE34,0x0C006903,0,0x8001A40C,0x8001EE3C),
            (NativeSignModalHookRole.FirstFrameBackdropDrain,0x8001A5C0,0x0C017DD9,0x00002021,0x8005F764,0x8001A5C8),
            (NativeSignModalHookRole.LaterFrameUiQueueSeal,0x8001C598,0x0C0059E1,0x00002021,0x80016784,0x8001C5A0),
            (NativeSignModalHookRole.PauseRestoreDrain,0x8002C57C,0x0C017DD9,0x00002021,0x8005F764,0x8002C584),
            (NativeSignModalHookRole.InventoryRestoreDrain,0x8002C7FC,0x0C017DD9,0x00002021,0x8005F764,0x8002C804)
        };
        List<NativeSignModalHookSite> sites = [];
        foreach (var spec in specs)
        {
            byte[] before = Slice(spec.Pc, 8).ToArray();
            if (Word(before,0) != spec.First || Word(before,4) != spec.Delay)
                throw new InvalidDataException("Modal candidate site lost its instruction/delay beforeimage.");
            sites.Add(new(spec.Role,spec.Pc,before,spec.Target,spec.Fallthrough));
        }
        Sites = sites.AsReadOnly();
        Transfers = Array.AsReadOnly(resident is null ? [] : new NativeSignFontTransferRequirement[]
        {
            new(NativeSignFontProtocolPhase.CaptureEnqueue,0x8005FA8C,resident.Backup,512,472,512,40),
            new(NativeSignFontProtocolPhase.AtlasEnqueue,0x8005FA28,resident.Atlas,512,472,512,40),
            new(NativeSignFontProtocolPhase.RestoreEnqueue,0x8005FA28,resident.Backup,512,472,512,40)
        });
        // Source initial SDK dispatch table. Its writable runtime identity and
        // IRQ/callback service closure remain admission requirements, not facts.
        if (At(0x80074A5C) != 0x80074A1C || At(0x80074A24) != 0x80061820 ||
            At(0x80074A38) != 0x80061470 || At(0x80074A3C) != 0x80061234 || At(0x80074A58) != 0x80061F48 ||
            At(0x80061F78) != 0x14400040 || At(0x80061F7C) != 0x2402FFFF ||
            At(0x80061FAC) != 0x14400033 || At(0x80061FB0) != 0x2402FFFF ||
            At(0x80061FEC) != 0x1040FFED || At(0x80061FF0) != 0x00001021 ||
            At(0x80061ADC) != 0x3042003F || At(0x8006185C) != 0x144000A0 || At(0x80061860) != 0x2402FFFF)
            throw new InvalidDataException("Native SDK queue/drain success and failure semantics changed.");
        PlanSha256 = Hash(System.Text.Encoding.UTF8.GetBytes($"jp-sign-modal-sites-v1|{SourceBound}|{Hash(_executable)}|{resident?.PlanSha256 ?? "fixture"}|no-hook-emission|no-complete-lifetime"));
    }
    internal bool SourceBound { get; }
    internal NativeJapaneseSignFontResidentPlan? Resident { get; }
    internal IReadOnlyList<NativeSignModalHookSite> Sites { get; }
    internal IReadOnlyList<NativeSignFontTransferRequirement> Transfers { get; }
    internal string PlanSha256 { get; }
    internal bool ExactCandidateBeforeimagesProven => SourceBound;
    internal bool InitialSdkReturnSemanticsSourceBound => SourceBound;
    internal bool CompleteModalLifetimeProven => false;
    internal bool LiveGpuDriverIdentityProven => false;
    internal bool ControllerRamAllocated => false;
    internal bool NativeHooksEmitted => false;
    internal bool InstallationApproved => false;
    internal bool PublicationApproved => false;
    internal byte[] CopyExecutable() => _executable.ToArray();
    internal byte[] CopyEvidence(NativeSignModalEvidence evidence)
    {
        if (!Evidence.Contains(evidence)) throw new InvalidDataException("Unknown modal source evidence extent.");
        return Slice(evidence.Pc, evidence.ByteLength).ToArray();
    }
    internal void RequireBeforeimages(ReadOnlySpan<byte> current)
    { if (!current.SequenceEqual(_executable)) throw new InvalidDataException("Modal source requires the exact complete captured original EXE, not selected accepted sites."); }
    internal void RequireResidentBody(ReadOnlySpan<byte> final)
    {
        if (!SourceBound || Resident is null) throw new InvalidDataException("Fixture has no native resident allocation authority.");
        Resident.RequireFinal(final);
    }
    internal void RequireInstallation() => throw new InvalidDataException(
        "Modal hook boundaries are not a complete lifecycle: controller/code ownership, mode17 entry/update/draw, GPU results/callbacks, primitive capacity, every exit and level-generation invalidation remain unproved.");

    internal static NativeJapaneseSignModalHookPlan Capture(NativeRegionalSourceSession usa, NativeJapaneseSignFontResidentPlan resident)
    {
        ArgumentNullException.ThrowIfNull(usa); ArgumentNullException.ThrowIfNull(resident);
        if (usa.Revision != NativeDiscRevision.UsaScus94228 || !resident.SourceBound)
            throw new InvalidDataException("Modal capture needs actual exact-USA and source-issued resident owners.");
        var result = new NativeJapaneseSignModalHookPlan(usa.ReadExecutableRange(0,usa.ExecutableByteLength),resident,true);
        usa.VerifyUnchanged(); return result;
    }
    internal static NativeJapaneseSignModalHookPlan DecodeFixture(byte[] exe) => new(exe,null,false);
    private ReadOnlySpan<byte> Slice(uint pc,int length) => _executable.AsSpan(checked((int)(pc-ExeBase)),length);
    private uint At(uint pc) => Word(Slice(pc,4),0);
    private static uint Word(ReadOnlySpan<byte> bytes,int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    internal static IReadOnlyList<NativeSignModalEvidence> Evidence { get; } = Array.AsReadOnly(new[]
    {
        new NativeSignModalEvidence("draw-state-ladder",0x8001EDDC,0x254,"37980df646580950e30917cdd6faba99605bac020d7c803660d050a850d44021"),
        new NativeSignModalEvidence("update-state-ladder",0x80033884,0x1D0,"7e6bf1d3f61b128830ed268e38904daea449c60e10ee6a555d81f3abf52757ad"),
        new NativeSignModalEvidence("update-unconditional-prelude",0x8003385C,0x28,"828d098a93851e34f84ef2183cdafff44aa953692bf3024be5040830a412d2c8"),
        new NativeSignModalEvidence("update-unconditional-tail",0x80033C30,0x20,"a36f65ee9d69cf4ea23751c14882804ad4036d9e73a4cd108093baa6ecc1355c"),
        new NativeSignModalEvidence("modal-first-frame",0x8001A40C,0x1D4,"73d93b970193080afc1dc0756365bfb0b2f163bf9beef24050e2ac0c2808f831"),
        new NativeSignModalEvidence("modal-later-frame-arena",0x8001A5E0,0x74,"3ac202d76b4d36d04f17512ad3c18f524a0a42bd43536ba7b72b7ea31e1c226d"),
        new NativeSignModalEvidence("modal-queue-submit-and-tail",0x8001C598,0xFC,"17146e7dfa88786dfe746aed36ee70635a8dbe10bd6631e04c9fff1a7705d72c"),
        new NativeSignModalEvidence("ui-queue-seal",0x80016854,0x88,"28731958b8d9df47f95b90dd72e83f3f8b05cd7a351630aa5e0d80e7384e668a"),
        new NativeSignModalEvidence("pause-complete-resume",0x8002C534,0xE4,"f720db0b746c096e733a92e8365708031bc7d7efe78008597f70f983bebac967"),
        new NativeSignModalEvidence("inventory-complete-resume",0x8002C7BC,0xA0,"4d2b9cdb3e74d1ccf47abdeeaea23ea8fc129cc43e00172cec91fa5103cfdbba"),
        new NativeSignModalEvidence("sdk-initial-table-and-pointer",0x80074A1C,0x44,"4e585e686756634a746ac3a80e62e921711fa8c4f09a9690d14e4822360f6d1b"),
        new NativeSignModalEvidence("sdk-queue-admission",0x80061820,0x2E0,"9f18ab89a978fe204ad08683aae8584757f5dec1c613275196e7119bc205d1ed"),
        new NativeSignModalEvidence("sdk-blocking-drain",0x80061F48,0x148,"6edb067315c1754d284d951ce9343367f3593f742b76f5c89f59d5f35801f12c"),
        new NativeSignModalEvidence("sdk-timeout-reset-path",0x80062090,0x1A0,"7bef961d538a8d9ee19a43bebcf616f04b6611877bba6c0682f9aeeab06365ac"),
        new NativeSignModalEvidence("native-sync-wrapper",0x8005F764,0x6C,"0e573f70016f10b87ac196ff7fa304e28d16549d9b1c5ef11f756f248e12689d"),
        new NativeSignModalEvidence("native-load-store-wrappers",0x8005FA28,0xC8,"982cbdc534ffcabfc2a1577aacd98bc64f773bbfbbd402d1b08ce66dbdb5be84")
    });
}

internal enum NativeSignFontProtocolPhase
{
    EnterDrain, CaptureEnqueue, CaptureDrain, AtlasEnqueue, AtlasDrain, Active,
    ExitDrain, RestoreEnqueue, RestoreDrain, Restored, Faulted, Invalidated
}

/// <summary>
/// Detached, immutable authoring protocol. It allocates NO native state and does
/// not certify that any caller performed the modeled operation. A queue depth
/// 0..63 is admission only; each dependent operation still needs a successful
/// blocking drain. Failure never silently restores world-render authority.
/// </summary>
internal sealed class NativeSignFontProtocol
{
    private NativeSignFontProtocol(ulong generation,NativeSignFontProtocolPhase phase)
    { Generation=generation; Phase=phase; }
    internal ulong Generation { get; }
    internal NativeSignFontProtocolPhase Phase { get; }
    internal bool MayIssueGlyphPackets => Phase==NativeSignFontProtocolPhase.Active;
    internal bool MayResumeWorld => Phase==NativeSignFontProtocolPhase.Restored;
    internal static NativeSignFontProtocol Begin(ulong generation) => generation==0
        ? throw new InvalidDataException("An explicit nonzero level/modal generation is required.")
        : new(generation,NativeSignFontProtocolPhase.EnterDrain);
    internal NativeSignFontProtocol CompleteRequest(ulong generation,int result)
    {
        RequireGeneration(generation);
        bool queue = Phase is NativeSignFontProtocolPhase.CaptureEnqueue or NativeSignFontProtocolPhase.AtlasEnqueue or NativeSignFontProtocolPhase.RestoreEnqueue;
        var next = Phase switch
        {
            NativeSignFontProtocolPhase.EnterDrain=>NativeSignFontProtocolPhase.CaptureEnqueue,
            NativeSignFontProtocolPhase.CaptureEnqueue=>NativeSignFontProtocolPhase.CaptureDrain,
            NativeSignFontProtocolPhase.CaptureDrain=>NativeSignFontProtocolPhase.AtlasEnqueue,
            NativeSignFontProtocolPhase.AtlasEnqueue=>NativeSignFontProtocolPhase.AtlasDrain,
            NativeSignFontProtocolPhase.AtlasDrain=>NativeSignFontProtocolPhase.Active,
            NativeSignFontProtocolPhase.ExitDrain=>NativeSignFontProtocolPhase.RestoreEnqueue,
            NativeSignFontProtocolPhase.RestoreEnqueue=>NativeSignFontProtocolPhase.RestoreDrain,
            NativeSignFontProtocolPhase.RestoreDrain=>NativeSignFontProtocolPhase.Restored,
            _=>throw new InvalidDataException("No GPU request is owned in this protocol phase.")
        };
        return new(Generation,(queue?result is >=0 and <64:result==0)?next:NativeSignFontProtocolPhase.Faulted);
    }
    internal NativeSignFontProtocol Close(ulong generation)
    {
        RequireGeneration(generation);
        if(Phase!=NativeSignFontProtocolPhase.Active)throw new InvalidDataException("Only the sole active modal can start normal restoration.");
        return new(Generation,NativeSignFontProtocolPhase.ExitDrain);
    }
    internal NativeSignFontProtocol UnexpectedExit(ulong generation)
    { RequireGeneration(generation); return new(Generation,NativeSignFontProtocolPhase.Faulted); }
    internal NativeSignFontProtocol InvalidateForReplacement(ulong nextGeneration)
    {
        if(nextGeneration<=Generation)throw new InvalidDataException("Level replacement must not reuse an old backup generation.");
        return new(nextGeneration,NativeSignFontProtocolPhase.Invalidated);
    }
    private void RequireGeneration(ulong generation)
    { if(generation!=Generation)throw new InvalidDataException("A stale modal/level generation cannot complete or restore this owner."); }
}

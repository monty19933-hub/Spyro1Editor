using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeMobyBehaviorFragmentEvidence(string Id,uint SourcePc,int ByteLength,string Sha256);
public sealed record NativeMobyBehaviorControlEvidence(string Kind,uint SourcePc,uint? TargetPc,uint SourceWord,uint DelayWord,bool HasDelaySlot);
public sealed record NativeMobyBehaviorLiteralEvidence(uint SourcePc,uint Word);
public sealed record NativeMobyBehaviorCallEvidence(uint RuntimePc,int ExecutableOffset,string EntryInstructionSha256);

/// <summary>
/// Exact native code and relocation obligations. No active dispatch roots, code
/// destination, callback implementation, runtime stack reservation or publication
/// is issued here. The internal linker refuses every missing external binding.
/// </summary>
public sealed class NativeDarkHollowWizardBehaviorSource
{
    private readonly NativeMipsBehaviorFragmentPackage _main,_spawn;
    private readonly IReadOnlyDictionary<string,byte[]> _bytes;
    private NativeDarkHollowWizardBehaviorSource(byte[] wizard,byte[] dark,byte[] executable)
    {
        Require(wizard,"5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1");
        Require(dark,"9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32");
        Require(executable,"a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998");
        Require(dark.AsSpan(0x408,0xF4),"56d3261e7e3843e492530f9c4470b1818b3474d275d41325c8de2870a7530a7c");
        Require(wizard.AsSpan(0xD70,0xA0),"352ef40d508106eee9499c0786a0a286bc2e58928df34e1ecba77364a73d8904");
        Require(wizard.AsSpan(0xC354,0x4C),"d8c482d83bfe5091eaa55ab056f74ca2a01f96e846e0aaffcc51fd780ccf129e");
        Require(dark.AsSpan(0x7EDC,0x4C),"93e30da4cb5ceba3232f2ef282fff31f44c453a6833e8aa70f25d5ca7f0f0a36");
        NativeAddressSpace donor=new(NativeAddressKind.LevelData,"wizard-overlay:"+Hash(wizard),NativeAddressRevision.Original,wizard.Length);
        NativeAddressSpace exe=new(NativeAddressKind.ExecutableFile,"retail-exe:"+Hash(executable),NativeAddressRevision.Original,executable.Length);
        byte[] Read(NativeByteExtent extent)=>extent.Space==donor?wizard.AsSpan((int)extent.Offset,(int)extent.ByteLength).ToArray():
            extent.Space==exe?executable.AsSpan((int)extent.Offset,(int)extent.ByteLength).ToArray():throw new InvalidDataException("Unbound native behavior source domain.");
        (string Id,uint Pc,int Length,string Hash)[] main=
        [
            ("wizard-death-helper",0x8007D118,0x1C,"cb6c9b201f8e3bae8282fe1805342ed76ae068cc5538de7d027e68f13c80badb"),
            ("lightning-main",0x8007EAD0,0x1A0,"1c0de3f70b36876cbf526730df0219c206f9e5d5621551bd8abb062c83609fe5"),
            ("wizard-main",0x80082818,0x668,"fd6623d1a6520736d6fd684a1c72b00a5a01727be764f7395cbe75c466af6007"),
            ("common-842cc",0x800842CC,0x24,"749bb5e1e951b80c87b1b0ec16abeeb9e98b4f08ed097739df571f5ea5d13b3f"),
            ("common-8434c",0x8008434C,0x14,"6da3bd3db238aa4de38d5d16a9a61b0c2799a01d05c2071aa409abe3857e3cad"),
            ("common-85a4c",0x80085A4C,0x4C,"3beab1570e37a77aae8d809b43dfa7e596057795f1bb45e0d4ecb857642ff04a"),
            ("common-delete",0x80086A50,0x10,"5a4a9b563d9e2e78be98a498afcead4373655c5eb8044e84bd697f1c310cb172")
        ];
        (string Id,uint Pc,int Length,string Hash)[] spawn=
        [
            ("spawn26-native-preamble",0x80086DD8,0x94,"92b99a51a4ea53531a711626d9aa1d8f7fc7a5be120cfbf14255f95c68b080b4"),
            ("spawn26-initializer",0x800870F0,0x230,"31e3328b9f94ee5fc424bb4f566048b8a40efacbd99ed634d6cbe70bb3fd97b4"),
            ("spawn26-finalizer",0x80087B08,0x38,"731646e82a5a26b929aff89ad7ae822be66c7fd56b920fb9eb8a7d83ee4f571a")
        ];
        NativeMipsBehaviorFragmentSource Make((string Id,uint Pc,int Length,string Hash) f)
        {
            int offset=checked((int)(f.Pc-0x8007AA38));Require(wizard.AsSpan(offset,f.Length),f.Hash);
            return new(f.Id,f.Pc,new("native",new(donor,offset,f.Length),f.Hash));
        }
        _main=NativeMipsBehaviorFragmentCodec.Capture(main.Select(Make),[0x80082818,0x8007EAD0],[InterceptedExitPc],Read);
        _spawn=NativeMipsBehaviorFragmentCodec.Capture(spawn.Select(Make),[0x80086DD8,0x800870F0],[],Read);
        if(_main.InstructionCount!=558||MainCallbackCount!=6||SpawnCallbackCount!=1||MainStackLoads!=0||MainMaximumStackStoreEnd!=0x34)
            throw new InvalidDataException("Native Wizard main/spawn/stack control frontier changed.");
        uint[] expectedCallbacks=[0x8007EBDC,0x8007EC60,0x80082840,0x80082DB4,0x80082DF0,0x80082E38];
        if(!_main.Transfers.Where(t=>t.Kind==NativeMipsBehaviorTransferKind.CallbackRegister).Select(t=>t.SourcePc).SequenceEqual(expectedCallbacks)||
            _spawn.Transfers.Single(t=>t.Kind==NativeMipsBehaviorTransferKind.CallbackRegister).SourcePc!=0x8008728C)
            throw new InvalidDataException("Native callback source sites changed.");
        // Full fragment certificates above bind the intact flags/children and
        // guard. Explicit words make a later accidental adapted-profile reuse
        // fail at its semantic boundary, not merely produce another report.
        uint W(uint pc)=>BinaryPrimitives.ReadUInt32LittleEndian(wizard.AsSpan((int)(pc-0x8007AA38)));
        if(W(0x8007EBC0)!=0x28420015||W(0x8007EB88)!=0x3C060002||W(0x8007EB90)!=0x34C60020||W(0x8007EBE8)!=0x2A220004)
            throw new InvalidDataException("Native impact dynamic guard/electric flags changed.");
        MainStackAddressWords=Array.AsReadOnly(_main.Fragments.SelectMany(f=>Enumerable.Range(0,f.ByteLength/4)
            .Select(i=>new NativeMobyBehaviorLiteralEvidence(f.Source.SourcePc+(uint)i*4,W(f.Source.SourcePc+(uint)i*4))))
            .Where(l=>(l.Word>>26)==9&&((l.Word>>21)&31)==29&&((l.Word>>16)&31)!=29).ToArray());
        if(MainStackAddressWords.Count!=1||MainStackAddressWords[0].SourcePc!=0x8007EB34||MainStackAddressWords[0].Word!=0x27B20068)
            throw new InvalidDataException("Native helper-derived stack address obligation changed.");
        var all=_main.Fragments.Concat(_spawn.Fragments).ToArray();
        _bytes=all.ToDictionary(f=>f.Source.Id,f=>f.CopyBytes(),StringComparer.Ordinal);
        Fragments=Array.AsReadOnly(all.Select(f=>new NativeMobyBehaviorFragmentEvidence(f.Source.Id,f.Source.SourcePc,f.ByteLength,f.Source.Read.ExpectedSha256)).ToArray());
        ControlTransfers=Array.AsReadOnly(_main.Transfers.Concat(_spawn.Transfers).OrderBy(t=>t.SourcePc)
            .Select(t=>new NativeMobyBehaviorControlEvidence(t.Kind.ToString(),t.SourcePc,t.TargetPc,t.SourceWord,t.DelayWord,t.HasDelaySlot)).ToArray());
        LiteralWords=Array.AsReadOnly(all.SelectMany(f=>Enumerable.Range(0,f.ByteLength/4)
            .Select(i=>new NativeMobyBehaviorLiteralEvidence(f.Source.SourcePc+(uint)i*4,NativeMipsBehaviorFragmentCodec.Word(f.CopyBytes(),i*4))))
            .Where(l=>(l.Word>>26)==15).ToArray());
        var calls=_main.Transfers.Concat(_spawn.Transfers).Where(t=>t.Kind==NativeMipsBehaviorTransferKind.Call26).Select(t=>t.TargetPc!.Value).Distinct().Order().ToArray();
        DirectExeCalls=Array.AsReadOnly(calls.Select(pc=>
        {
            if(pc<0x80010000||pc>=0x80075800)throw new InvalidDataException("Native direct call has an unowned executable entry.");
            int offset=(int)(pc-0x8000F800);return new NativeMobyBehaviorCallEvidence(pc,offset,Hash(executable.AsSpan(offset,4)));
        }).ToArray());
        List<string> unresolved=ControlTransfers.Where(t=>t.Kind==nameof(NativeMipsBehaviorTransferKind.CallbackRegister))
            .Select(t=>$"Callback at {t.SourcePc:X8}: source argument and selected effect/spawn ABI require explicit destination binding.").ToList();
        unresolved.AddRange(_main.Transfers.Where(t=>t.TargetPc is uint target&&!_main.OwnsPc(target)&&t.Kind!=NativeMipsBehaviorTransferKind.Call26)
            .Concat(_spawn.Transfers.Where(t=>t.TargetPc is uint target&&!_spawn.OwnsPc(target)&&t.Kind!=NativeMipsBehaviorTransferKind.Call26))
            .Select(t=>$"{t.Kind} at {t.SourcePc:X8} to {t.TargetPc:X8}: explicit source-certified continuation/dispatch binding required."));
        unresolved.AddRange(DirectExeCalls.Select(c=>$"EXE {c.RuntimePc:X8}: entry bytes source-bound; transitive global/GTE/resource consumers not closed."));
        unresolved.AddRange([
            "Main class11B/class26 dispatch roots are not activated or allocated.",
            "Native donor exit86D8C must bind an owned context-restoring adapter before Dark82914; donor cursor load cannot be copied.",
            "Spawn26 preamble branches/falls into the broad native class dispatcher; specializing it requires code-owned class26-only input validation.",
            "Particle07/41 native spawn/update fragments and callback dispatch integration remain missing.",
            "Four impact children, dynamic capacity-active>=21 per child, model states0/1/2/3 and native20020 electric flags must remain intact.",
            "Source literal words are preserved, not a proof that every global/data pointer consumer is closed.",
            "Adapter uses an owned nominal2B0 frame; actual runtime stack/transient memory budget remains unproved.",
            "Lightning derives s2=sp+68 at7EB34 for native vector helpers; direct-store end34 does not bound helper writes or prove stack peak.",
            "Source-issued model/player/sound/texture/scene ownership and valid mover/collider placement must join one final active closure."
        ]);
        UnresolvedObligations=Array.AsReadOnly(unresolved.ToArray());
        PackageSha256=Hash(Encoding.UTF8.GetBytes(string.Join('|',Fragments.Select(f=>$"{f.Id}:{f.SourcePc:X8}:{f.ByteLength:X}:{f.Sha256}"))));
    }
    public static NativeDarkHollowWizardBehaviorSource Capture(string imagePath)
    {
        using var image=new FileStream(imagePath,FileMode.Open,FileAccess.Read,FileShare.Read);RequireOriginal(image);
        var layout=DiscImage.DetectLayout(imagePath);if(layout.SectorSize!=2352||layout.UserOffset!=24)throw new InvalidDataException("Native behavior requires MODE2/2352 geometry.");
        byte[] header=DiscImage.ReadFileBytes(image,layout,37,0,0x800);
        if(Word(header,13*8)!=0xEF6000||Word(header,13*8+4)!=0xB000||Word(header,39*8)!=0x2F5B000||Word(header,39*8+4)!=0x10000)
            throw new InvalidDataException("Native behavior overlay WAD owners changed.");
        byte[] wizard=DiscImage.ReadFileBytes(image,layout,37,0x2F5B000,0x10000),dark=DiscImage.ReadFileBytes(image,layout,37,0xEF6000,0xB000);
        byte[] exe=DiscImage.ReadFileBytes(image,layout,53875,0,0x66000);var result=new NativeDarkHollowWizardBehaviorSource(wizard,dark,exe);RequireOriginal(image);return result;
    }
    public int MainInstructionCount=>_main.InstructionCount;
    public int MainFragmentCount=>_main.Fragments.Count;
    public int SpawnFragmentCount=>_spawn.Fragments.Count;
    public int MainCallbackCount=>_main.Transfers.Count(t=>t.Kind==NativeMipsBehaviorTransferKind.CallbackRegister);
    public int SpawnCallbackCount=>_spawn.Transfers.Count(t=>t.Kind==NativeMipsBehaviorTransferKind.CallbackRegister);
    public int NativeImpactChildCount=>4;
    public int NativeDynamicGuard=>21;
    public uint InterceptedExitPc=>0x80086D8C;
    public uint DestinationContinuationPc=>0x80082914;
    public int MainStackLoads=>_main.StackAccesses.Count(a=>a.IsLoad);
    public int MainMaximumStackStoreEnd=>_main.StackAccesses.Where(a=>!a.IsLoad).Max(a=>a.Offset+a.ByteWidth);
    public bool CompleteNativeClosureProven=>false;
    public bool RuntimeFitProven=>false;
    public bool PublicationApproved=>false;
    public IReadOnlyList<string> UnresolvedObligations { get; }
    public IReadOnlyList<NativeMobyBehaviorFragmentEvidence> Fragments { get; }
    public IReadOnlyList<NativeMobyBehaviorControlEvidence> ControlTransfers { get; }
    public IReadOnlyList<NativeMobyBehaviorLiteralEvidence> LiteralWords { get; }
    public IReadOnlyList<NativeMobyBehaviorLiteralEvidence> MainStackAddressWords { get; }
    public IReadOnlyList<NativeMobyBehaviorCallEvidence> DirectExeCalls { get; }
    public string PackageSha256 { get; }
    public byte[] CopySourceFragment(string id)=>_bytes.TryGetValue(id,out var bytes)?bytes.ToArray():throw new InvalidDataException("Unknown source-owned native behavior fragment.");
    internal NativeMipsBehaviorFragmentPackage MainPackage=>_main;
    internal NativeMipsBehaviorFragmentPackage SpawnPackage=>_spawn;
    private static void RequireOriginal(FileStream image)
    {
        image.Position=0;if(image.Length!=NativeMobySourceInventory.OriginalImageByteLength||Convert.ToHexStringLower(SHA256.HashData(image))!=NativeMobySourceInventory.OriginalImageSha256||
            image.Length!=NativeMobySourceInventory.OriginalImageByteLength||image.Position!=NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Native behavior issuer requires the stable exact original image preimage.");
    }
    private static void Require(ReadOnlySpan<byte> bytes,string expected){if(Hash(bytes)!=expected)throw new InvalidDataException("Native behavior source preimage certificate changed.");}
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static uint Word(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}

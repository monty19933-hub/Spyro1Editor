using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeWizardDeathRewardEvidence(string Owner,long SourceOffset,int ByteLength,string Sha256);

/// <summary>Exact native death reward and output-only treasure target accounting; not runtime/publication approval.</summary>
public sealed class NativeDarkHollowWizardDeathRewardPlan
{
    private readonly NativeWizardTreasureTargetPatch _patch;
    private readonly byte[] _final;
    private NativeDarkHollowWizardDeathRewardPlan(byte[] executable,byte[] dark,byte[] wizardData,byte[] peteHeader,byte[] peteCopy,
        NativeDarkHollowWizardOverlayPlan overlay)
    {
        Require(executable,0x66000,"a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998");
        Require(dark,0xB000,"9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32");
        Require(wizardData,0x28A000,"9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8");
        var loader=NativeMobyModelLoaderCertificate.CaptureRetail(executable,peteHeader,peteCopy);
        if(!loader.SharedClasses.Contains((ushort)0x56)||!overlay.CopyOriginalExecutable().AsSpan().SequenceEqual(executable)||
            !overlay.CopyOriginalOverlay().AsSpan().SequenceEqual(dark))
            throw new InvalidDataException("Native death reward source is not this overlay's exact shared/source owner.");
        byte[] baseExe=overlay.CopyExecutable(),data=overlay.CopyFinalLevelData(),activeOverlay=overlay.CopyOverlay();
        overlay.RequireFinal(activeOverlay,baseExe,data);RequireSingleRewardWizard(data);
        List<NativeWizardDeathRewardEvidence> evidence=[];
        void Certificate(string owner,byte[] bytes,int offset,int length,long sourceOffset,string expected)
        {
            var slice=bytes.AsSpan(offset,length);Require(slice,length,expected);
            evidence.Add(new(owner,sourceOffset,length,expected));
        }
        int donorRow=0x1CE800+0x6388;
        Certificate("Wizard-donor-row",wizardData,donorRow,0x58,0x2F6B000L+donorRow,"59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82");
        if(wizardData[donorRow+0x53]!=RewardSelector||wizardData[donorRow+0x3A]!=0x7F)
            throw new InvalidDataException("Native donor's deterministic unclaimed reward selector changed.");
        Certificate("EXE-death-emitter",executable,0x2B3C0,0x51C,0x2B3C0,"979e567dd4bebbdd9cadaf8f884e1110be16f293c6590ba6214941be5be7a4a8");
        Certificate("EXE-Dark-spawn-binding",executable,0x4B058,0x10,0x4B058,"56569d669a6562cd613c558ecd1ce1dfbcd75a179b903f0291474999fdd4294b");
        Certificate("EXE-collection-value-award",executable,0x2BFC0,0x310,0x2BFC0,"bdaa92cb7c343607f5bbc7462f3bf54743f8f6842bb70b9f74cee06b712038e7");
        Certificate("EXE-level-slot-map",executable,0x5DE0,0x88,0x5DE0,"b6a57847d9e35a80c4002b0afc3e788a70766277f7c14c06eacc0af1e1e60ce9");
        Certificate("Dark-spawn-dispatch",dark,0x7F28,0x158,0xEFDF28,"5d147c76a31597acc031424b20419b1fc557aa17efb4fe968732a640aa67d14f");
        Certificate("Dark-gem-initializer",dark,0x8200,0x158,0xEFE200,"8f72f98f9287f1a16e599b0bf1d9a29eb169ca1396d9e4bad1b7da371b4e48ae");
        Certificate("Dark-spawn-finalizer",dark,0x87DC,0x60,0xEFE7DC,"7820c5eca67ea422562f19cd658eda6239392a3b367e5968203a4a6e7c312347");
        // PETE roots are file-relative. Its runtime copy begins after0x800;
        // select the complete registered owner, not a guessed logical model size.
        if(Word(peteHeader,4+2*8)!=0x33724||Word(peteHeader,8+2*8)!=0x56||Word(peteHeader,4+3*8)!=0x3382C)
            throw new InvalidDataException("Native shared reward model registration changed.");
        Certificate("PETE-model56",peteCopy,0x33724-0x800,0x108,0x7EBF24,"52404b4a45b86d26298f62a8b97fa92703fdd7ca6ea5cbde025e17ac8ce46ea8");
        // These exact source instructions distinguish one class56 gem worth10
        // from a quantity field, random drop, or synthetic direct gem award.
        if(Word(executable,0x2B3F4)!=0x92620053||Word(executable,0x2B3FC)!=0x3050007F||Word(executable,0x2B54C)!=0x3C028007)
            throw new InvalidDataException("Native death reward selector instructions changed.");
        if(Word(executable,0x2C2A8)!=0x2404000A||Word(executable,0x2C2B4)!=0x0C00EE15||
            BinaryPrimitives.ReadUInt16LittleEndian(executable.AsSpan(0x5FC3C))!=TreasureTargetBefore)
            throw new InvalidDataException("Native class56 value/award/target instruction preimage changed.");
        // All code certificates used by this reward must remain native in the
        // already-composed overlay/EXE, independently of its overall readback.
        foreach(var c in evidence)
        {
            if(c.Owner.StartsWith("EXE-",StringComparison.Ordinal))
                Require(baseExe.AsSpan((int)c.SourceOffset,c.ByteLength),c.ByteLength,c.Sha256);
            else if(c.Owner.StartsWith("Dark-",StringComparison.Ordinal))
                Require(activeOverlay.AsSpan((int)c.SourceOffset-0xEF6000,c.ByteLength),c.ByteLength,c.Sha256);
        }
        var original=new NativeAddressSpace(NativeAddressKind.ExecutableFile,"retail-reward:"+Hash(executable),NativeAddressRevision.Original,executable.Length);
        var final=new NativeAddressSpace(NativeAddressKind.ExecutableFile,"dark-one-wizard-reward:"+Hash(baseExe),NativeAddressRevision.Final,baseExe.Length);
        _patch=NativeWizardTreasureTargetPatch.Create(new(original,0x5FC3C,2),new(final,0x5FC3C,2),executable,baseExe);
        _final=_patch.CopyExecutable();BaseExecutableSha256=Hash(baseExe);ExecutableSha256=Hash(_final);
        SourceCertificates=Array.AsReadOnly(evidence.ToArray());
    }
    public static NativeDarkHollowWizardDeathRewardPlan Capture(string originalImagePath,NativeDarkHollowWizardOverlayPlan overlay)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath);ArgumentNullException.ThrowIfNull(overlay);
        using var image=new FileStream(originalImagePath,FileMode.Open,FileAccess.Read,FileShare.Read);RequireImage(image);
        var layout=DiscImage.DetectLayout(originalImagePath);
        if(layout.SectorSize!=2352||layout.UserOffset!=24)throw new InvalidDataException("Native death reward requires exact original disc geometry.");
        var plan=new NativeDarkHollowWizardDeathRewardPlan(DiscImage.ReadFileBytes(image,layout,53875,0,0x66000),
            DiscImage.ReadFileBytes(image,layout,37,0xEF6000,0xB000),DiscImage.ReadFileBytes(image,layout,37,0x2F6B000,0x28A000),
            DiscImage.ReadFileBytes(image,layout,37,0x7B8800,0x800),DiscImage.ReadFileBytes(image,layout,37,0x7B9000,0x39C48),overlay);
        RequireImage(image);return plan;
    }
    public int RewardSelector=>0x56;
    public int NativeGemValue=>10;
    public int TreasureTargetBefore=>100;
    public int TreasureTargetAfter=>110;
    public bool CompleteNativeClosureProven=>false;
    public bool RuntimeVerified=>false;
    public bool PublicationApproved=>false;
    public string ExecutableSha256 { get; }
    public string BaseExecutableSha256 { get; }
    public IReadOnlyList<NativeWizardDeathRewardEvidence> SourceCertificates { get; }
    internal NativeWizardTreasureTargetPatch TreasureTargetPatch=>_patch;
    public byte[] CopyExecutable()=>_final.ToArray();
    public void RequireExecutable(ReadOnlySpan<byte> bytes)
    {if(!bytes.SequenceEqual(_final))throw new InvalidDataException("Native death reward executable readback differs from its exact plan.");}
    public byte[] RevertToOverlayExecutable(ReadOnlySpan<byte> bytes)=>_patch.Revert(bytes);
    internal static void RequireSingleRewardWizard(ReadOnlySpan<byte> data)
    {
        if(data.Length<0x800)throw new InvalidDataException("Native reward level header is absent.");
        uint start=Word(data,0x18),length=Word(data,0x1C);
        if(length!=0x11800||start>data.Length||length>data.Length-start)
            throw new InvalidDataException("Native reward scene owner is invalid.");
        var scene=data.Slice((int)start,(int)length);
        if(Word(scene,0x8C34)!=129)throw new InvalidDataException("Native reward requires exactly one appended Wizard beyond128 native rows.");
        int found=0;
        for(int i=0;i<129;i++)
        {
            var row=scene.Slice(0x8C38+i*0x58,0x58);
            if(BinaryPrimitives.ReadUInt16LittleEndian(row[0x36..])!=0x11B)continue;
            found++;
            if(i!=128)throw new InvalidDataException("Native reward permits only one appended Wizard, not an existing/second Wizard.");
            if(row[0x53]!=0x56||row[0x3A]!=0x7F||row[0x48]!=0||row[0x49]!=0)
                throw new InvalidDataException("Native Wizard reward or initial unclaimed/active state changed.");
        }
        if(found!=1)throw new InvalidDataException("Native reward requires exactly one appended Wizard.");
    }
    private static void RequireImage(FileStream image)
    {
        image.Position=0;
        if(image.Length!=NativeMobySourceInventory.OriginalImageByteLength||HashStream(image)!=NativeMobySourceInventory.OriginalImageSha256||
            image.Length!=NativeMobySourceInventory.OriginalImageByteLength||image.Position!=NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Native death reward requires the stable exact original image.");
    }
    private static string HashStream(Stream image)=>Convert.ToHexStringLower(SHA256.HashData(image));
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(ReadOnlySpan<byte> bytes,int length,string expected)
    {if(bytes.Length!=length||Hash(bytes)!=expected)throw new InvalidDataException("Native death reward source certificate changed.");}
    private static uint Word(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}

internal sealed class NativeWizardTreasureTargetPatch
{
    private readonly byte[] _base,_final;
    private NativeWizardTreasureTargetPatch(NativeByteExtent original,NativeByteExtent final,ReadOnlySpan<byte> retailExe,ReadOnlySpan<byte> overlayExe)
    {
        ArgumentNullException.ThrowIfNull(original);ArgumentNullException.ThrowIfNull(final);
        if(original.Space.Kind!=NativeAddressKind.ExecutableFile||original.Space.Revision!=NativeAddressRevision.Original||
            final.Space.Kind!=NativeAddressKind.ExecutableFile||final.Space.Revision!=NativeAddressRevision.Final)
            throw new InvalidDataException("Native reward target requires exact original/final executable domains.");
        if(original.Offset!=0x5FC3C||final.Offset!=original.Offset||original.ByteLength!=2||final.ByteLength!=2)
            throw new InvalidDataException("Native reward owns only its exact Dark treasure target field.");
        if(retailExe.Length!=0x66000||overlayExe.Length!=retailExe.Length||original.Space.ByteLength!=retailExe.Length||final.Space.ByteLength!=overlayExe.Length)
            throw new InvalidDataException("Native reward executable owner length changed before copying.");
        if(BinaryPrimitives.ReadUInt16LittleEndian(retailExe[0x5FC3C..])!=100||BinaryPrimitives.ReadUInt16LittleEndian(overlayExe[0x5FC3C..])!=100)
            throw new InvalidDataException("Native reward target preimage is not the original unmodified100.");
        Original=original;Final=final;_base=overlayExe.ToArray();_final=_base.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(_final.AsSpan(0x5FC3C),110);
    }
    internal static NativeWizardTreasureTargetPatch Create(NativeByteExtent original,NativeByteExtent final,
        ReadOnlySpan<byte> retailExe,ReadOnlySpan<byte> overlayExe)=>new(original,final,retailExe,overlayExe);
    internal NativeByteExtent Original { get; }
    internal NativeByteExtent Final { get; }
    internal byte[] CopyExecutable()=>_final.ToArray();
    internal byte[] Revert(ReadOnlySpan<byte> final)
    {
        if(!final.SequenceEqual(_final))throw new InvalidDataException("Native reward revert refuses changed final readback.");
        return _base.ToArray();
    }
}

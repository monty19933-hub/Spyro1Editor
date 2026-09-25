using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeJapaneseEggMemorySmoke")]

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Source-bound normal-load transfer geometry and synchronous F5 visual stack.
/// Does not replace the broader runtime report or claim IRQ, GPU/renderer peak,
/// alternate-mode, transition-lifetime or gameplay acceptance.
/// </summary>
internal sealed class NativeJapaneseEggMemoryReport
{
    internal sealed record Metrics(uint SceneEnd,uint NormalReservedStart,long NormalGap,
        uint TransferPeakEnd,uint PeteTemporaryStart,uint PeteReadEnd,uint PeteCopySourceEnd,
        long PeteReadOverhang,uint PeteActualCopyReadEnd,long PeteActualReadOverlap,uint PeteResidentStart,uint BootStackPointer,uint DispatchStackPointer,
        uint VisualTickStackLow,long StackMargin)
    {
        internal bool ModeledExtentsFit => NormalGap>=0&&TransferPeakEnd<=NormalReservedStart&&
            TransferPeakEnd<=PeteTemporaryStart&&StackMargin>=0;
    }
    internal sealed record SourceSpan(uint Pc,int ByteLength,string Sha256,string Role);
    private NativeJapaneseEggMemoryReport(NativeJapaneseEggArtisansIntegrationPlan plan,Metrics values,SourceSpan[] spans)
    {
        IntegrationPlanSha256=plan.PlanSha256;Values=values;Evidence=Array.AsReadOnly(spans);
    }
    internal string IntegrationPlanSha256 { get; }
    internal Metrics Values { get; }
    internal IReadOnlyList<SourceSpan> Evidence { get; }
    internal bool SourceCertified=>true;
    internal bool ModeledLoadExtentsFitProven=>Values.ModeledExtentsFit;
    internal bool SynchronousVisualStackFitProven=>Values.StackMargin>=0;
    internal bool FullRuntimePeakProven=>false;
    internal bool PublicationApproved=>false;

    internal static NativeJapaneseEggMemoryReport Capture(NativeRegionalSourceSession usa,NativeJapaneseEggArtisansIntegrationPlan plan)
    {
        ArgumentNullException.ThrowIfNull(usa);ArgumentNullException.ThrowIfNull(plan);
        if(usa.Revision!=NativeDiscRevision.UsaScus94228||!plan.SourceCertified)
            throw new InvalidDataException("Memory capture requires actual USA and two-F5 integration issuers.");
        byte[] exe=usa.ReadExecutableRange(0,usa.ExecutableByteLength),data=plan.CopyLevelData(),overlay=plan.CopyOriginalOverlay();
        RequireSourceExecutable(exe);
        if(!exe.AsSpan().SequenceEqual(plan.CopyOriginalExecutable())||
            !usa.ReadWadEntry(10).AsSpan().SequenceEqual(plan.CopyOriginalLevelData())||
            !usa.ReadWadEntry(9).AsSpan().SequenceEqual(overlay))
            throw new InvalidDataException("Memory integration source ancestry differs.");
        plan.RequireFinal(plan.CopyOverlay(),plan.CopyExecutable(),data);
        // This capture charges the actual final ancestor; it never reuses a
        // retail gap for a changed copy anchor or changed full model/scene read.
        var old=plan.CopyOriginalLevelData();
        if(!data.AsSpan(0,16).SequenceEqual(old.AsSpan(0,16))||Word(data,4)!=0xEE000||
            Word(data,12)!=0x8D800||Word(data,20)!=0x4E800||Word(data,28)!=0x14800||
            plan.FinalCopyBufferAddress!=0x80089238||plan.RuntimeMemory.UsedLevelDataBytes!=0x8D31C)
            throw new InvalidDataException("This normal-load certificate admits only the actual two-F5 resource footprint.");
        SourceSpan[] spans=
        [
            new(0x8005B7D8,0x108,"58fa8da2248f5804c79362dad219ccaedcb3214cff735afdb276a1fa04d93e23","PETE directory staging then native-length copy"),
            new(0x8005B6F8,0xE0,"d0b4cf5a7cf9ee407ff413f7261bd5f92f4880d7cee8467852f4d757e6b84295","normal and alternate buffer allocator"),
            new(0x8005B904,0x78,"bbfe670bcb40f5952ad0586c3049f6e7c723ddb1e0968f8e7f1727590a25b770","actual boot stack binding"),
            new(0x8005A718,0x30,"677d24243afbccce1cc8ae5541adb182778eeaa8148bc7c8e0e9330e8ee86742","Artisans copy anchor and update callback binding"),
            new(0x80012204,0x2C,"7d40661f745552b58462ca4047601eecb5220640871968517bbce2103934455c","main frame and initialization calls"),
            new(0x80012230,8,"36680124c045edc70cff5f13b0f76dcd4cd1da0649d5accdedfdb25a4d514a4e","actual main-to-update JAL and delay slot"),
            new(0x8003385C,0x10,"ee2a02ca4beae773dd35f2196fc8d024078d45e16700094ffe67dcebe884407b","update frame"),
            new(0x80033A98,0x14,"fce1660c0e8ecc6eb36e45039fff8a2d8b5c6693faf064b870b155bc22138070","ordinary mode calls source-bound Artisans callback"),
            new(0x800156FC,0x1EC,"1f337d4e2c7b9b9ef0017938ff8906d14c5b3356f51192d175556e79a840fb32","normal header VRAM bank and data staging sequence"),
            new(0x80016698,0xE8,"8b9f95c3bcd69ae3dce304695a45427270131d7e7ac9b94282f541b644a0bea8","CD request sector rounding"),
            new(0x80016958,0x54,"a3b8e30c7eb4c67ee12309f645026b8b9f522276e769b38295fc7e25d40667f5","forward copy prefetches16 even when storing a short final group"),
            new(0x800755A4,8,"284620b7f621e7f47fb58e162ef4aa4db52946a05fb46cdf0dcdd83c0f0a11cb","top800 reservation and 200000 stack origin"),
        ];
        foreach(var span in spans)
            if(Hash(exe.AsSpan(checked((int)(span.Pc-0x8000F800)),span.ByteLength))!=span.Sha256)
                throw new InvalidDataException("Native memory instruction owner changed: "+span.Role);
        if(Word(overlay,checked((int)(0x8007D9C8-0x8007AA38)))!=0x27BDFE40||
            Word(exe,checked((int)(0x80038EE0-0x8000F800)))!=0x27BDFFD8||
            Word(exe,checked((int)(0x8003385C-0x8000F800)))!=0x27BDFFE0||
            Word(exe,checked((int)(0x80033AA4-0x8000F800)))!=0x0040F809)
            throw new InvalidDataException("Source synchronous callback/frame ancestry changed.");
        int adapter=NativeMipsOwnedFrameAdapter.Create(0x80080000,0x80080004).FrameByteLength;
        byte[] finalOverlay=plan.CopyOverlay();
        if(Word(finalOverlay,checked((int)(plan.VisualAncestor.EntryPc-0x8007AA38)))!=0x27BDFD50)
            throw new InvalidDataException("Actual integrated visual adapter frame is not the existing 2B0 owner.");
        Metrics values=Measure(plan.FinalCopyBufferAddress,(uint)plan.RuntimeMemory.UsedLevelDataBytes,Word(data,12),
            Word(data,20),Word(data,28),(uint)usa.Entries[8].ByteLength,Word(plan.CopyPeteHeader(),0),
            Word(exe,0x65DA8),Word(exe,0x65DA4),adapter);
        if(!values.ModeledExtentsFit||values.SceneEnd!=plan.RuntimeMemory.SceneReadEnd||
            values.NormalReservedStart!=plan.RuntimeMemory.NormalReservedBufferStart)
            throw new InvalidDataException("Modeled normal transfer or synchronous visual stack exceeds its native bound.");
        usa.VerifyUnchanged();
        return new(plan,values,spans);
    }

    // Neutral arithmetic only. It cannot issue a source-bound report or approve
    // bytes, and does not hide overlapping phases by summing all allocations.
    internal static Metrics Measure(uint copy,uint used,uint dataRead,uint model,uint scene,uint peteDirectory,
        uint peteNative,uint stackOrigin,uint topReserved,int adapter)
    {
        if(copy<0x80010000||copy>=0x80200000||copy%4!=0||used==0||used%4!=0||used>dataRead||
            new[]{dataRead,model,scene,peteDirectory}.Any(n=>n==0||n>0x200000||n%0x800!=0)||
            peteNative<0x800||peteNative>0x100000||peteNative%4!=0||stackOrigin!=0x200000||
            topReserved<8||topReserved>0x10000||topReserved%4!=0||adapter<=0||adapter>0x10000||adapter%4!=0)
            throw new InvalidDataException("Invalid bounded native memory geometry.");
        long top=0x80200000L-topReserved,resident=top-peteNative,normal=resident-0x6008-2*0x1C000;
        long temporary=top-2*peteDirectory,peteReadEnd=temporary+peteDirectory;
        long copySourceEnd=temporary+0x800+peteNative;
        long actualCopyReadEnd=temporary+0x800+((peteNative+15L)&~15L);
        long sceneEnd=copy+(long)used+model+scene;
        long peak=Math.Max(sceneEnd,Math.Max(copy+(long)dataRead,copy+0x80000L));
        // Boot subtracts8, main20, update20, source Artisans dispatch1C0.
        // Sine/random/angle leaf helpers allocate no frames; turn helper adds28.
        long boot=0x80000000L+stackOrigin-8,dispatch=boot-0x20-0x20-0x1C0;
        long low=dispatch-adapter-0x28;
        if(new[]{normal,resident,temporary,peteReadEnd,copySourceEnd,actualCopyReadEnd,sceneEnd,peak,boot,dispatch,low}.Any(n=>n<0x80010000L||n>0x80200000L))
            throw new InvalidDataException("Native memory endpoint is outside main RAM.");
        return new((uint)sceneEnd,(uint)normal,normal-sceneEnd,(uint)peak,(uint)temporary,(uint)peteReadEnd,
            (uint)copySourceEnd,copySourceEnd-peteReadEnd,(uint)actualCopyReadEnd,
            Math.Max(0,Math.Min(actualCopyReadEnd,top)-Math.Max(temporary+0x800,resident)),
            (uint)resident,(uint)boot,(uint)dispatch,(uint)low,low-top);
    }
    internal static void RequireSourceExecutable(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length!=0x66000||Hash(bytes)!="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998")
            throw new InvalidDataException("Actual complete USA executable identity is required.");
    }
    private static uint Word(ReadOnlySpan<byte> bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}

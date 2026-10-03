using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Exact original Magic12/13 in Artisans: retain every complete model
/// and its alignment while preserving the native dragon stream reservation.
/// Only four generated zero sector-padding bytes move between package tails.</summary>
internal static class UniversalMagicArtisansRescueModels
{
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Armored druid group in Artisans: "+why);}
    internal static void Prepare(UniversalLevelData data)
    {
        var sf=data.Subfiles()[2];var regs=data.Registrations();
        Require(sf==(1572864,288768)&&regs.Count==33&&
            Hash(data.Bytes.AsSpan(0,2048))=="0e344e1a117c0469f8dfef070dca2e350af7682eba326d40af6a2d8a4f727420"&&
            Hash(data.Bytes.AsSpan(sf.Offset,sf.Length))=="3ab0fa019f8301883f8b1c04d320cf4e9d90e5961cb1c2b2448a2884b742c04c",
            "the complete source-bound model allocation differs from its audited owner.");
        var moved=regs.Single(r=>r.ClassId==271);var anchor=regs.Single(r=>r.ClassId==423);var statue=regs.Single(r=>r.ClassId==250);
        Require(moved.Root==0x1b1eb4&&anchor.Root==0x1b01f4&&statue.Root==0x1b0350&&moved.Root>statue.Root,
            "the native statue/imported-model ordering changed.");
        const int payload=83756,padding=544,shift=84296;
        Require(data.Package(271).Length==payload+padding&&data.Bytes.AsSpan(moved.Root+payload,padding).IndexOfAnyExcept((byte)0)<0,
            "the complete imported model's generated sector padding changed.");
        var old=data.Bytes.ToArray();var before=new UniversalLevelData(old);
        old.AsSpan(anchor.Root,moved.Root-anchor.Root).CopyTo(data.Bytes.AsSpan(anchor.Root+shift));
        old.AsSpan(moved.Root,shift).CopyTo(data.Bytes.AsSpan(anchor.Root));
        var relocated=regs.Select(r=>(Class:r.ClassId,Root:r.ClassId==271?anchor.Root:r.Root>=anchor.Root&&r.Root<moved.Root?r.Root+shift:r.Root)).OrderBy(r=>r.Root).ToArray();
        for(int n=0;n<relocated.Length;n++)
        {data.Put32(0x50+4*(n+1),relocated[n].Root);BinaryPrimitives.WriteUInt16LittleEndian(data.Bytes.AsSpan(0x150+2*(n+1)),(ushort)relocated[n].Class);}
        Require(data.Bytes.Length==old.Length&&data.Subfiles().SequenceEqual(before.Subfiles()),"model normalization changed the allocation.");
        foreach(var r in regs)
        {
            var after=data.Registrations().Single(t=>t.ClassId==r.ClassId);Require((after.Root&7)==(r.Root&7),"a complete native model's alignment changed.");
            byte[] a=before.Package(r.ClassId),b=data.Package(r.ClassId);int common=Math.Min(a.Length,b.Length);
            Require(a.AsSpan(0,common).SequenceEqual(b.AsSpan(0,common))&&a.AsSpan(common).IndexOfAnyExcept((byte)0)<0&&b.AsSpan(common).IndexOfAnyExcept((byte)0)<0,
                "a complete native/imported model changed beyond generated zero sector padding.");
            Require(r.ClassId==271?b.Length==a.Length-4:r.ClassId==251?b.Length==a.Length+4:b.Length==a.Length,"an unexpected model's package extent changed.");
        }
        Require(before.Package(271).AsSpan(0,payload).SequenceEqual(data.Package(271).AsSpan(0,payload)),"the entire imported model payload changed.");
        Require(Hash(data.Bytes.AsSpan(0,2048))=="239d40ca7aa750d61dca42d9bd1d5101c0c1ce431f021b74f2f8d34e86a1823b"&&
            Hash(data.Bytes.AsSpan(sf.Offset,sf.Length))=="8faa61d56d55cdab9bd224b92147a6785231420b2a310d6a6de20bb3d23badc9",
            "the independently derived complete model/registration owner changed.");
    }
}

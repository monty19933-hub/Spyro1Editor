using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Exact original Magic12/13 in Artisans: retain every complete model
/// and its alignment while preserving the native dragon stream reservation.
/// The shared animation importer preserves root alignment before this exact
/// whole-package rotation; no payload or package padding is reassigned.</summary>
internal static class UniversalMagicArtisansRescueModels
{
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Armored druid group in Artisans: "+why);}
    internal static void Prepare(UniversalLevelData data)
    {
        var sf=data.Subfiles()[2];var regs=data.Registrations();
        Require(sf==(1572864,288768)&&regs.Count==33&&
            Hash(data.Bytes.AsSpan(0,2048))=="b6bdad6dfbe339ae290b06f946f828f1465faaed072e2e178545ec58eceac456"&&
            Hash(data.Bytes.AsSpan(sf.Offset,sf.Length))=="f3d4c190582504411962314f065172e7fbf6f35a9d1cd50a8b8cf39b93095274",
            "the complete source-bound model allocation differs from its audited owner.");
        var moved=regs.Single(r=>r.ClassId==271);var anchor=regs.Single(r=>r.ClassId==423);var statue=regs.Single(r=>r.ClassId==250);
        Require(moved.Root==0x1b1eb8&&anchor.Root==0x1b01f8&&statue.Root==0x1b0354&&moved.Root>statue.Root,
            "the native statue/imported-model ordering changed.");
        const int payload=83756,padding=540,shift=84296;
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
            byte[] a=before.Package(r.ClassId),b=data.Package(r.ClassId);
            Require(a.AsSpan().SequenceEqual(b),"a complete native/imported model or its package padding changed.");
        }
        Require(before.Package(271).AsSpan(0,payload).SequenceEqual(data.Package(271).AsSpan(0,payload)),"the entire imported model payload changed.");
        Require(Hash(data.Bytes.AsSpan(0,2048))=="db347383bac5e4baa1e45747eb77a4281c57630f6fabe6da0a373fe72e127576"&&
            Hash(data.Bytes.AsSpan(sf.Offset,sf.Length))=="021a3ba02ef10c041f6db4b559430f13031910632b597f7023d4e044f6772252",
            "the independently derived complete model/registration owner changed.");
    }
}

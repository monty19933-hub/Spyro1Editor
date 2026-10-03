using System.Buffers.Binary;
namespace Spyro.Editor.Core.Universal;

/// <summary>Retains the native dragon loader's model250 scratch boundary. All
/// packages stay complete; registration identities and the total allocation stay unchanged.</summary>
internal static class UniversalAlpineArtisansRescueModels
{
    private static int I(byte[] b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
    private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Artisans dragon rescue: "+why);}
    internal static int RequiredPrefix(UniversalLevelData original,byte[] exe)
    {
        Require(UniversalAlpineArtisansHost.Hash(original.Bytes)==UniversalAlpineArtisansHost.DataHash,"original dragon streams changed.");
        UniversalAlpineArmNative.RequireNative(exe);
        // Original14C04..14DF8 reads up to model250, rounded down to a CD
        // sector, then copies readBytes-header[12]. The entire stream sound
        // prefix must already be present. Include all five retained streams.
        var sf=original.Subfiles();Require(sf.Count==9,"original dragon stream inventory changed.");
        int required=0;
        foreach(int n in Enumerable.Range(4,5))
        {
            var s=sf[n];int sound=I(original.Bytes,s.Offset+4),bytes=I(original.Bytes,s.Offset+8),geometry=I(original.Bytes,s.Offset+12);
            Require(sound==36&&bytes>0&&geometry==sound+bytes&&geometry<=s.Length,"retained dragon stream sound/header boundary changed.");
            required=Math.Max(required,UniversalLevelData.Align(geometry,2048));
        }
        return required;
    }
    internal static void RequirePrefix(UniversalLevelData data,UniversalLevelData original,byte[] exe)
    {
        int need=RequiredPrefix(original,exe),model=data.Subfiles()[2].Offset;
        var dragon=data.Registrations().SingleOrDefault(r=>r.ClassId==250);
        Require(dragon!=null,"the original dragon statue model is missing.");
        int available=(dragon!.Root-model)/2048*2048;
        Require(available>=need,$"dragon rescue needs {need:N0} bytes before its statue model; this layout provides {available:N0} bytes. Keep the complete rescue resource reservation.");
    }
    internal static void Prepare(UniversalLevelData data,UniversalLevelData original,int completeFdClass,byte[] exe)
    {
        int need=RequiredPrefix(original,exe);var sf=data.Subfiles()[2];var regs=data.Registrations();
        var dragon=regs.Single(r=>r.ClassId==250);var moved=regs.Single(r=>r.ClassId==completeFdClass);
        Require((dragon.Root-sf.Offset)/2048*2048<need,"the expected reclaimed Artisans model prefix changed.");
        var roots=regs.OrderBy(r=>r.Root).ToArray();int end=roots.Where(r=>r.Root>moved.Root).Select(r=>r.Root).DefaultIfEmpty(sf.Offset+sf.Length).Min();
        Require(moved.Root>dragon.Root&&end>moved.Root&&(end-moved.Root)%8==0,"the complete imported armored model cannot preserve its native alignment.");
        int insert=roots.Where(r=>r.Root<=dragon.Root&&(r.Root&7)==(moved.Root&7)).Select(r=>r.Root).DefaultIfEmpty(-1).Max();
        Require(insert>=sf.Offset,"no retained package boundary preserves the imported model alignment.");
        var old=data.Bytes.ToArray();int size=end-moved.Root;
        // Insert the complete imported package before model250 rather than
        // leave reclaimed holes or shorten any actor/animation. This changes
        // only model order and root offsets; native reload uses those roots.
        old.AsSpan(insert,moved.Root-insert).CopyTo(data.Bytes.AsSpan(insert+size));
        old.AsSpan(moved.Root,size).CopyTo(data.Bytes.AsSpan(insert));
        List<(int Class,int Root)> relocated=[];
        foreach(var r in regs)
        {
            int root=r.Slot==moved.Slot?insert:r.Root>=insert&&r.Root<moved.Root?r.Root+size:r.Root;
            Require((root&7)==(r.Root&7),"a native model root alignment changed.");
            relocated.Add((r.ClassId,root));
        }
        int slot=1;foreach(var r in relocated.OrderBy(r=>r.Root))
        { data.Put32(0x50+4*slot,r.Root);BinaryPrimitives.WriteUInt16LittleEndian(data.Bytes.AsSpan(0x150+2*slot),(ushort)r.Class);slot++; }
        Require(data.Subfiles().SequenceEqual(new UniversalLevelData(old).Subfiles())&&data.Bytes.Length==old.Length,"model ordering changed the allocation.");
        var before=new UniversalLevelData(old);
        foreach(var r in regs)Require(before.Package(r.ClassId).AsSpan().SequenceEqual(data.Package(r.ClassId)),"a complete registered model package changed.");
        RequirePrefix(data,original,exe);
    }
}

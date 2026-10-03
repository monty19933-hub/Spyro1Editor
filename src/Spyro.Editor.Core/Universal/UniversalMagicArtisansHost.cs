using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Universal;

/// <summary>The original Artisans host with two whole native linked enemy families removed.
/// Fixed source identities are retained, never compacted or rebound. This host profile
/// deliberately excludes additional removals, terrain changes, mixed imports and other selectors.</summary>
internal static class UniversalMagicArtisansHost
{
    internal static readonly int[] RemovedRows = [7,29,30,31,32,33];
    private static void Require(bool ok,string why) { if(!ok)throw new InvalidDataException("Armored druid group in Artisans: "+why); }
    private static string H(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static int I(byte[] bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at));
    internal static void RequireOriginal(byte[] bytes,byte[] overlay,byte[] exe,IReadOnlyCollection<int>? removed)
    {
        Require(H(bytes)=="86cde4f886d97821a3a4b869100b586842dbd4b9fb696558bead274bb547af4f" && H(overlay)=="7df6cf2f3ed4a71a123f03621019470bd6fe0cd243f75b1e8c3a115e6b1fcb55","start from original Artisans; existing object, terrain and asset edits are not supported by this first host certificate");
        Require(H(exe)=="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998","original native executable and all incoming callers are required");
        Require(removed!=null && removed.Order().SequenceEqual(RemovedRows),"make room by deleting the six original Gnorcs at rows7,29,30,31,32,33 together; partial families and additional native deletions have no issued index/asset owner");
        var data=new UniversalLevelData(bytes);var bindings=UniversalExeBindings.Read(exe,10);var host=new UniversalOverlay(overlay,bindings);
        Require(data.Scene().RowCount==174 && (data.Scene().ArenaEnd-data.Scene().RowsOffset)/88==290,"native physical prefix/allocated dynamic arena changed");
        var allocation=UniversalRowRemoval.Allocate(data,RemovedRows,2);Require(allocation.FinalRowCount==174&&allocation.NewRows.SequenceEqual(new[]{7,29}),"physical allocation must reuse only authenticated removed slots7/29");
        Require(bindings.Count==9&&host.Init==0x800857CC&&host.Update==0x8007D9C8&&host.CopyBuffer==0x80088620&&bindings[UniversalExeBindings.ParticleSpawnGlobal].Value==0x800873E0&&bindings[UniversalExeBindings.ParticleUpdateGlobal].Value==0x80086134,"native callback/profile ABI changed");
        var scene=data.Scene();int at=data.Subfiles()[3].Offset+scene.PodComponent;Require(I(bytes,at+4)==0,"native pods must be absent before the one imported source group");
        for(int row=0;row<174;row++)Require(data.Row(row)[0x43]==255,"original native actor pod domain changed");
        // Full native overlay/EXE authentication above binds every producer/caller,
        // branch, table and class selector. These exact source fields expose the
        // surviving indexed domains; no destination same-number inference is used.
        foreach(int row in new[]{133,134,135,136,137})Link(row,0x12,4,138);
        Link(160,0x31,8,91);
        foreach(var (row,target) in new[]{(88,89),(91,90),(93,92),(143,142)}){Link(row,0xFA,0x20,target);Require(I(data.CaptureRow("artisans",row).Properties,0x24)==-1,"dragon negative secondary index changed");}
        Link(45,0xBB,0,46);
        // Class06E's direct 8007DB58 branch reaches800804F0 (before the
        // general handler closure inventory). Its two source rows are the
        // retained dragon/dragon-pad pair, not one of the reclaimed Gnorcs.
        foreach(var (row,dragon,pad) in new[]{(161,88,89),(162,91,90),(163,93,92),(164,143,142)}) { Link(row,0x6E,0,dragon);Link(row,0x6E,4,pad); }
        var whirlwind=data.CaptureRow("artisans",42);Require(whirlwind.ClassId==0x12C&&I(whirlwind.Properties,0x10)==-1&&(I(whirlwind.Properties,8)&8)==0,"original inactive whirlwind index domain changed");
        var collect=data.CaptureRow("artisans",115);Require(collect.ClassId==0x143&&I(collect.Properties,0)==10,"native class-filtered collection domain changed");
        foreach(var row in RemovedRows)Require(data.CaptureRow("artisans",row).ClassId==(row is 7 or 31?0x153:0x72),"removed original owner class changed");
        foreach(var (row,target) in new[]{(29,30),(30,29),(32,33),(33,32)})Link(row,0x72,8,target,true);
        foreach(var row in new[]{7,31}){var t=data.CaptureRow("artisans",row);Require(I(t.Properties,0x10)==(row==7?19:37)&&I(t.Properties,0x14)==(row==7?18:35),"removed runningGnorc chain issuer changed");}
        void Link(int row,int cls,int field,int target,bool removedFamily=false)
        {
            var owner=data.CaptureRow("artisans",row);Require(owner.ClassId==cls&&I(owner.Properties,field)==target,"source row-index owner changed");
            Require(target>=0&&target<174&&(removedFamily?RemovedRows.Contains(target):!RemovedRows.Contains(target)),"surviving native index names a removed/reused row");
        }
    }
    // Saved native edits soft-delete exactly X/Y/Z to -480000 and adjust the
    // reward target. Restore only those owned fields on clones, then require
    // complete original images: unrelated user edits are rejected, never lost.
    internal static (byte[] Data,byte[] Exe) RestoreSavedDeletions(byte[] data,byte[] overlay,byte[] exe,IReadOnlyCollection<int>? removed)
    {
        Require(removed!=null&&removed.Order().SequenceEqual(RemovedRows),"saved deletion set must include all six original linked Gnorcs only");
        byte[] original=data.ToArray();var parsed=new UniversalLevelData(original);int scene=parsed.Subfiles()[3].Offset;
        (int Row,int X,int Y,int Z)[] sourceCoordinates=[(7,78848,37888,6637),(29,89088,64512,8566),(30,87040,64512,8220),(31,134134,50186,1590),(32,97280,52224,6587),(33,99328,50176,6630)];
        foreach(var (row,x,y,z) in sourceCoordinates)
        {
            int at=scene+parsed.Scene().RowsOffset+row*88+12;
            Require(I(original,at)==-480000&&I(original,at+4)==-480000&&I(original,at+8)==-480000,"saved native deletion did not emit exact soft-remove coordinates");
            BinaryPrimitives.WriteInt32LittleEndian(original.AsSpan(at),x);BinaryPrimitives.WriteInt32LittleEndian(original.AsSpan(at+4),y);BinaryPrimitives.WriteInt32LittleEndian(original.AsSpan(at+8),z);
        }
        byte[] originalExe=exe.ToArray();UniversalTreasureAccounting.ApplyDelta(originalExe,10,13);
        RequireOriginal(original,overlay,originalExe,removed);
        Require(RemovedRows.Sum(row=>UniversalMobyComposer.TreasureValue(parsed.Row(row)))==13,"native reward ownership changed");
        return(original,originalExe);
    }
    internal static int CacheBytes(UniversalLevelData original,int finalPhysicalRows)
    {
        int arena=(original.Scene().ArenaEnd-original.Scene().RowsOffset)/88;
        int dynamicRows=checked((original.Scene().ArenaEnd-original.Scene().RowsOffset-finalPhysicalRows*88)/112);
        int words=checked(finalPhysicalRows+dynamicRows+1);
        Require(arena==290&&finalPhysicalRows==174&&dynamicRows==91&&words==266,"native cache bound lacks exact component-derived owner");
        Require(0x400+words*4<=0x1C00,"cache preservation leaves original SonyImage scratch domain");return words*4;
    }
}

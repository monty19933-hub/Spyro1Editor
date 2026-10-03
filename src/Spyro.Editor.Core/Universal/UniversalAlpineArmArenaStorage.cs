using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Private owned reserve storage over intact Blowhard. Reserving memory
/// is a prerequisite only: no source factory frequency or gameplay certificate
/// is issued by this operation. The existing native reserve is never consumed.</summary>
internal static class UniversalAlpineArmArenaStorage
{
    internal const string HostHash="3dfe32f742de381626afc133b7e750bb3b21a08ce51cae4ec4ef1eee296a8bf6";
    internal const int NativeRows=87,NativeSlots=91,BytesPerArm=112;
    private static int I(byte[] b,int p)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(p));
    private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Alpine additional native arena storage: "+why);}
    internal static void RequireWritten(UniversalLevelData host,UniversalLevelData source,UniversalOverlay overlay,byte[] exe,int slots,int placedMembers,UniversalLevelData written)
    {
        var expected=Reserve(host,source,overlay,exe,slots,placedMembers);
        Require(written.Bytes.SequenceEqual(expected.Bytes),"written reserve/native component/pointer/fixup allocation differs from independently derived source/input regions");
    }
    internal static UniversalLevelData Reserve(UniversalLevelData host,UniversalLevelData source,UniversalOverlay overlay,byte[] exe,int slots,int placedMembers)
    {
        UniversalAlpineArmNative.RequireNative(exe);UniversalAlpineArmResources.ProveSource(source,overlay);
        Require(Convert.ToHexStringLower(SHA256.HashData(host.Bytes))==HostHash,"requires intact original Blowhard; native edits need their own owner");
        Require(slots>0&&placedMembers is 2 or 3&&NativeRows+NativeSlots+slots+placedMembers+1<=256,"reserve exceeds the authenticated row/cache namespace");
        var sc=host.Scene();Require(sc.RowCount==NativeRows&&UniversalMobyComposer.ArenaSlots(host)==NativeSlots,"native row/reserve layout changed");
        var sf=host.Subfiles()[3];byte[] original=host.Bytes.AsSpan(sf.Offset,sf.Length).ToArray();int growth=checked(slots*BytesPerArm),insert=sc.ArenaEnd;
        Require(insert<=sc.PropertiesComponent&&original.AsSpan(sc.RowsOffset+NativeRows*88,insert-sc.RowsOffset-NativeRows*88).IndexOfAnyExcept((byte)0)<0,"native unused reserve owns nonzero bytes");
        int Move(int v)=>v>=insert?checked(v+growth):v;
        byte[] rewritten=new byte[checked(sc.UsedEnd+growth)];original.AsSpan(0,insert).CopyTo(rewritten);original.AsSpan(insert,sc.UsedEnd-insert).CopyTo(rewritten.AsSpan(insert+growth));
        BinaryPrimitives.WriteInt32LittleEndian(rewritten.AsSpan(sc.MobyComponent),sc.MobyComponentSize+growth);
        foreach(int fixup in sc.Fixups)BinaryPrimitives.WriteInt32LittleEndian(rewritten.AsSpan(Move(fixup)),Move(I(original,fixup)));
        int fixups=Move(sc.FixupsOffset);for(int i=0;i<sc.Fixups.Count;i++)BinaryPrimitives.WriteInt32LittleEndian(rewritten.AsSpan(fixups+4+4*i),Move(sc.Fixups[i]));
        var result=new UniversalLevelData(host.Bytes.ToArray());result.ReplaceScene(rewritten);
        var after=result.Scene();Require(after.RowCount==NativeRows&&UniversalMobyComposer.ArenaSlots(result)==NativeSlots+slots,"derived reserve failed independent scene readback");
        for(int row=0;row<NativeRows;row++)
        {
            var expected=host.Row(row);int rowAt=sc.RowsOffset+row*88;
            foreach(int f in sc.Fixups.Where(f=>f>=rowAt&&f<rowAt+88))BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(f-rowAt),Move(I(original,f)));
            Require(result.Row(row).SequenceEqual(expected),"native row identity or owned pointer changed beyond reserve relocation");
        }
        return result;
    }
}

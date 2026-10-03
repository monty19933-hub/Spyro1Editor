using Spyro.Editor.Core.Exporting;
namespace Spyro.Editor.Core.Universal;

/// <summary>Private common-poof sound resource owner. Exact native385BC reads
/// global selector0x26 (decimal38), before original55A78 playback. This only
/// issues sample/control retention, not a reachable death or SPU playback claim.</summary>
internal static class UniversalAlpineArmDeathSound
{
    internal const int Selector=0x26;
    internal static void Require(UniversalLevelData source,UniversalOverlay overlay,byte[] exe,UniversalLevelData originalHost,UniversalLevelData written)
    {
        UniversalAlpineArmResources.ProveSource(source,overlay);UniversalAlpineArmNative.RequireNative(exe);
        if(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(originalHost.Bytes))!=UniversalAlpineArmArenaStorage.HostHash)
            throw new InvalidDataException("Alpine common death sound requires its intact original Blowhard resource owner.");
        var a=new NativeLevelSoundLayout(source.Bytes).Sound;var b=new NativeLevelSoundLayout(originalHost.Bytes).Sound;var c=new NativeLevelSoundLayout(written.Bytes).Sound;
        var da=a.Descriptors[a.Map[Selector]];var db=b.Descriptors[b.Map[Selector]];var dc=c.Descriptors[c.Map[Selector]];
        if(a.Map[Selector]!=22||b.Map[Selector]!=22||c.Map[Selector]!=b.Map[Selector]||
            !da.CopyDescriptor().AsSpan(4).SequenceEqual(db.CopyDescriptor().AsSpan(4))||
            !dc.CopyDescriptor().AsSpan(4).SequenceEqual(db.CopyDescriptor().AsSpan(4))||
            !da.Sample.CopyBytes().SequenceEqual(db.Sample.CopyBytes())||!dc.Sample.CopyBytes().SequenceEqual(db.Sample.CopyBytes()))
            throw new InvalidDataException("Alpine native global death sound0x26 lost its original selector, controls or complete ADPCM sample.");
    }
}

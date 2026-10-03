using System.Security.Cryptography;
using System.Text.Json;
namespace Spyro.Editor.Core.Universal;
/// <summary>Original Blowhard decoded texture consumers and all unknown nonzero
/// bytes. This immutable source-owner ledger prevents caller-supplied fit ranges
/// from releasing native terrain/model/particle/shared pixels or palettes.</summary>
internal static class UniversalAlpineArmTextureOwners
{
    private static readonly (long Offset,int Length)[] Ranges;
    private static readonly string VramHash;
    static UniversalAlpineArmTextureOwners()
    {
        using var stream=typeof(UniversalAlpineArmTextureOwners).Assembly.GetManifestResourceStream("Spyro.Editor.Core.Universal.AlpineBlowhardTextureOwners.json")??throw new InvalidDataException("Alpine native texture ownership resource is missing.");
        using var doc=JsonDocument.Parse(stream);var root=doc.RootElement;
        if(root.GetProperty("OriginalHostSha256").GetString()!=UniversalAlpineArmArenaStorage.HostHash||root.GetProperty("OriginalDiscSha256").GetString()!=UniversalSourceLibrary.OriginalUsaSha256)throw new InvalidDataException("Alpine native texture ledger has the wrong source owner.");
        VramHash=root.GetProperty("NativeVramSha256").GetString()!;
        Ranges=root.GetProperty("ProtectedRanges").EnumerateArray().Select(x=>(x[0].GetInt64(),x[1].GetInt32())).ToArray();
        if(Ranges.Any(x=>x.Offset<0||x.Length<1||x.Offset+x.Length>UniversalVramAllocator.ImageBytes))throw new InvalidDataException("Alpine native texture ledger exceeds VRAM.");
    }
    internal static IEnumerable<(long Offset,int Length)> Protected(UniversalLevelData original)
    {
        if(Convert.ToHexStringLower(SHA256.HashData(original.Vram()[..UniversalVramAllocator.ImageBytes]))!=VramHash)throw new InvalidDataException("Alpine native texture ledger differs from actual retained Blowhard pixels.");
        return Ranges.Select(x=>x);
    }
    internal static void RequireWritten(UniversalLevelData original,UniversalLevelData written)
    {
        foreach(var (at,length) in Protected(original))if(!original.Vram().Slice(checked((int)at),length).SequenceEqual(written.Vram().Slice(checked((int)at),length)))throw new InvalidDataException("Alpine retained native texture consumer pixels/palette changed.");
    }
}

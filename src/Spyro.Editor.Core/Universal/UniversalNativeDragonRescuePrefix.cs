using System.Buffers.Binary;
using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

internal sealed record UniversalNativeDragonRescueBudget(int RequiredPrefix,int AvailablePrefix,int StreamCount)
{ internal int Deficit=>Math.Max(0,RequiredPrefix-AvailablePrefix); }

/// <summary>The original dragon loader uses the registered250 model as the
/// end of temporary stream storage. Model removal can preserve every remaining
/// package yet shrink that live reservation enough to underflow native memcpy.
/// A stream that does not fit whole is read in two parts: the first fills the
/// reservation (its sound prefix goes to the SPU), the rest of the stream is then
/// moved down and the remainder read behind it, ending at modelbase+size-prefix.
/// Both the sound prefix and that remainder must fit below the live statue.</summary>
internal static class UniversalNativeDragonRescuePrefix
{
    private static readonly IReadOnlyDictionary<string,string> OriginalHosts=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
    {
        ["artisans"]="86cde4f886d97821a3a4b869100b586842dbd4b9fb696558bead274bb547af4f",
        ["stonehill"]="c341a3a10a67590c69d23547f6ad147f05f0b6fb7d0fb1360d572793276e796b",
        ["darkhollow"]="a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97",
        ["townsquare"]="7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0",
        ["toasty"]="0d1aacf62f5c988a4d8c1f41ef8025464aa178a09e92e64e3f6d36957da8b8f9",
        ["peacekeepers"]="c140bd422471c66cd57bd85b29912819f798f5b080779153674714889306c9d3",
        ["drycanyon"]="94cc8d715ea0564ac50a456fb852ff80393a97c0963fe7b5bb97ad3a9ad23224",
        ["clifftown"]="8246261aa9d5fe9f83bc138051bbfc9734af4673928808b08d1b414674babdc0",
        ["icecavern"]="02846f17d7ce6a8813c07c90bd82bf3862ffe044ff202f75877c3da0757f8062",
        ["doctorshemp"]="2383ceb1257e8512c80cbd8f81bf7ceadd6a3e62d26c4a870ed9577e134f2a8d",
        ["magiccrafters"]="ad34af2e3401514e7db740691c80fd2bbf59057a55bdfedbaaa39b6939141fcb",
        ["alpineridge"]="507c7b327bdfe9e4c230bef7d93da5d8c935a9a9f033ad99de22a5af8dfd0ddf",
        ["highcaves"]="5f8f3189f0695e8710f71cc4056fc5dd677758c0cf26fd291953a650a6ada16c",
        ["wizardpeak"]="9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8",
        ["blowhard"]="3dfe32f742de381626afc133b7e750bb3b21a08ce51cae4ec4ef1eee296a8bf6",
        ["beastmakers"]="41264e1b9ab2eb877fa9890c32456f3636090159cdff4486bca47571c85117bf",
        ["terracevillage"]="26bd02231f93d780ee75b528aac8e5797fbb0b578acfb2ba21e692872e6b5c5e",
        ["mistybog"]="b8019233e082408247c0a5a7b5c60f95d36147d3029041aec60fa5decce44541",
        ["treetops"]="808552324fd3925f676bb3deeee719395b1f63612d6a0f502b67f469b2d6118e",
        ["metalhead"]="987462f78e22d41438700100d57c918e4063c214903c71f5ed668dce2727963c",
        ["dreamweavers"]="cadd3a2366eae64995c0eb40427410c569b6cbc8faea1ba0872639f31230c559",
        ["darkpassage"]="217d4e525f10b63f4ae4a413d807989241b7f2f91963b543b421cbd3954c364a",
        ["loftycastle"]="57a61a60d5f4b4e303287bcf95b7868f135d60d42d7118fd1d1ecfb6b58beacf",
        ["hauntedtowers"]="970bc9e977d679f01d3ad65c8f0d07dee746ea2ede5307351dadb1c8887fb79d",
        ["jacques"]="3f7a1ea3dcd8e1f87055e2ae746ce45f44e99b9f7a0dd8142beefe8e94f2ecac",
        ["gnastysworld"]="b2a25147c4d81e7fd366a830546b92e6a482fe9a56f8a429a30fcb4857091a02",
        ["gnorccove"]="0f4ad8c1644a823022ac03718f3b46167a9545b202a4ef4b47d1de085c4a0d7e",
        ["twilightharbor"]="a230505350794dcae8205b142a9d60621a4c20bf4c20d3c83818c5a4f7ca0b26",
    };
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static int I(byte[] b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(at));
    private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Dragon rescue capacity: "+why);}
    internal static UniversalNativeDragonRescueBudget? RequireSafe(string key,string name,UniversalLevelData original,UniversalLevelData written,byte[] exe)
    {
        var budget=Measure(key,name,original,written,exe);
        Require(budget==null||budget.Deficit==0,$"{name}'s remaining dragon rescues need {budget?.Deficit:N0} more bytes in their temporary area. Restore some removed native object families before exporting; that reserved space cannot be reclaimed.");
        return budget;
    }
    /// <summary>The rescue reservation below the statue against the largest stream's need, or null without a statue.</summary>
    internal static UniversalNativeDragonRescueBudget? Measure(string key,string name,UniversalLevelData original,UniversalLevelData written,byte[] exe)
    {
        var statue=written.Registrations().SingleOrDefault(r=>r.ClassId==250);
        bool live=Enumerable.Range(0,written.Scene().RowCount).Any(r=>BinaryPrimitives.ReadUInt16LittleEndian(written.Row(r).AsSpan(54))==250);
        if(statue==null){Require(!live,name+" has a remaining dragon statue without its complete native model.");return null;}
        Require(OriginalHosts.TryGetValue(key,out var sourceHash)&&Hash(original.Bytes)==sourceHash,name+" needs its original dragon rescue resources; this host has no authenticated rescue owner.");
        using var native=new MemoryStream();foreach(var span in new[]{(0x14b70,0x15370),(0x133e0,0x13658),(0x16958,0x169ac)})
        {int at=span.Item1-0xf800,length=span.Item2-span.Item1;Require(exe.Length>=at+length,"the original native rescue loader is incomplete.");native.Write(exe,at,length);}
        Require(Hash(native.ToArray())=="da58e9b774853661b0c9e4b118964cf46aba23c030804bfe5e1ba6f503cfd17c","the native stream reader, model reload or copy owner changed.");
        var sf=original.Subfiles();var final=written.Subfiles();Require(sf.Count==final.Count,"the retained rescue stream inventory changed.");int need=0,count=0;
        for(int n=4;n<sf.Count;n++)
        {
            var a=sf[n];var b=final[n];Require(a.Length>=36&&I(original.Bytes,a.Offset+4)==36&&I(original.Bytes,a.Offset+8)>0&&I(original.Bytes,a.Offset+12)==36+I(original.Bytes,a.Offset+8)&&I(original.Bytes,a.Offset+12)<=a.Length,"a retained native rescue stream header changed.");
            // A statue placed elsewhere takes its scene's camera track along (NativeDragonRescueCamera); nothing else changes.
            Require(NativeDragonRescueCamera.SameButCamera(original.Bytes.AsSpan(a.Offset,a.Length),written.Bytes.AsSpan(b.Offset,b.Length)),"a complete retained dragon rescue stream changed.");
            // Native14C04 sector-rounds pointer250-modelbase. Native14DF8
            // then copies readBytes-header[12], so the full sound prefix must
            // be present BEFORE the first partial-stream copy.
            // Native14E6C then reads the rest of the stream behind the moved part, up to modelbase+size-prefix.
            int prefix=I(original.Bytes,a.Offset+12);
            need=Math.Max(need,Math.Max(UniversalLevelData.Align(prefix,2048),UniversalLevelData.Align(a.Length-prefix,2048)));count++;
        }
        Require(count>0,"no retained rescue stream was authenticated.");var models=final[2];Require(statue!.Root>=models.Offset&&statue.Root<models.Offset+models.Length,"the registered dragon statue model leaves its owned model subfile.");int available=(statue.Root-models.Offset)/2048*2048;
        return new UniversalNativeDragonRescueBudget(need,available,count);
    }
}

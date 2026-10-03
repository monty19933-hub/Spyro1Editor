using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
namespace Spyro.Editor.Core.Universal;

/// <summary>Exact original High Caves animation28. This owns its native depth-scale2
/// header and record; it neither replaces a destination record nor grants caller/particle authority.</summary>
public sealed class UniversalHighCavesTornadoReactionCertificate
{
    internal UniversalHighCavesTornadoReactionCertificate(NativePlayerAnimationSource source) { Source=source; }
    internal NativePlayerAnimationSource Source { get; }
    public string SourceLevelKey => "highcaves";
    public int AnimationId => 28;
    public int FrameCount => 39;
    public int ByteLength => 19280;
    public byte NativeDepthScale => 2;
    public string RecordSha256 => UniversalHighCavesTornadoReaction.RecordHash;
    public string Scope => "original-player-animation28-header-and-native-consumers";
    public bool GameplayVerified => false;
}

public static class UniversalHighCavesTornadoReaction
{
    internal const string RecordHash="d85b2986cdc0783d4af05b862b92f31fd0fd173df302bd3934f748368fc986da";
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static readonly (uint Pc,int Length,string Hash)[] NativeOwners=
    [
        (0x80013230,0x1B0,"0cca46f0e44c16b8adea088e3081637ec5b04a512dc2c8eac494f9d16732cefb"),
        (0x80023AC4,0x1E2C,"5f151d5685850ef32ed2f55ea007cbbbb40b40dd8043231772e5c9c5da40e871"),
        (0x8003FD50,0x50,"5266a1997876840d432cfca3c1f7ece643c3981c8f6b9cda26c76a0a1b6a4c0f"),
        (0x8006C470,0x2D,"9f502dce2b68cb8497072c5c9c92d3f914710cdbbcbc9c25c86cbe29a4241e0b"),
    ];
    internal static void RequireNative(byte[] executable)
    {
        if(executable.Length!=0x66000)throw new InvalidDataException("Tornado Spyro animation needs the complete native executable.");
        foreach(var p in NativeOwners)
            if(Hash(executable.AsSpan((int)(p.Pc-UniversalExeBindings.ExeBase),p.Length))!=p.Hash)
                throw new InvalidDataException("Tornado Spyro animation lost its native loader, renderer or state consumer.");
    }
    internal static void RequireRecord(ReadOnlySpan<byte> bytes,ReadOnlySpan<byte> standardHeader)
    {
        if(bytes.Length!=19280||Hash(bytes)!=RecordHash||BinaryPrimitives.ReadInt32LittleEndian(bytes)!=28||
            BinaryPrimitives.ReadInt32LittleEndian(bytes[4..])!=19272||BinaryPrimitives.ReadInt16LittleEndian(bytes[8..])!=39)
            throw new InvalidDataException("Tornado Spyro animation differs from its complete original record.");
        // Native AnimationHeader+0B (record+13) is an animation-owned depth scale;
        // it is not a texture pointer or shared geometry root. Keep the original2.
        if(standardHeader.Length!=36||bytes[0x13]!=2||standardHeader[11]!=1)
            throw new InvalidDataException("Tornado Spyro animation has an unowned depth-scale context.");
        for(int i=2;i<36;i++)if(i!=11&&bytes[8+i]!=standardHeader[i])
            throw new InvalidDataException("Tornado Spyro animation changed shared faces, colors or geometry metadata.");
        for(int i=0;i<39;i++)if(bytes[44+i*4+3]!=255)
            throw new InvalidDataException("Tornado Spyro animation has an unowned frame sound selector.");
    }
    internal static UniversalHighCavesTornadoReactionCertificate Capture(string sourceKey,int sourceLevelId,
        NativePlayerAnimationSource source,NativePlayerAnimationRecord record,byte[] executable)
    {
        if(sourceKey!="highcaves"||sourceLevelId!=32||record.AnimationId!=28)
            throw new InvalidDataException("Tornado Spyro animation requires its original High Caves source identity.");
        RequireNative(executable);
        var level=source.Levels.Single(l=>l.LevelId==32);
        var population=source.Levels.Where(l=>l.Stream.Records.Any(r=>r.AnimationId==28)).Select(l=>l.LevelId).ToArray();
        if(source.SourceImageSha256!=NativePlayerAnimationSource.OriginalImageSha256||
            source.SharedPlayerPackageSha256!="46f570bf0ad2d433f8b42874ae40a3e5d7155d7206dddd1cd08a2d4be16e0e76"||
            level.DataSha256!="5f8f3189f0695e8710f71cc4056fc5dd677758c0cf26fd291953a650a6ada16c"||
            level.Stream.Sha256!="f5a0a2903ee500c6c1688a3f85d3450c00d4d135fc741d2b53b9d2129a059bc9"||
            !population.SequenceEqual([32])||source.SharedAnimationIds.Contains(28)||
            level.Stream.Records.Single(r=>r.AnimationId==28).Sha256!=RecordHash||record.Sha256!=RecordHash)
            throw new InvalidDataException("Tornado Spyro animation lost its original local/shared source owners.");
        var standard=source.Levels.Single(l=>l.LevelId==10).Stream.Records.Single(r=>r.AnimationId==18).CopyRecord();
        if(Hash(standard)!="c1ce1c4db1bdd656645aa62f237efd0a9b4a07dfc13e2e03ca83d697e1a419a6")
            throw new InvalidDataException("Tornado Spyro animation changed its native standard-header comparison owner.");
        RequireRecord(record.CopyRecord(),standard.AsSpan(8,36));
        return new(source);
    }
    public static void RequireDestination(UniversalHighCavesTornadoReactionCertificate certificate,
        UniversalLevelData destination,byte[] executable)
    {
        ArgumentNullException.ThrowIfNull(certificate);RequireNative(executable);
        // Reuse the exact original-source codec/merge only as a readback check.
        // It rejects changed resident28 and validates every existing destination
        // record. A missing28 would add bytes, which is explicitly refused here.
        var check=certificate.Source.MergeCaptured(destination.Bytes,32,[28]);
        if(check.AddedIds.Count!=0||check.ExactGrowth!=0||!check.CopyFinalBytes().AsSpan().SequenceEqual(destination.Bytes))
            throw new InvalidDataException("Tornado Spyro animation is absent from the final player stream.");
    }
}

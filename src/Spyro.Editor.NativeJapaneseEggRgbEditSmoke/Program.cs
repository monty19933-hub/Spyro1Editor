using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks=0;
void Check(bool value,string why){if(!value)throw new Exception(why);checks++;}
void Refuse(Action action,string why){try{action();}catch(InvalidDataException){checks++;return;}catch(ArgumentException){checks++;return;}throw new Exception("Expected refusal: "+why);}
void Immutable(Action action,string why){try{action();}catch(NotSupportedException){checks++;return;}throw new Exception("Expected immutable collection: "+why);}
static uint Word(byte[] b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(at));
static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));

void Verify(NativeJapaneseEggRgbEditSource source)
{
    var original=source.NativeOriginal();byte[] native=source.AssetAncestor.Model.CopyPackage();
    Check(original.Origin==NativeJapaneseEggPaletteOrigin.NativeOriginal&&original.NativePalettePreserved&&original.ChangedByteCount==0&&original.ChangedWordCount==0,"Native palette is an exact no-op, not an authored preset.");
    Check(original.CopyPackage().SequenceEqual(native)&&original.FinalPackageSha256==Hash(native),"Native-original model is byte exact.");
    Check(original.Fields.Count==140&&source.RgbOwners.Count==140&&original.FinalRgbOwners.Count==140,"Exactly140 three-byte fields, no CODE bytes.");
    Check(source.RgbOwners.All(o=>ReferenceEquals(o.Space,source.AssetAncestor.OriginalDataDomain)&&o.ByteLength==3)&&
        original.FinalRgbOwners.All(o=>ReferenceEquals(o.Space,original.FinalPackageDomain)&&o.ByteLength==3),"Owned edit extents retain actual source/final identities.");
    var authoredSame=source.CreateAuthored(source.NativeNearRgb.ToArray(),source.NativeFarRgb.ToArray());
    Check(authoredSame.Origin==NativeJapaneseEggPaletteOrigin.Authored&&authoredSame.NativePalettePreserved&&authoredSame.VariantIdentity==original.VariantIdentity,"Authored equality preserves explicit intent while content identity deduplicates exact bytes.");
    uint[] near=source.NativeNearRgb.ToArray(),far=source.NativeFarRgb.ToArray();near[0]=0x332211;far[^1]=0xFFFFFF;
    var authored=source.CreateAuthored(near,far);byte[] after=authored.CopyPackage();
    Check(Word(after,source.NearRange.PackageOffset)==0xFF332211&&Word(after,source.FarRange.PackageOffset+69*4)==0xFFFFFF,"Explicit 00BBGGRR uses low R and preserves FF/00 CODE.");
    Check(authored.Origin==NativeJapaneseEggPaletteOrigin.Authored&&!authored.NativePalettePreserved,"Custom arrays never acquire native-original palette provenance.");
    Check(authored.VariantIdentity==Hash(Encoding.UTF8.GetBytes("jp-f5-rgb-v1\n"+Hash(native)+"\n"+Hash(after)+"\n")),"Independent deterministic identity calculation.");
    Check(authored.ApplyExact(native).SequenceEqual(after)&&authored.RevertExact(after).SequenceEqual(native),"Exact apply and full inverse roundtrip.");
    uint[] savedNear=near.ToArray(),savedFar=far.ToArray();near[0]^=0xFF;far[^1]^=0xFF;after[0]^=1;
    Check(authored.NearRgb.SequenceEqual(savedNear)&&authored.FarRgb.SequenceEqual(savedFar)&&authored.CopyPackage()[0]==native[0],"Caller arrays and returned byte arrays cannot mutate a plan.");
    Check(source.NativeNearRgb.SequenceEqual(original.NearRgb)&&source.NativeFarRgb.SequenceEqual(original.FarRgb)&&source.AssetAncestor.Model.CopyPackage().SequenceEqual(native),"Source palette and authenticated asset remain immutable.");
    Immutable(()=>((IList<uint>)source.NativeNearRgb)[0]=0,"native near RGB");
    Immutable(()=>((IList<uint>)source.NativeFarRgb)[0]=0,"native far RGB");
    Immutable(()=>((IList<uint>)authored.NearRgb)[0]=0,"authored near RGB");
    Immutable(()=>((IList<uint>)authored.FarRgb)[0]=0,"authored far RGB");
    Immutable(()=>((IList<NativeJapaneseEggRgbField>)authored.Fields)[0]=new(false,0,0,0,0),"typed field list");
    byte[] copiedOriginal=authored.CopyOriginalPackage();copiedOriginal[0]^=1;
    Check(authored.CopyOriginalPackage().SequenceEqual(native),"Original copy also detaches.");
    Check(!source.NativeImportApproved&&!source.DistinctPerInstanceRootBindingProven&&!source.PublicationApproved&&
        !authored.NativeImportApproved&&!authored.DistinctPerInstanceRootBindingProven&&!authored.FinalPixelsVerified&&!authored.PublicationApproved,"No changed-model authority or independent runtime root binding.");
    Refuse(authored.RequireDistinctInstanceBinding,"RGB bytes do not bind model instances");
    var fields=authored.Fields.SelectMany(f=>Enumerable.Range(f.PackageOffset,3)).ToHashSet();
    byte[] actual=authored.CopyPackage();
    for(int i=0;i<native.Length;i++)if(!fields.Contains(i))Check(actual[i]==native[i],"Every CODE/geometry/frame/pointer/other byte is preserved: "+i);
    foreach(var pointer in source.AssetAncestor.PointerFields)Check(Word(actual,pointer.PackageFieldOffset)==pointer.SourceWord,"All original source-relative relocation fields preserved.");
    for(int i=0;i<native.Length;i++)
    {
        int at=i;byte[] wrong=native.ToArray();wrong[at]^=1;
        Refuse(()=>authored.ApplyExact(wrong),"changed complete source byte "+at);
        wrong=actual.ToArray();wrong[at]^=1;
        Refuse(()=>authored.RevertExact(wrong),"changed complete afterimage byte "+at);
    }
    Refuse(()=>authored.ApplyExact(native[..^1]),"truncated source");Refuse(()=>authored.RevertExact(actual[..^1]),"truncated afterimage");
    Refuse(()=>authored.ApplyExact(native.Concat(new byte[]{0}).ToArray()),"extra source tail");Refuse(()=>authored.RevertExact(actual.Concat(new byte[]{0}).ToArray()),"extra afterimage tail");
    foreach(int count in new[]{0,1,69,71,140})
    {
        Refuse(()=>source.CreateAuthored(new uint[count],new uint[70]),"near count "+count);
        Refuse(()=>source.CreateAuthored(new uint[70],new uint[count]),"far count "+count);
    }
    foreach(uint invalid in new[]{0x01000000u,0x80000000u,uint.MaxValue})foreach(int index in new[]{0,35,69})
    {
        uint[] bad=new uint[70];bad[index]=invalid;
        Refuse(()=>source.CreateAuthored(bad,new uint[70]),"near CODE input");Refuse(()=>source.CreateAuthored(new uint[70],bad),"far CODE input");
    }
    Refuse(()=>new NativeJapaneseEggRgbEditPlan(source,NativeJapaneseEggPaletteOrigin.NativeOriginal,savedNear,savedFar),"native-original label cannot be forged internally for changed RGB");
    Refuse(()=>new NativeJapaneseEggRgbEditPlan(source,(NativeJapaneseEggPaletteOrigin)99,new uint[70],new uint[70]),"unknown origin");
    uint seed=0x12345678;var identities=new HashSet<string>();
    for(int variant=0;variant<32;variant++)
    {
        uint Next(){seed=unchecked(seed*1664525+1013904223);return seed&0xFFFFFF;}
        uint[] n=Enumerable.Range(0,70).Select(_=>Next()).ToArray(),f=Enumerable.Range(0,70).Select(_=>Next()).ToArray();
        var edit=source.CreateAuthored(n,f);byte[] b=edit.CopyPackage();
        Check(identities.Add(edit.VariantIdentity)&&edit.VariantIdentity==source.CreateAuthored(n,f).VariantIdentity,"Repeatable content identity without collisions in deterministic authored cases.");
        Check(edit.ChangedByteCount<=420&&edit.ChangedWordCount<=140,"Edit budgets charge RGB fields only.");
        for(int i=0;i<70;i++)
        {
            Check(Word(b,source.NearRange.PackageOffset+i*4)==(0xFF000000|n[i]),"Authored near packed RGB and preserved CODE.");
            Check(Word(b,source.FarRange.PackageOffset+i*4)==f[i],"Authored far packed RGB and preserved CODE.");
        }
        for(int i=0;i<native.Length;i++)if(!fields.Contains(i))Check(b[i]==native[i],"Random RGB edit preserves all unrelated bytes.");
        Check(edit.RevertExact(b).SequenceEqual(native),"Every authored variant reverts to exact native source.");
        var parsed=NativeJapaneseEggModel.DecodeFixture(b);
        Check(parsed.PackageByteLength==native.Length&&!parsed.SourceGeometryClosureProven,"Existing parser readback does not mislabel authored bytes as native source.");
    }
    Check(source.CreateAuthored(new uint[70],new uint[70]).Fields.All(f=>f.FinalRgb==0),"All-black RGB is a valid explicit authored choice, not a missing field.");
}

Check(typeof(NativeJapaneseEggRgbEditSource).GetConstructors(BindingFlags.Public|BindingFlags.Instance).Length==0&&
    typeof(NativeJapaneseEggRgbEditPlan).GetConstructors(BindingFlags.Public|BindingFlags.Instance).Length==0,"No caller package/hash can directly construct source/edit authority.");
if(args.Length==0)
{
    var asset=NativeJapaneseEggModelAssetPlan.Fixture(Fixtures.Model());var source=NativeJapaneseEggRgbEditSource.Fixture(asset);Verify(source);
    Refuse(()=>NativeJapaneseEggRgbEditSource.Capture(null!,null!),"null source asset");
    Refuse(()=>NativeJapaneseEggRgbEditSource.Capture(asset,null!),"null USA source");
    Check(!source.SourceAncestryCertified&&!source.NativeOriginal().SourceAncestryCertified,"Synthetic geometry/colour fixtures cannot certify native source.");
    foreach(int count in new[]{1,69,71})
    {
        var other=NativeJapaneseEggModelAssetPlan.Fixture(Fixtures.Model(count));
        Refuse(()=>NativeJapaneseEggRgbEditSource.Fixture(other),"otherwise-valid unsupported palette count "+count);
    }
    foreach(int at in new[]{0x88+3,0x88+69*4+3,0x1A0+3,0x1A0+69*4+3})
    {
        byte[] b=Fixtures.Model();b[at]^=0x80;
        Refuse(()=>NativeJapaneseEggRgbEditSource.Fixture(NativeJapaneseEggModelAssetPlan.Fixture(b)),"unsupported native CODE byte "+at);
    }
    foreach(int length in new[]{0,0x70,0x88,0x1A0,0x2C7})
        Refuse(()=>NativeJapaneseEggRgbEditSource.Fixture(NativeJapaneseEggModelAssetPlan.Fixture(Fixtures.Model().AsSpan(0,length))),"truncated model owner "+length);
    Console.WriteLine($"JP_EGG_RGB_EDIT_SOURCE_FREE_OK checks={checks} sourceReads=0 sourceOrCandidateWrites=0");
}
else if(args.Length==3&&args[0]=="--source")
{
    using var jp=NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.JapanScps10083);
    using var usa=NativeRegionalSourceSession.Open(args[2],NativeDiscRevision.UsaScus94228);
    var asset=NativeJapaneseEggModelAssetPlan.Capture(jp,usa);var source=NativeJapaneseEggRgbEditSource.Capture(asset,usa);Verify(source);
    Check(source.SourceAncestryCertified&&source.NativeOriginal().SourceAncestryCertified&&ReferenceEquals(source.AssetAncestor,asset),"Actual model source and renderer ancestry are retained.");
    Check(source.NearRange.PackageOffset==0x45C&&source.FarRange.PackageOffset==0x574&&asset.Model.PackageByteLength==0x808,"Exact source RGB owners derive from existing model parser.");
    Check(source.OriginalPackageSha256==NativeJapaneseEggModel.SourcePackageSha256&&source.NativeOriginal().FinalPackageSha256==NativeJapaneseEggModel.SourcePackageSha256,"Native-original golden unchanged.");
    Refuse(()=>NativeJapaneseEggRgbEditSource.Capture(asset,jp),"Japanese EXE cannot substitute for actual USA renderer");
    Refuse(()=>NativeJapaneseEggRgbEditSource.Capture(NativeJapaneseEggModelAssetPlan.Fixture(Fixtures.Model()),usa),"fixture asset cannot acquire source ancestry");
    byte[] native=asset.Model.CopyPackage();
    Check(source.NativeNearRgb[0]==0x6A9230&&source.NativeFarRgb[0]==0x6A9230&&Word(native,0x45C)==0xFF6A9230&&Word(native,0x574)==0x006A9230,"Actual near/far first native RGB and independent CODE metadata.");
    jp.VerifyUnchanged();usa.VerifyUnchanged();Console.WriteLine($"JP_EGG_RGB_EDIT_SOURCE_OK checks={checks} sourcePackage={source.OriginalPackageSha256} fields=140 distinctInstanceBinding=false sourceOrCandidateWrites=0");
}
else throw new ArgumentException("Usage: no arguments, or --source <exact-JP-BIN> <exact-USA-BIN>.");

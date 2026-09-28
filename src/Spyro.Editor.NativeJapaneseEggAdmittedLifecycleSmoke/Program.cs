using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using F=AttemptFixtures;

int checks=0;
void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
void Refuse(Action action){try{action();}catch(InvalidDataException){checks++;return;}throw new Exception("Expected closed-composition refusal.");}
var manifest=NativeCollectibleCardLoadResearchComposition.FixedManifest();
var maps=new[]{new NativeJapaneseEggCollectibleIdentity("egg-a",Guid.Parse("11111111-1111-1111-1111-111111111111")),
    new NativeJapaneseEggCollectibleIdentity("egg-b",Guid.Parse("22222222-2222-2222-2222-222222222222"))};
var fixture=F.Authored();
var old=NativeJapaneseEggLifecycleOverlayPlan.Fixture(fixture,manifest,0,maps,F.Predicate());
string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
// Captured from the preserved PRE-GUARD smoke Core binary, SHA256
//20900770d8bfd6f2f5bbecc4a3cd1ecb294f0bc7f77b222953addce0bdc9a5fe,
// independently loaded before rebuilding it; that type had no LiveGuard member.
Check(Hash(old.CopyOverlay())=="6712fbc9f88088b79be897d9300d0c8ae7406da7a8c278ab93352b7eabc1271b"&&
    Hash(old.CopyExecutable())=="bc00912493d03f4c627abfc8975e22fb6c7faf841dd206506802cd6d5b43cb41"&&
    Hash(old.CopyLevelData())=="04232ed7d53edf79fdf72cd2a8bcc59314ff901ab5082cc81c4cc7d1de1dc157","Existing fixture factory matches exact pre-guard afterimages.");
var guarded=NativeJapaneseEggLifecycleOverlayPlan.FixtureGuarded(fixture,manifest,0,maps,F.Predicate());
Check(old.LiveGuard is null&&guarded.LiveGuard is not null,"Guarded route is separate; existing lifecycle route stays unchanged.");
Check(guarded.Layout.FinalByteLength<=0x10000&&guarded.Layout.CopyAnchor>old.Layout.CopyAnchor,"Guard capacity charged before final row projection within unchanged64KiB budget.");
Check(guarded.Binding.Rows.All(r=>r.RowAddress==old.Binding.Rows.Single(o=>o.RowId==r.RowId).RowAddress+
    guarded.Layout.CopyAnchor-old.Layout.CopyAnchor),"Final guard growth reprojects all actual row identities.");
Refuse(()=>NativeJapaneseEggAdmittedLifecyclePlan.Compose(NativeCollectibleCardAdmissionPlan.CreateFixture(),fixture,guarded));
AdmissionCpuProbe.Verify(NativeCollectibleCardAdmissionPlan.CreateFixture(),fixture,guarded,null,Check);
Console.WriteLine($"JP_EGG_ADMITTED_LIFECYCLE_SOURCE_FREE_OK checks={checks} imageWrites=0 cardWrites=0 runtime=false");
if(args.Length==0)return;
if(args.Length!=3||args[0]!="--source")throw new ArgumentException("Only --source JP-BIN USA-BIN; no image/card/publication mode.");
using var jp=NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.JapanScps10083);
using var usa=NativeRegionalSourceSession.Open(args[2],NativeDiscRevision.UsaScus94228);
try
{
    var p=NativeJapaneseEggAdmittedLifecyclePlan.Capture(jp,usa);p.RequireSourceComposition();
    var legacy=NativeJapaneseEggLifecycleOverlayPlan.Capture(jp,usa,p.Authored,manifest,0,maps);
    Check(Hash(legacy.CopyOverlay())=="6de1633c2407ab48d88c31f007b9e41d8f5875f1a732769ad73d36346dcf5dba"&&
        Hash(legacy.CopyExecutable())=="124d8c18ae70a464d39b05dd3278cc80b22a3d448b287799c10a3c6cdbec957b"&&
        Hash(legacy.CopyLevelData())=="6675cf971f53b6b3fbb31b0d40ffd54f5404d3b8049c1ef64fff44a068ee4dac","Existing source factory matches exact pre-guard afterimages.");
    Check(p.SourceCertified&&!p.RuntimeVerified&&!p.CardWrites&&!p.NormalCreateBinApproved&&!p.PublicationApproved,
        "Closed source composition emits research code without runtime/save/publication authority.");
    Check(p.EntryOwners.Select(e=>e.Index).SequenceEqual(new[]{2,9,10}),"Only card overlay2 and Artisans overlay9/data10 owned.");
    Check(p.CopyCardOverlay().SequenceEqual(p.Admission.CopyOverlay()),"Card admission ancestor remains byte-identical.");
    byte[] exe=p.CopyExecutable(),original=p.CopyOriginalExecutable();
    Check(p.RevertExecutableExact(exe).SequenceEqual(original),"Complete merged EXE original inverse.");
    Check(p.RevertCardOverlayExact(p.CopyCardOverlay()).SequenceEqual(usa.ReadWadEntry(2))&&
        p.RevertArtisansOverlayExact(p.CopyArtisansOverlay()).SequenceEqual(usa.ReadWadEntry(9))&&
        p.RevertLevelDataExact(p.CopyLevelData()).SequenceEqual(usa.ReadWadEntry(10)),"All three complete original WAD owner inverses.");
    foreach(int at in new[]{0,0x10,0x2F00,0x3030,0x1EAFC,0x23E24,0x4AF18,0x4AF20,0x65DA4,0x65E40,0x65FFF})
    {var bad=exe.ToArray();bad[at]^=1;Refuse(()=>p.RevertExecutableExact(bad));}
    foreach(var pair in new[]{(p.CopyCardOverlay(),(Action<byte[]>)(b=>p.RevertCardOverlayExact(b))),
        (p.CopyArtisansOverlay(),(Action<byte[]>)(b=>p.RevertArtisansOverlayExact(b))),
        (p.CopyLevelData(),(Action<byte[]>)(b=>p.RevertLevelDataExact(b)))})
    {foreach(int at in new[]{0,pair.Item1.Length-1}){var bad=pair.Item1.ToArray();bad[at]^=1;Refuse(()=>pair.Item2(bad));}}
    AdmissionCpuProbe.Verify(p.Admission,p.Authored,p.Lifecycle,exe,Check);
    var changed=manifest.Advance([Guid.Parse("33333333-3333-3333-3333-333333333333")],[]);
    Refuse(()=>p.Lifecycle.Binding.RequireCurrent(p.Authored,changed));
    Refuse(()=>NativeJapaneseEggAdmittedLifecyclePlan.Compose(p.Admission,fixture,guarded));
    Check(BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(0x65DA4))==0xA00,"No old800 reservation regression during EXE merge.");
    var projection=p.Lifecycle.Projection;var guard=p.Lifecycle.LiveGuard!;
    Console.WriteLine("ADMITTED_LAYOUT "+JsonSerializer.Serialize(new{p.ExecutableSha256,p.EntryOwners,
        projection.CopyAnchor,projection.ModelAddress,projection.SceneAddress,projection.SceneEnd,
        guard.BeforeEntryPc,guard.AfterEntryPc,guard.ValidatePc,guard.CodeByteLength,
        guardSha256=Convert.ToHexStringLower(SHA256.HashData(guard.CopyBytes())),globals=guard.ExpectedSceneGlobals.Select(g=>new{g.Address,g.Value}),
        rows=p.Lifecycle.Binding.Rows,models=projection.Models.Select(m=>new{m.RowId,m.RootAddress,m.RowAddress,m.PrivateAddress,
            nearColor=m.RootAddress+0x45C,farColor=m.RootAddress+0x574})}));
    Console.WriteLine($"JP_EGG_ADMITTED_LIFECYCLE_SOURCE_OK checks={checks} overlay={p.CopyArtisansOverlay().Length:X} anchor={p.Lifecycle.Layout.CopyAnchor:X8} imageWrites=0 cardWrites=0 runtime=false");
}
finally{try{jp.VerifyUnchanged();}finally{usa.VerifyUnchanged();}}

using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class NativeMobyModelTextureCases
{
    internal static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        byte[] Fixture()
        {
            byte[] b=new byte[0xA0];Put(b,0,1);Put(b,0x34,0x80);Put(b,0x38,0x3C);
            Put(b,0x80,20);Put(b,0x84,2);byte[] texture=[0,0,0x20,0,31,0,8,0,31,31,0,31];texture.CopyTo(b,0x8C);return b;
        }
        byte[] b=Fixture();var model=NativeMobyModelTextureLayout.Decode(b);
        check(model.References.Count==1&&model.References[0].PackageOffset==0x8C&&model.FaceTableOffsets.SequenceEqual(new[]{0x80}),"Model texture parser missed the native triangle descriptor.");
        check(!model.CompleteModelClosureProven&&!model.PublicationApproved,"Texture grammar became complete native model/export authority.");
        var packed=b.ToArray();packed[0x3C]=5;packed[0x40]=1;
        check(NativeMobyModelTextureLayout.Decode(packed).References.Count==1,"Native packed single-word frame headers were measured as paired frames.");
        byte[] detached=model.References[0].CopyBytes();detached[0]=77;
        check(model.References[0].CopyBytes()[0]==0,"Texture reference aliases exposed mutable payload.");
        var rebased=NativeMobyModelTextureLayout.Rebase(model.References[0],32,64,0x1234,0x19);
        check(rebased[0]==32&&rebased[1]==64&&rebased[4]==63&&rebased[9]==95&&BinaryPrimitives.ReadUInt16LittleEndian(rebased.AsSpan(2))==0x1234,"Texture UV/palette/page rebasing changed source geometry.");
        reject(()=>NativeMobyModelTextureLayout.Rebase(model.References[0],240,0,0x1234,0x19),"UV");
        reject(()=>NativeMobyModelTextureLayout.Rebase(model.References[0],0,0,0x1234,0x39),"material");
        var bad=b.ToArray();Put(bad,0,0xFFFFFFFF);reject(()=>NativeMobyModelTextureLayout.Decode(bad),"owner");
        bad=b.ToArray();Put(bad,0x38,4);reject(()=>NativeMobyModelTextureLayout.Decode(bad),"overlap");
        bad=b.ToArray();Put(bad,0x34,0x3C);reject(()=>NativeMobyModelTextureLayout.Decode(bad),"overlap");
        bad=b.ToArray();Put(bad,0x80,24);reject(()=>NativeMobyModelTextureLayout.Decode(bad),"owner");
        bad=b.ToArray();bad[0x92]=0x88;reject(()=>NativeMobyModelTextureLayout.Decode(bad),"4-bpp");
        bad=b.ToArray();Put(bad,0x38,0x3D);reject(()=>NativeMobyModelTextureLayout.Decode(bad),"aligned");
        bad=b.ToArray();Put(bad,0x80,16);reject(()=>NativeMobyModelTextureLayout.Decode(bad),"owner");
        bad=b.ToArray();Put(bad,0x50,0x7FFFFFFC);reject(()=>NativeMobyModelTextureLayout.Decode(bad),"owner");
        foreach(uint command in new uint[]{0,0x80000000,0x80000002,0x80000004})
        {
            var x=Fixture();int length=command==0?8:command==0x80000000?12:command==0x80000002?24:20;
            Put(x,0x80,(uint)length);Put(x,0x84,command);
            if(command==0x80000002){Array.Clear(x,0x8C,12);model.References[0].CopyBytes().CopyTo(x,0x90);}
            var parsed=NativeMobyModelTextureLayout.Decode(x);
            check(parsed.References.Count==((command&6)!=0?1:0),"Native untextured/quad/special renderer command grammar changed.");
        }
        var empty=new byte[0x80000];
        var atlas=new NativeMobyIndexedTextureAllocation(empty,[]);
        var first=atlas.Reserve(32,32);var second=atlas.Reserve(32,32);
        check(first!=second&&first.PixelOffset!=second.PixelOffset&&first.PaletteOffset!=second.PaletteOffset,"Private texture allocations alias mutable target storage.");
        check(first.U+32<=256&&first.V+32<=256&&first.Page>=8&&first.PaletteOffset%32==0,"Native texture allocation is not descriptor encodable.");
        var repeat=new NativeMobyIndexedTextureAllocation(empty,[]);
        check(first==repeat.Reserve(32,32)&&second==repeat.Reserve(32,32),"Private texture allocation order is not reproducible.");
        reject(()=>new NativeMobyIndexedTextureAllocation(empty,[new(0,0x80001,"bad")]),"protected extent");
        var full=new NativeMobyIndexedTextureAllocation(empty,[new(0,0x80000,"all consumers")]);
        reject(()=>full.Reserve(32,32),"pixel rectangle");
        reject(()=>atlas.Reserve(31,32),"dimensions");
        // The first tentative pixel rectangle consumes the only complete CLUT
        // run. Refusal must roll it back so a smaller request can still succeed.
        var constrained=Enumerable.Repeat((byte)1,0x80000).ToArray();Array.Clear(constrained,0,2);Array.Clear(constrained,32,32);
        var transaction=new NativeMobyIndexedTextureAllocation(constrained,[]);
        reject(()=>transaction.Reserve(64,1),"CLUT");
        var recovered=transaction.Reserve(2,1);
        var fresh=new NativeMobyIndexedTextureAllocation(constrained,[]);
        check(recovered==fresh.Reserve(2,1),"Failed pixel/CLUT reservation committed partial ownership.");
        check(empty.All(v=>v==0),"Private atlas allocation modified its source buffer.");
        Console.WriteLine("NATIVE_MOBY_TEXTURE_RENDERER_COMMAND_GRAMMAR_AND_REFERENCE_REBASE_OK");
    }
    internal static void Source(string image,Action<bool,string> check)
    {
        var plan=NativeDarkHollowWizardTexturePlan.Capture(image);
        check(plan.WizardTextureReferences==51&&plan.LightningTextureReferences==9&&plan.Allocations.Count==5,"Exact native texture dependency field/tile counts changed.");
        check(plan.Patches.Count==198&&plan.Patches.Count(p=>p.Owner=="target-vram")==137,"Native pixel/CLUT/descriptor extraction coverage changed.");
        check(!plan.CompleteNativeClosureProven&&!plan.RuntimeFitProven&&!plan.PublicationApproved,"Texture fit granted intact native Wizard/export approval.");
        check(plan.Allocations.Sum(a=>a.RowBytes*a.Rows)==2056&&plan.Allocations.Count*32==160,"Native texture pixel/palette byte count changed.");
        byte[] package=plan.CopyWizardPackage();package[0]^=1;
        check(plan.CopyWizardPackage()[0]==3,"Detached model package aliases the source plan.");
        byte[] target=plan.CopyTexturePatchedTargetLevel();target[^1]^=1;
        check(plan.CopyTexturePatchedTargetLevel()[^1]!=target[^1],"Detached target VRAM plan aliases immutable output.");
        check(plan.CopyWizardPackage().AsSpan(4,3).SequenceEqual(new byte[]{0x31,0x32,0x33}),"Texture relocation silently rewrote unresolved model-local sound selectors.");
        check(plan.FinalTextureDataSha256=="9c62ac9289cfdbac95c3bcc89a0cb8fdcc0f3ca8e53c7dbdbc601fd152d9f0e1"&&
            NativeMobyModelTextureLayout.Decode(plan.CopyWizardPackage()).PackageSha256=="776a28cdb88cd542a10898b372c3dbd5fe95357046f7f72641d14e309b7759f3"&&
            NativeMobyModelTextureLayout.Decode(plan.CopyLightningPackage()).PackageSha256=="51748c9b1eea600542e5f4988078ddcb8149e77156674faa2fe0539f1906fc88",
            "Native Dark texture/model source-output byte goldens changed.");
        foreach(var a in plan.Allocations)
            Console.WriteLine($"NATIVE_DARK_WIZARD_PRIVATE_TILE {a.Id} pixels={a.TargetPixelOffset:X}/{a.RowBytes:X}x{a.Rows} clut={a.TargetPaletteOffset:X} sha={a.PixelsSha256}");
        Console.WriteLine($"NATIVE_DARK_WIZARD_TEXTURE_SOURCE_OK: target {plan.FinalTextureDataSha256}; wizard {NativeMobyModelTextureLayout.Decode(plan.CopyWizardPackage()).PackageSha256}; lightning {NativeMobyModelTextureLayout.Decode(plan.CopyLightningPackage()).PackageSha256}");
    }
    private static void Put(byte[] b,int o,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(o),v);
}

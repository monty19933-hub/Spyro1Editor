using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class NativeMobyTextureMaterializationCases
{
    // Neutral byte/geometry tests only: no native source, GPU lifetime or admission certificate.
    internal static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        byte[] source=Enumerable.Range(0,0x80000).Select(i=>(byte)((i*37+i/1024)%251)).ToArray();
        byte[] target=new byte[0x80000];target[128]=0xA5;
        byte[] sourceBefore=source.ToArray(),targetBefore=target.ToArray();
        NativeTexturePageOwnedRange[] protectedRanges=[new(0,64,"live-zero-pixels-and-clut")];
        NativeMobyTextureTileRequest first=new("z-first",0x2A0,0x28,4,20,11,22);
        NativeMobyTextureTileRequest edge=new("a-edge",0x7FFF,0x1F,240,250,255,255);
        NativeMobyTextureTileRequest[] requests=[first,edge,first with {Id="equal-distinct-owner"}];
        var plan=NativeMobyTextureMaterialization.Compile(source,target,protectedRanges,requests);
        byte[] output=plan.CopyUpload();bool[] written=new bool[output.Length];
        check(plan.Locations.Count==3&&plan.Allocations.Select(a=>a.Id).SequenceEqual(requests.Select(r=>r.Id))&&
            plan.Patches.Count==15,"Every ordered request retains its own allocation, including equal source tiles with distinct IDs.");
        for(int n=0;n<requests.Length;n++)
        {
            var r=requests[n];var at=plan.Locations[n];var evidence=plan.Allocations[n];
            int rowBytes=(r.MaxU-r.MinU+1)/2,rows=r.MaxV-r.MinV+1;
            int sourcePixel=(((r.Page&16)!=0?256:0)+r.MinV)*1024+(r.Page&15)*128-1024+r.MinU/2;
            int sourceClut=(r.Palette>>6)*1024+(r.Palette&63)*32-1024;
            byte[] pixels=new byte[rowBytes*rows];
            for(int row=0;row<rows;row++)
            {
                sourceBefore.AsSpan(sourcePixel+row*1024,rowBytes).CopyTo(pixels.AsSpan(row*rowBytes));
                check(output.AsSpan(at.PixelOffset+row*1024,rowBytes).SequenceEqual(sourceBefore.AsSpan(sourcePixel+row*1024,rowBytes)),
                    "Each complete source pixel row is installed at the allocator-derived target row.");
            }
            check(output.AsSpan(at.PaletteOffset,32).SequenceEqual(sourceBefore.AsSpan(sourceClut,32))&&
                at.PixelRowBytes==rowBytes&&at.Rows==rows&&
                at.PixelOffset==(((at.Page&16)!=0?256:0)+at.V)*1024+(at.Page&15)*128-1024+at.U/2&&
                at.PaletteOffset==(at.Palette>>6)*1024+(at.Palette&63)*32-1024&&
                at.U+rowBytes*2<=256&&at.V+rows<=256,
                "Physical pixel/CLUT owners round-trip through actual descriptor encodings without crossing a page.");
            check(evidence==new NativeMobyTextureAllocationEvidence(r.Id,sourcePixel,at.PixelOffset,rowBytes,rows,
                sourceClut,at.PaletteOffset,Hash(pixels),Hash(sourceBefore.AsSpan(sourceClut,32))),
                "Allocation evidence derives complete source and destination extents and payload hashes in request order.");
        }
        foreach(var patch in plan.Patches)
        {
            check(patch.Owner=="target-vram"&&patch.Offset>=0&&patch.ByteLength>0&&patch.Offset<=output.Length-patch.ByteLength&&
                patch.CopyBefore().SequenceEqual(targetBefore.AsSpan(patch.Offset,patch.ByteLength).ToArray())&&
                patch.CopyAfter().SequenceEqual(output.AsSpan(patch.Offset,patch.ByteLength).ToArray()),
                "Every patch owns exact upload-relative before/after bytes, not a guessed level-file offset.");
            for(int i=patch.Offset;i<patch.Offset+patch.ByteLength;i++)
            {check(!written[i]&&i>=64&&i!=128,"New detached owners do not overlap each other or live zero/nonzero owners.");written[i]=true;}
        }
        check(Enumerable.Range(0,output.Length).All(i=>written[i]||output[i]==targetBefore[i])&&
            source.SequenceEqual(sourceBefore)&&target.SequenceEqual(targetBefore),
            "The entire unallocated upload complement and both input buffers remain exact.");
        var repeat=NativeMobyTextureMaterialization.Compile(source,target,protectedRanges,requests);
        check(repeat.CopyUpload().SequenceEqual(output)&&repeat.Locations.SequenceEqual(plan.Locations)&&
            repeat.Allocations.SequenceEqual(plan.Allocations),"Repeated complete inputs yield identical allocation and output bytes.");
        byte[] descriptor=[4,20,0,0,11,20,0,0,4,22,11,22];
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(2),first.Palette);
        BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(6),first.Page);
        var location=plan.Locations[0];ushort finalPage=(ushort)((first.Page&~0x1F)|location.Page);
        byte[] rebased=NativeMobyModelTextureLayout.Rebase(new(0,descriptor),location.U-4,location.V-20,location.Palette,finalPage);
        check((BinaryPrimitives.ReadUInt16LittleEndian(rebased.AsSpan(6))&0x20)!=0&&
            NativeMobyModelTextureLayout.Rebase(new(0,rebased),4-location.U,20-location.V,first.Palette,first.Page).SequenceEqual(descriptor),
            "Materialization accepts TPAGE material bit0x20; the existing descriptor rebase preserves it and exactly inverts.");
        var empty=NativeMobyTextureMaterialization.Compile(source,target,protectedRanges,[]);
        check(empty.Locations.Count==0&&empty.Patches.Count==0&&empty.Allocations.Count==0&&empty.CopyUpload().SequenceEqual(target),
            "An empty request list is an exact copied no-op, not a fabricated texture owner.");

        foreach(int length in new[]{0,0x7FFFF,0x80001})
        {reject(()=>NativeMobyTextureMaterialization.Compile(new byte[length],target,[],[]),"upload");
         reject(()=>NativeMobyTextureMaterialization.Compile(source,new byte[length],[],[]),"upload");}
        foreach(var bad in new[]{first with {Id=""},first with {Id=" "},first with {MinU=-2},first with {MinV=-1},
            first with {MaxU=256},first with {MaxV=256},first with {MinU=5},first with {MaxU=10},
            first with {MaxU=3},first with {MaxV=19},first with {Page=0x88},first with {Id=null!}})
            reject(()=>NativeMobyTextureMaterialization.Compile(source,target,[],[bad]),"request");
        reject(()=>NativeMobyTextureMaterialization.Compile(source,target,[],[first,first]),"request");
        reject(()=>NativeMobyTextureMaterialization.Compile(source,target,[],[null!]),"request");
        foreach(var bad in new[]{first with {Page=7},first with {Palette=0},first with {Palette=0xFFFF}})
            reject(()=>NativeMobyTextureMaterialization.Compile(source,target,[],[bad]),"upload");
        foreach(var bad in new NativeTexturePageOwnedRange[]{new(-1,1,"bad"),new(0,0,"bad"),new(0x7FFFF,2,"bad"),null!})
            reject(()=>NativeMobyTextureMaterialization.Compile(source,target,[bad],[first]),"protected");
        byte[] full=Enumerable.Repeat((byte)1,0x80000).ToArray();
        reject(()=>NativeMobyTextureMaterialization.Compile(source,full,[],[first]),"No descriptor-encodable");
        reject(()=>NativeMobyTextureMaterialization.Compile(source,target,[new(0,0x80000,"all-live-zero")],[first]),"No descriptor-encodable");
        reject(()=>NativeMobyTextureMaterialization.Compile(source,target,[],[first,edge with {Id="invalid-later",MinU=241}]),"request");
        check(target.SequenceEqual(targetBefore)&&source.SequenceEqual(sourceBefore),"A later failed request cannot expose a partial transaction or mutate either input.");
        // Finite hostile input: the old unbounded snapshot safely reaches the
        // duplicate-ID refusal, which is not the required defensive bound.
        var falseCount=new FalseCountRequests(first);
        reject(()=>NativeMobyTextureMaterialization.Compile(source,target,[],falseCount),"defensive compiler bound");
        check(falseCount.Yielded==4097&&falseCount.Disposed,
            "The 4097th enumerated declaration is refused and disposed even when IReadOnlyList.Count reports zero.");
        reject(()=>NativeMobyTextureMaterialization.Compile(source,target,[],Enumerable.Repeat(first,4097).ToArray()),"defensive compiler bound");
        Null(()=>NativeMobyTextureMaterialization.Compile(source,target,null!,[]),check);
        Null(()=>NativeMobyTextureMaterialization.Compile(source,target,[],null!),check);
        ReadOnly(plan.Locations,check);ReadOnly(plan.Patches,check);ReadOnly(plan.Allocations,check);
        var patchBefore=plan.Patches[0].CopyBefore();var patchAfter=plan.Patches[0].CopyAfter();
        source[20482]^=1;source[10240]^=1;target[0]^=1;requests[0]=edge;protectedRanges[0]=new(0,0x80000,"changed");
        output[0]^=1;patchBefore[0]^=1;patchAfter[0]^=1;
        check(plan.CopyUpload().SequenceEqual(repeat.CopyUpload())&&plan.Locations.SequenceEqual(repeat.Locations)&&
            plan.Allocations.SequenceEqual(repeat.Allocations)&&plan.Patches[0].CopyBefore().SequenceEqual(repeat.Patches[0].CopyBefore())&&
            plan.Patches[0].CopyAfter().SequenceEqual(repeat.Patches[0].CopyAfter()),
            "Caller buffers, request/range arrays, output copies and patch copies cannot mutate the frozen transaction.");
        Console.WriteLine("NATIVE_MOBY_TEXTURE_MATERIALIZATION_INSTALL_COMPLEMENT_PROTECTION_ENCODING_IMMUTABILITY_OK nativeAdmission=false");
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private sealed class FalseCountRequests(NativeMobyTextureTileRequest request):IReadOnlyList<NativeMobyTextureTileRequest>
    {
        public int Count=>0;
        public NativeMobyTextureTileRequest this[int index]=>throw new InvalidOperationException("The false-count probe must be enumerated.");
        internal int Yielded{get;private set;}
        internal bool Disposed{get;private set;}
        public IEnumerator<NativeMobyTextureTileRequest> GetEnumerator()
        {try{for(int i=0;i<5000;i++){Yielded++;yield return request;}}finally{Disposed=true;}}
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    private static void Null(Action action,Action<bool,string> check)
    {try{action();}catch(ArgumentNullException){check(true,"Null collections are refused explicitly.");return;}check(false,"Expected null-collection refusal.");}
    private static void ReadOnly<T>(IReadOnlyList<T> values,Action<bool,string> check)
    {check(values is ICollection<T> {IsReadOnly:true},"Returned collections are read-only snapshots.");
     if(values is IList<T> list){try{list[0]=list[0];}catch(NotSupportedException){check(true,"Collection mutation is refused.");return;}
         check(false,"A frozen collection allowed replacement.");}}
}

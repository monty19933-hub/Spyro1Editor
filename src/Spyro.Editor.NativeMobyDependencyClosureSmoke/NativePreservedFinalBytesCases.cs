using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

internal static class NativePreservedFinalBytesCases
{
    internal static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        var method=typeof(NativeMobyDependencyClosurePlan).GetMethod("RequirePreservedFinalBytes",BindingFlags.Instance|BindingFlags.NonPublic);
        check(method is {IsAssembly:true}&&method.ReturnType==typeof(void)&&
            method.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{
                typeof(NativeAddressSpace),typeof(ReadOnlySpan<byte>),typeof(Func<NativeByteExtent,byte[]>)}),
            "Preservation verification is an internal void check, not an issuer or native capability result.");

        byte[] original=Enumerable.Range(0,96).Select(i=>(byte)i).ToArray();
        var donor=new NativeAddressSpace(NativeAddressKind.LevelData,"preservation-source",NativeAddressRevision.Original,original.Length);
        var target=new NativeAddressSpace(NativeAddressKind.LevelData,"preservation-final",NativeAddressRevision.Final,128);
        NativeMobyDependencyRead Read(string id,int offset,int length)=>new(id,new(donor,offset,length),Hash(original.AsSpan(offset,length)));
        byte[] Reader(NativeByteExtent extent)
        {
            if(!ReferenceEquals(extent.Space,donor))throw new InvalidDataException("The actual Original source domain was replaced.");
            return original.AsSpan((int)extent.Offset,(int)extent.ByteLength).ToArray();
        }
        NativeMobyDependencyNode Resident(string id,NativeMobyDependencyRead read,params NativeMobyDependencyPreservation[] preserved)=>
            new(id,NativeMobyDependencyKind.Sound,NativeMobyDependencyResolution.DestinationResident,
                NativeMobyDependencySharing.Immutable,read.ExpectedSha256,[],[read],Preserved:preserved);
        NativeMobyDependencyClosurePlan Compile(params NativeMobyDependencyNode[] nodes)=>
            NativeMobyDependencyClosureCompiler.Compile(nodes,nodes.Select(n=>n.Id),Reader);
        byte[] Final(NativeMobyDependencyClosurePlan graph)
        {
            byte[] bytes=Enumerable.Repeat((byte)0xA5,128).ToArray();
            foreach(var node in graph.Nodes)
            foreach(var preserved in node.Preserved)
            {
                var read=node.Reads.Single(r=>r.Id==preserved.ReadId);
                Reader(read.Extent).AsSpan(preserved.ReadOffset,(int)preserved.Final.ByteLength)
                    .CopyTo(bytes.AsSpan((int)preserved.Final.Offset));
            }
            return bytes;
        }

        var firstRead=Read("shared",0,16);
        var first=Resident("a.first",firstRead,new("shared",4,new(target,8,8)),new("shared",0,new(target,32,4)));
        var plan=Compile(first);byte[] complete=Final(plan);string fingerprint=plan.Fingerprint;
        int calls=0;
        plan.RequirePreservedFinalBytes(target,complete,e=>{calls++;return Reader(e);});
        check(calls==1&&complete[32]==0,
            "Split preservations re-read their one complete node/read only once and verify referenced zero bytes.");
        var second=Resident("b.second",Read("shared",32,16),new NativeMobyDependencyPreservation("shared",2,new(target,56,8)));
        var pair=Compile(first,second);byte[] pairFinal=Final(pair);string pairFingerprint=pair.Fingerprint;
        List<long> offsets=[];
        pair.RequirePreservedFinalBytes(target,pairFinal,e=>{offsets.Add(e.Offset);return Reader(e);});
        check(offsets.SequenceEqual(new long[]{0,32}),
            "Identical read IDs in different nodes resolve independently to their actual source extents.");
        var sameExtent=Resident("b.same-extent",firstRead,new NativeMobyDependencyPreservation("shared",4,new(target,56,8)));
        var samePair=Compile(first,sameExtent);calls=0;
        samePair.RequirePreservedFinalBytes(target,Final(samePair),e=>{calls++;return Reader(e);});
        check(calls==2,"Even equal extent/hash/read-ID tuples remain separate node/read callback obligations.");
        var overlap=Resident("b.overlap",Read("overlap",4,8),new NativeMobyDependencyPreservation("overlap",2,new(target,10,4)));
        var overlapping=Compile(first,overlap);
        overlapping.RequirePreservedFinalBytes(target,Final(overlapping),Reader);
        check(overlapping.Nodes.Sum(n=>n.Preserved.Count)==3,
            "Equal overlapping preservation promises are checked independently, not rejected or promoted to broader sharing.");

        // Preflight the entire owner set and all argument geometry before any
        // callback, even when the first matching owner would otherwise succeed.
        byte[] Counted(NativeByteExtent extent){calls++;return Reader(extent);}
        void PreflightFailure<T>(Action action,string label)where T:Exception
        {calls=0;Refuse<T>(action,check,label);check(calls==0,label+" precedes every Original callback.");}
        PreflightFailure<ArgumentNullException>(()=>plan.RequirePreservedFinalBytes(null!,complete,Counted),"Null Final domain refusal");
        PreflightFailure<ArgumentNullException>(()=>plan.RequirePreservedFinalBytes(target,complete,null!),"Null Original reader refusal");
        foreach(var replacement in new[]{
            new NativeAddressSpace(target.Kind,target.OwnerIdentity,target.Revision,target.ByteLength),
            new(target.Kind,target.OwnerIdentity,NativeAddressRevision.Original,target.ByteLength),
            new(NativeAddressKind.Scene,target.OwnerIdentity,NativeAddressRevision.Final,target.ByteLength),
            new(target.Kind,target.OwnerIdentity,NativeAddressRevision.Final,target.ByteLength+1)})
            PreflightFailure<InvalidDataException>(()=>plan.RequirePreservedFinalBytes(replacement,complete,Counted),
                "A matching label or alternate revision/kind/length cannot replace the actual Final-domain object");
        foreach(byte[] bytes in new[]{Array.Empty<byte>(),complete[..^1],new byte[129]})
            PreflightFailure<InvalidDataException>(()=>plan.RequirePreservedFinalBytes(target,bytes,Counted),"Incomplete or oversized Final extent refusal");
        var otherTarget=new NativeAddressSpace(target.Kind,target.OwnerIdentity,target.Revision,target.ByteLength);
        var mixed=Compile(first,Resident("z.other-domain",Read("other",48,8),new NativeMobyDependencyPreservation("other",0,new(otherTarget,80,8))));
        PreflightFailure<InvalidDataException>(()=>mixed.RequirePreservedFinalBytes(target,complete,Counted),
            "A later preservation in an equal-labelled different domain cannot be silently skipped");
        var empty=Compile(new NativeMobyDependencyNode("empty",NativeMobyDependencyKind.Bundle,NativeMobyDependencyResolution.Aggregate,
            NativeMobyDependencySharing.Immutable,Hash([]),[],[]));
        calls=0;reject(()=>empty.RequirePreservedFinalBytes(target,complete,Counted),"preserv");
        check(calls==0,"No preservation promises means refusal, not vacuous single-domain success.");

        foreach(var fault in new Func<NativeByteExtent,byte[]>[]{
            _=>null!,e=>Reader(e)[..^1],e=>[..Reader(e),(byte)0],
            e=>{byte[] bytes=Reader(e);bytes[4]^=1;return bytes;},
            e=>{byte[] bytes=Reader(e);bytes[15]^=1;return bytes;}})
            Refuse<InvalidDataException>(()=>plan.RequirePreservedFinalBytes(target,complete,fault),check,
                "Every preserved read needs its full declared length/hash, including bytes outside the preserved slice");
        original[15]^=1;
        Refuse<InvalidDataException>(()=>plan.RequirePreservedFinalBytes(target,complete,Reader),check,
            "A successful earlier verification cannot cache stale Original preimages across calls");
        original[15]^=1;
        foreach(int at in new[]{8,11,15,32,33,35})
        {
            byte[] bad=complete.ToArray();bad[at]^=1;
            Refuse<InvalidDataException>(()=>plan.RequirePreservedFinalBytes(target,bad,Reader),check,
                "First, interior, last and zero-valued preserved Final bytes are all compared");
        }

        // All full Original reads must be detached before final comparisons.
        // Both reads have the same length, so a scratch-buffer reader is legal.
        byte[] scratch=new byte[16];calls=0;
        pair.RequirePreservedFinalBytes(target,pairFinal,e=>{calls++;Reader(e).CopyTo(scratch,0);return scratch;});
        check(calls==2&&scratch.SequenceEqual(original.AsSpan(32,16).ToArray()),
            "A later callback may reuse/mutate its earlier borrowed buffer without corrupting an already copied Original preimage.");
        byte[] hostileFinal=pairFinal.ToArray();calls=0;
        Refuse<InvalidDataException>(()=>pair.RequirePreservedFinalBytes(target,hostileFinal,e=>{
            calls++;if(calls==2)hostileFinal[8]^=1;return Reader(e);}),check,
            "Collect-then-compare detects a later callback mutating an earlier Final range; an early Final clone cannot hide it");
        check(calls==2&&hostileFinal[8]!=pairFinal[8],"The hostile mutation actually occurred in the earlier preservation range.");
        var primary=new IOException("actual later preservation-reader failure");
        byte[] initiallyWrong=pairFinal.ToArray();initiallyWrong[8]^=1;calls=0;
        try
        {
            pair.RequirePreservedFinalBytes(target,initiallyWrong,e=>{
                calls++;if(calls==2)throw primary;return Reader(e);});
            throw new InvalidDataException("Expected actual later Original reader exception.");
        }
        catch(IOException error)
        {
            check(ReferenceEquals(error,primary)&&calls==2,
                "All Original callbacks precede final comparisons, and the actual reader exception propagates unchanged.");
        }

        // A compiled graph can contain installs and relocated references. This
        // method checks only Preserved declarations, never those other owners.
        var modelRead=Read("model-payload",64,8);
        var model=new NativeMobyDependencyNode("model",NativeMobyDependencyKind.ActorPackage,
            NativeMobyDependencyResolution.ImportFromSource,NativeMobyDependencySharing.Immutable,modelRead.ExpectedSha256,[],[modelRead],
            c=>new(c.Read("model-payload"),[new("model",new(target,80,8))],[]));
        var pointerRead=Read("pointer-payload",72,8);
        var pointer=new NativeMobyDependencyNode("pointer",NativeMobyDependencyKind.ActorPackage,
            NativeMobyDependencyResolution.ImportFromSource,NativeMobyDependencySharing.Immutable,pointerRead.ExpectedSha256,["model"],[pointerRead],
            c=>new(c.Read("pointer-payload"),[new("pointer",new(target,96,8))],
                [new("model-pointer",0,"model",4,4,BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(72,4)))]));
        var scoped=Compile(first,model,pointer);byte[] scopedFinal=Final(scoped);string scopedFingerprint=scoped.Fingerprint;
        foreach(var install in scoped.Installs)
            scoped.CopyPayload(install.NodeId).CopyTo(scopedFinal,(int)install.Destination.Offset);
        check(scoped.Installs.Count==2&&scoped.References.Count==1&&
            BinaryPrimitives.ReadUInt32LittleEndian(scopedFinal.AsSpan(96,4))==84,
            "Scope fixture contains genuinely compiled installed bytes and a resolved native reference.");
        byte[] modelPayload=scoped.CopyPayload("model"),pointerPayload=scoped.CopyPayload("pointer");
        scopedFinal[80]^=1;scopedFinal[96]^=1;scopedFinal[127]^=1;calls=0;
        scoped.RequirePreservedFinalBytes(target,scopedFinal,e=>{
            calls++;if(e.Offset>=64)throw new InvalidDataException("Unpreserved import reads are outside this verifier's scope.");return Reader(e);});
        check(calls==1&&scopedFinal[80]!=modelPayload[0]&&
            BinaryPrimitives.ReadUInt32LittleEndian(scopedFinal.AsSpan(96,4))!=84&&scopedFinal[127]!=0xA5,
            "Changed install, reference and complement bytes remain deliberately outside a successful preservation-only check.");
        byte[] complementOnly=complete.ToArray();complementOnly[127]^=1;
        plan.RequirePreservedFinalBytes(target,complementOnly,Reader);
        plan.RequirePreservedFinalBytes(target,complete,Reader);
        pair.RequirePreservedFinalBytes(target,pairFinal,Reader);
        check(plan.Fingerprint==fingerprint&&pair.Fingerprint==pairFingerprint&&scoped.Fingerprint==scopedFingerprint&&
            Compile(first).Fingerprint==fingerprint&&Compile(first,second).Fingerprint==pairFingerprint&&
            scoped.CopyPayload("model").SequenceEqual(modelPayload)&&scoped.CopyPayload("pointer").SequenceEqual(pointerPayload)&&
            original.SequenceEqual(Enumerable.Range(0,96).Select(i=>(byte)i)),
            "Verification, callback faults and retries never mutate graph fingerprints, detached payloads or Original source bytes.");
        Console.WriteLine("NATIVE_PRESERVED_FINAL_BYTES_OK sourceOpened=false nativeAdmission=false installsAndComplementChecked=false");
    }

    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Refuse<T>(Action action,Action<bool,string> check,string reason)where T:Exception
    {try{action();}catch(T){check(true,reason);return;}throw new InvalidDataException("Expected "+typeof(T).Name+": "+reason);}
}

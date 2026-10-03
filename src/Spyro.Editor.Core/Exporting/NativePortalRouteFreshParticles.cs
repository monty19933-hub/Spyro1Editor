using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>One exact issued initializer and a separately source-owned copied
/// updater chain. This issues no particle caller or new type12 authority.</summary>
public sealed class NativeFreshParticleBinding
{
    internal NativeFreshParticleBinding(UniversalCollectionStarContinuation continuation, byte[] initializer)
    { Continuation=continuation; Initializer=initializer.ToArray(); }
    internal UniversalCollectionStarContinuation Continuation { get; }
    internal byte[] Initializer { get; }
    public uint ParticleUpdate => Continuation.Entry;
    public uint NativeContinuation => Continuation.Native;
}

public static partial class NativePortalRouteFreshBinding
{
    private const uint NativeParticleSpawn=0x80088F68, NativeParticleUpdate=0x80087E20;
    private static readonly uint[] OwnedGlobals=[..Overridden,UniversalExeBindings.ParticleUpdateGlobal];

    // The old three-override packet remains byte-for-byte unchanged. This fourth
    // setter runs only for65. The branch-delay LUI is dead on the Town path:
    // CommonTail first overwrites v0, then loads its native copy pointer.
    private static byte[] BuildOwnedParticles(uint copy,uint init,uint update,uint particle,uint townCopy,uint townUpdate)
    {
        if(new[]{copy,init,update,particle,townCopy,townUpdate}.Any(v=>!Pointer(v)))
            throw new InvalidDataException("Fresh owned callbacks must remain aligned resident addresses.");
        var p=new UniversalAsmPart();void I(int op,int rs,int rt,int imm)=>p.Emit(UniversalMips.EncodeI(op,rs,rt,imm));
        I(15,0,1,0x8008);I(9,1,1,-32768);I(15,0,3,0x8008);
        foreach(var(global,retailValue) in Retail)
        {
            uint value=global==UniversalExeBindings.CopyBufferGlobal?townCopy:global==UniversalExeBindings.UpdateGlobal?townUpdate:retailValue;
            long delta=(long)value-0x80080000;
            if(delta>=short.MinValue&&delta<=short.MaxValue)I(9,3,2,(int)delta);
            else if((value>>16)==0x8008)I(13,3,2,(int)(value&0xFFFF));
            else if(global is UniversalExeBindings.CopyBufferGlobal or UniversalExeBindings.UpdateGlobal)
            {I(15,0,2,UniversalMips.Hi(value));I(9,2,2,UniversalMips.Lo(value));}
            else throw new InvalidDataException("Fresh retail callback escaped its exact source literal base.");
            I(43,1,2,checked((short)((long)global-GlobalBase)));
        }
        I(35,1,2,checked((short)((long)0x8007596C-GlobalBase)));I(9,0,3,65);
        p.Emit(UniversalMips.EncodeI(5,2,3,0),new(UniversalFixKind.BranchLabel,Label:"tail"));
        I(15,0,2,UniversalMips.Hi(copy));
        uint previous=0;
        foreach(var(global,value) in OwnedGlobals.Zip(new[]{copy,init,update,particle}))
        {
            long delta=(long)value-previous;
            if(global==Overridden[0])I(9,2,2,UniversalMips.Lo(value));
            else if(delta>=short.MinValue&&delta<=short.MaxValue)I(9,2,2,(int)delta);
            else {I(15,0,2,UniversalMips.Hi(value));I(9,2,2,UniversalMips.Lo(value));}
            I(43,1,2,checked((short)((long)global-GlobalBase)));previous=value;
        }
        p.Label("tail");p.Emit(UniversalMips.EncodeJ(2,CommonTail));p.Emit(0);
        byte[] code=UniversalBehaviorBundleBuilder.Assemble([p],Address).Code;
        if(code.Length>Length)throw new InvalidDataException("The certified fresh particle route exceeds its native initializer owner.");
        byte[] result=new byte[Length];code.CopyTo(result,0);return result;
    }

    internal static bool TryReadOwnedParticles(ReadOnlySpan<byte> executable,int levelId,out IReadOnlyDictionary<uint,UniversalExeBinding> bindings)
    {
        bindings=new Dictionary<uint,UniversalExeBinding>();
        if(executable.Length!=0x66000||UniversalExeBindings.Word(executable,UniversalExeBindings.LevelInitJumpTable+(uint)levelId*4)!=Address)return false;
        byte[] owner=executable.Slice((int)(Address-UniversalExeBindings.ExeBase),Length).ToArray();
        uint W(int at){if(at<0||at+4>Length)throw new InvalidDataException("Fresh particle decode escaped its initializer owner.");return BinaryPrimitives.ReadUInt32LittleEndian(owner.AsSpan(at,4));}
        // A source legacy or original three-override packet belongs to its old
        // parser. Recognize this variant only by its exact branch-delay LUI.
        if(W(0)!=UniversalMips.EncodeI(15,0,1,0x8008))return false;
        int cursor=12;var values=new Dictionary<uint,uint>();
        foreach(var(global,_) in Retail)
        {
            uint materializer=W(cursor),value;
            if(UniversalMips.Op(materializer)==15&&UniversalMips.Rt(materializer)==2)
            {value=unchecked((uint)((UniversalMips.Imm(materializer)<<16)+UniversalMips.SImm(W(cursor+4))));cursor+=8;}
            else if(UniversalMips.Op(materializer)==9&&UniversalMips.Rs(materializer)==3&&UniversalMips.Rt(materializer)==2)
            {value=unchecked((uint)(0x80080000L+UniversalMips.SImm(materializer)));cursor+=4;}
            else if(UniversalMips.Op(materializer)==13&&UniversalMips.Rs(materializer)==3&&UniversalMips.Rt(materializer)==2)
            {value=0x80080000u|(uint)UniversalMips.Imm(materializer);cursor+=4;}
            else throw new InvalidDataException("Fresh particle initializer retail materializer changed.");
            if(W(cursor)!=UniversalMips.EncodeI(43,1,2,checked((short)((long)global-GlobalBase))))throw new InvalidDataException("Fresh particle initializer retail store changed.");
            cursor+=4;values.Add(global,value);
        }
        uint delay=W(cursor+12);
        if(UniversalMips.Op(delay)!=15||UniversalMips.Rs(delay)!=0||UniversalMips.Rt(delay)!=2)return false;
        cursor+=16;var owned=new Dictionary<uint,uint>();uint previous=(uint)UniversalMips.Imm(delay)<<16;
        foreach(uint global in OwnedGlobals)
        {
            uint w=W(cursor),value;
            if(UniversalMips.Op(w)==9&&UniversalMips.Rs(w)==2&&UniversalMips.Rt(w)==2)
            {value=unchecked((uint)((long)previous+UniversalMips.SImm(w)));cursor+=4;}
            else if(UniversalMips.Op(w)==15&&UniversalMips.Rs(w)==0&&UniversalMips.Rt(w)==2)
            {value=unchecked((uint)((UniversalMips.Imm(w)<<16)+UniversalMips.SImm(W(cursor+4))));cursor+=8;}
            else throw new InvalidDataException("Fresh particle initializer owned materializer changed.");
            if(W(cursor)!=UniversalMips.EncodeI(43,1,2,checked((short)((long)global-GlobalBase))))throw new InvalidDataException("Fresh particle initializer owned store changed.");
            cursor+=4;owned.Add(global,value);previous=value;
        }
        if(!owner.SequenceEqual(BuildOwnedParticles(owned[Overridden[0]],owned[Overridden[1]],owned[Overridden[2]],owned[UniversalExeBindings.ParticleUpdateGlobal],values[Overridden[0]],values[Overridden[2]])))
            throw new InvalidDataException("Fresh particle initializer differs from its complete canonical owner.");
        if(levelId==65)foreach(var(global,value) in owned)values[global]=value;
        bindings=values.ToDictionary(p=>p.Key,p=>new UniversalExeBinding(p.Key,0,0,p.Value));return true;
    }

    internal static void RequireNativeParticleInitializer(byte[] executable)
    {
        RequireKnownDispatcher(executable);
        uint entry=UniversalExeBindings.Word(executable,UniversalExeBindings.LevelInitJumpTable+65*4);
        if(entry!=Address||UniversalExeBindings.Word(executable,UniversalExeBindings.LevelInitJumpTable+13*4)!=Address)
            throw new InvalidDataException("Fresh particle destination lost its source Town/65 initializer entry.");
        bool compact=TryRead(executable,65,out var bindings);
        if(!compact)
        {
            var bytes=executable.AsSpan((int)(Address-UniversalExeBindings.ExeBase),Length);
            if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=SourceSha256)throw new InvalidDataException("Fresh native particle initializer has an unowned legacy edit.");
            bindings=UniversalExeBindings.Read(executable,65);
        }
        if(bindings[UniversalExeBindings.ParticleSpawnGlobal].Value!=NativeParticleSpawn||bindings[UniversalExeBindings.ParticleUpdateGlobal].Value!=NativeParticleUpdate)
            throw new InvalidDataException("Fresh particle source needs its unchanged native constructor and updater.");
    }
    private static void RequireKnownDispatcher(byte[] executable)
    {
        if(executable.Length!=0x66000)throw new InvalidDataException("Fresh particles need the complete native executable.");
        if(Convert.ToHexStringLower(SHA256.HashData(executable.AsSpan((int)(CommonTail-UniversalExeBindings.ExeBase),24)))!="19566edf84d04523d6cc943feb5107817297413669d3838b10ef868db744151d")throw new InvalidDataException("Fresh particle common tail changed.");
        foreach(var p in CallerOwners)if(Convert.ToHexStringLower(SHA256.HashData(executable.AsSpan((int)(p.Address-UniversalExeBindings.ExeBase),p.Length)))!=p.Sha256)throw new InvalidDataException("Fresh particle initializer dispatcher ownership changed.");
    }
    private static void RequireContinuation(byte[] executable,UniversalOverlay overlay,UniversalCollectionStarContinuation continuation)
    {
        if(continuation.Native!=NativeParticleUpdate||continuation.Entry==continuation.Native||!overlay.Contains(continuation.Start)||(ulong)continuation.Start+(uint)continuation.Length>overlay.End||
            Convert.ToHexStringLower(SHA256.HashData(overlay.Bytes.Slice((int)(continuation.Start-UniversalOverlay.Base),continuation.Length)))!=continuation.Sha)
            throw new InvalidDataException("Fresh copied particle chain lost its issued code/table/native continuation.");
        // Bind the entry metadata to the unique already issued chain. A copied
        // hash alone cannot authorize entering its interior or another wrapper.
        byte[] code=overlay.Bytes.Slice((int)(continuation.Start-UniversalOverlay.Base),continuation.Length).ToArray();
        uint W(int at)=>BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan(at,4));
        List<uint> entries=[];
        for(int first=0;first+48<=code.Length;first+=4)
        {
            if(W(first)!=UniversalMips.EncodeI(9,29,29,-32)||W(first+4)!=UniversalMips.EncodeI(43,29,31,24)||W(first+8)!=UniversalMips.EncodeI(43,29,4,20))continue;
            int at=first+12,calls=0;
            while(at+16<=code.Length&&W(at)==UniversalMips.EncodeI(35,29,4,20)&&W(at+4)==0&&UniversalMips.IsJal(W(at+8))&&W(at+12)==0)
            {
                uint target=UniversalMips.BranchTarget(W(at+8),continuation.Start+(uint)at+8);
                if(target<continuation.Start||target>=(ulong)continuation.Start+(uint)continuation.Length)break;
                at+=16;calls++;
            }
            if(calls>0&&at+20<=code.Length&&W(at)==UniversalMips.EncodeI(35,29,31,24)&&W(at+4)==UniversalMips.EncodeI(35,29,4,20)&&W(at+8)==UniversalMips.EncodeI(9,29,29,32)&&W(at+12)==UniversalMips.EncodeJ(2,continuation.Native)&&W(at+16)==0)
                entries.Add(continuation.Start+(uint)first);
        }
        if(entries.Count!=1||entries[0]!=continuation.Entry)throw new InvalidDataException("Fresh particles lost their unique scoped calls and once-only native tail.");
        var bindings=UniversalExeBindings.Read(executable,65).ToDictionary();
        if(bindings[UniversalExeBindings.ParticleSpawnGlobal].Value!=NativeParticleSpawn)throw new InvalidDataException("Fresh copied particle chain changed the native constructor.");
        bindings[UniversalExeBindings.ParticleUpdateGlobal]=bindings[UniversalExeBindings.ParticleUpdateGlobal] with{Value=NativeParticleUpdate};
        var owner=UniversalCollectionStarParticles.RequireOwner(new UniversalOverlay(overlay.Bytes.ToArray(),bindings));
        if(owner.Key!="townsquare"||owner.Update!=continuation.Native)throw new InvalidDataException("Fresh copied particle chain lost its exact native Town dispatcher.");
        UniversalCollectionStarParticles.RequireNative(executable);
    }
    public static NativeFreshParticleBinding RebindOwnedParticleContinuation(byte[] originalExecutable,byte[] stagedExecutable,
        UniversalOverlay finalOverlay,UniversalCollectionStarContinuation continuation,uint newUpdate,uint newInit,uint newCopyBuffer)
    {
        RequireOwners(originalExecutable,stagedExecutable);RequireKnownDispatcher(stagedExecutable);
        var fresh=UniversalExeBindings.Read(stagedExecutable,65);var town=UniversalExeBindings.Read(stagedExecutable,13);
        if(fresh[UniversalExeBindings.ParticleUpdateGlobal].Value!=NativeParticleUpdate&&fresh[UniversalExeBindings.ParticleUpdateGlobal].Value!=continuation.Entry)
            throw new InvalidDataException("Fresh particle rebinding cannot replace another updater owner.");
        if(!TryRead(stagedExecutable,65,out _))RequireNativeParticleInitializer(stagedExecutable);
        RequireContinuation(stagedExecutable,finalOverlay,continuation);
        byte[] code=BuildOwnedParticles(newCopyBuffer,newInit,newUpdate,continuation.Entry,town[Overridden[0]].Value,town[Overridden[2]].Value);
        code.CopyTo(stagedExecutable.AsSpan((int)(Address-UniversalExeBindings.ExeBase),Length));
        var result=new NativeFreshParticleBinding(continuation,code);RequireOwnedParticleDestination(originalExecutable,stagedExecutable,finalOverlay,result);return result;
    }
    public static void RequireOwnedParticleDestination(byte[] originalExecutable,byte[] executable,UniversalOverlay finalOverlay,NativeFreshParticleBinding binding)
    {
        RequireOwners(originalExecutable,executable);RequireKnownDispatcher(executable);
        if(!executable.AsSpan((int)(Address-UniversalExeBindings.ExeBase),Length).SequenceEqual(binding.Initializer)||!TryReadOwnedParticles(executable,65,out var fresh)||!TryReadOwnedParticles(executable,13,out var town))
            throw new InvalidDataException("Fresh owned particle initializer changed after issue.");
        if(fresh[UniversalExeBindings.ParticleUpdateGlobal].Value!=binding.ParticleUpdate||town[UniversalExeBindings.ParticleUpdateGlobal].Value!=binding.NativeContinuation)
            throw new InvalidDataException("Fresh particles lost their independent65/native Town routes.");
        RequireContinuation(executable,finalOverlay,binding.Continuation);
    }
    internal static NativeFreshParticleBinding RebindOwnedParticleTown(byte[] originalExecutable,byte[] executable,
        UniversalOverlay freshOverlay,NativeFreshParticleBinding binding,uint townUpdate,uint townCopy)
    {
        RequireOwnedParticleDestination(originalExecutable,executable,freshOverlay,binding);
        var fresh=UniversalExeBindings.Read(executable,65);
        byte[] code=BuildOwnedParticles(fresh[Overridden[0]].Value,fresh[Overridden[1]].Value,fresh[Overridden[2]].Value,
            binding.ParticleUpdate,townCopy,townUpdate);
        code.CopyTo(executable.AsSpan((int)(Address-UniversalExeBindings.ExeBase),Length));
        var result=new NativeFreshParticleBinding(binding.Continuation,code);
        RequireOwnedParticleDestination(originalExecutable,executable,freshOverlay,result);return result;
    }
}

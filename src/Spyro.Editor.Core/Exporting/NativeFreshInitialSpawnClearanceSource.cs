using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Scene;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeFreshInitialSpawnClearanceSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>Closed original/source-owned initial Wizard adapter; no caller certificate or writable native payload.</summary>
internal sealed class NativeFreshInitialSpawnClearanceSource
{
    internal const string ExecutableHash="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    internal const string WizardHash="070a57069c8747ada4caacc0394b93e9a970e5ba7ff21159584abdc7a0fd4faf";
    private NativeFreshInitialSpawnClearanceSource(NativeInitialSpawnClearanceFinding finding,List<NativeInitialSpawnClearanceEvidence> evidence)
    {Finding=finding;Evidence=evidence.AsReadOnly();}
    internal NativeInitialSpawnClearanceFinding Finding{get;}
    internal IReadOnlyList<NativeInitialSpawnClearanceEvidence> Evidence{get;}

    internal static void RequireDocument(NativeFreshLevelDocumentIntent document,NativeId65FreshWizardResearchScenePlan scene)
    {
        ArgumentNullException.ThrowIfNull(document);ArgumentNullException.ThrowIfNull(scene);
        scene.RequireAncestry(scene.CompilationAncestor,scene.FamilyAncestor,scene.ResourceAncestor);
        if((scene.AuthoringDocumentAncestor is not null&&!ReferenceEquals(document,scene.AuthoringDocumentAncestor))||
            !ReferenceEquals(document.Base,scene.CompilationAncestor.Intent)||document.Placements.Count!=1||
            document.Placements[0] is not NativeFreshWizardRouteIntent wizard||scene.PlacedCount!=1||
            new NativeWizardPlacementIntent(wizard.OriginX,wizard.OriginY,wizard.OriginZ,wizard.EndX,wizard.EndY)!=scene.Placement)
            throw new InvalidDataException("This research adapter requires one exact aggregate Wizard route and the SAME actual compiled Base/scene.");
        // These are geometric authoring restrictions, not a collider certificate.
        NativeWizardPlacementGeometry.Validate(scene.Placement);
        if(!scene.CopyCanonicalDocument().AsSpan().SequenceEqual(NativeId65FreshDocumentCodec.SerializeCanonical(document.Base)))
            throw new InvalidDataException("Actual compiled Base canonical bytes changed.");
    }

    internal static NativeFreshInitialSpawnClearanceSource Capture(FileStream image,NativeFreshLevelDocumentIntent document,
        NativeId65FreshWizardResearchScenePlan scene,CancellationToken token)
    {
        RequireDocument(document,scene);token.ThrowIfCancellationRequested();
        var layout=DiscImage.DetectLayout(image);
        var exeRecord=DiscImage.FindRootFileRecord(image,layout,n=>n.Equals("SCUS_942.28",StringComparison.OrdinalIgnoreCase));
        if(layout.SectorSize!=2352||layout.UserOffset!=24||exeRecord.Lba!=53875||exeRecord.Size!=0x66000)
            throw new InvalidDataException("Initial-pose native source executable owner changed.");
        byte[] exe=DiscImage.ReadFileBytes(image,layout,exeRecord.Lba,0,exeRecord.Size);
        if(exe.Length!=0x66000||Hash(exe)!=ExecutableHash||!exe.AsSpan().SequenceEqual(
            scene.CompilationAncestor.FreshResourceAncestor.ResourceChainAncestor.Resources.Surface.CopySourceExecutable()))
            throw new InvalidDataException("Complete actual original and compiled source executable disagree.");
        var evidence=ValidateExecutable(exe).ToList();
        var family=scene.FamilyAncestor;var resource=scene.ResourceAncestor;
        if(family.SourceImageSha256!=NativeMobySourceInventory.OriginalImageSha256||family.DonorLevelId!=33||family.DonorTrueIndex!=6)
            throw new InvalidDataException("Initial collider requires actual source Wizard33/T6 family.");
        byte[] package=family.CopyOwner("wizard.model"),models=resource.CopyModelPayload(),bytes=scene.CopyScene();
        if(package.Length!=0x35B4||Hash(package)!=WizardHash)throw new InvalidDataException("Complete source-owned Wizard package changed.");
        RequireInstalledOwners(package,models,resource.ModelRoots,resource.Owners);
        scene.RequireExactScene(bytes);
        if(bytes.Length!=0x3000||I32(bytes,0xD0)!=1||I32(bytes,0xCC)!=0xEB8||
            I32(bytes,0xE0)!=scene.Placement.OriginX||I32(bytes,0xE4)!=scene.Placement.OriginY||I32(bytes,0xE8)!=scene.Placement.OriginZ)
            throw new InvalidDataException("Actual scene placed-row owner changed.");
        var spawn=new NativeSpawnClearancePoint(I32(bytes,0),I32(bytes,4),I32(bytes,8));
        if(spawn!=new NativeSpawnClearancePoint(document.Base.SpawnX,document.Base.SpawnY,document.Base.SpawnZ))
            throw new InvalidDataException("Actual source scene spawn differs from authored Base.");
        Add("source-complete-Wizard33-T6","family:wizard.model",0,package);
        Add("installed-Wizard-header-and-animation","resource:models",0x13B94,models.AsSpan(0x13B94,0x224));
        Add("installed-frame0-vertex0","resource:models",0x14B30,models.AsSpan(0x14B30,4));
        Add("installed-sphere-command-vertex-flags","resource:models",0x17124,models.AsSpan(0x17124,16));
        Add("generated-independent-lookahead-owner","resource:models",0x17134,models.AsSpan(0x17134,4));
        Add("actual-initial-placed-row","scene",0xD4,bytes.AsSpan(0xD4,0x58));Add("actual-spawn","scene",0,bytes.AsSpan(0,12));
        token.ThrowIfCancellationRequested();
        var finding=EvaluateWizard(models.AsSpan(0x13B80,0x35B4),bytes.AsSpan(0xD4,0x58),spawn,document.Placements[0].EditorId);
        return new(finding,evidence);
        void Add(string id,string domain,int at,ReadOnlySpan<byte> b)=>evidence.Add(new(id,domain,at,b.Length,Hash(b)));
    }

    internal static void RequireInstalledOwners(ReadOnlySpan<byte> package,ReadOnlySpan<byte> models,
        IReadOnlyList<NativeFreshWizardModelRoot> roots,IReadOnlyList<NativeFreshWizardResourceOwner> owners)
    {
        // The issued resource partitions three fixed domains into positive
        // extents. Strided upload owners are not bounded by a guessed row count;
        // even a one-byte-per-owner partition cannot exceed their total bytes.
        const int maximumOwners=0xE5800+0x8FC+0x17800;
        if(package.Length!=0x35B4||models.Length!=0x17800||roots.Count!=4||owners.Count<=0||owners.Count>maximumOwners)
            throw new InvalidDataException("Bounded actual resource/model owner count changed.");
        foreach(var owner in owners)
        {
            if(owner is null)throw new InvalidDataException("Missing resource extent owner.");
            int domainLength=owner.Domain switch{
                NativeFreshWizardResourceDomain.Upload=>0xE5800,
                NativeFreshWizardResourceDomain.Data=>0x8FC,
                NativeFreshWizardResourceDomain.Models=>0x17800,
                _=>throw new InvalidDataException("Unknown resource extent domain.")};
            if(owner.Offset<0||owner.Offset>domainLength||owner.ByteLength<=0||owner.ByteLength>domainLength-owner.Offset)
                throw new InvalidDataException("Resource owner must have a positive bounded extent in its actual domain.");
        }
        var root=roots.SingleOrDefault(r=>r.ClassId==0x11B);
        if(root is null||root.Offset!=0x13B80||root.ByteLength!=0x35B4||root.RegistrationExtentByteLength!=0x3C80)
            throw new InvalidDataException("Source Wizard model registration root/extent changed.");
        // Verify the complete model owner partition, not just selected zero words.
        int cursor=0;
        foreach(var owner in owners.Where(o=>o.Domain==NativeFreshWizardResourceDomain.Models))
        {
            if(owner.Offset!=cursor||owner.ByteLength<=0||owner.ByteLength>models.Length-cursor||Hash(models.Slice(cursor,owner.ByteLength))!=owner.Sha256)
                throw new InvalidDataException("Actual models have an unowned, overlapping or changed byte extent.");
            cursor+=owner.ByteLength;
        }
        if(cursor!=models.Length)throw new InvalidDataException("Actual model owner partition is incomplete.");
        var lookahead=owners.SingleOrDefault(o=>o.Domain==NativeFreshWizardResourceDomain.Models&&o.Offset==0x17134);
        if(lookahead is null||lookahead.ByteLength!=0x6CC||lookahead.Provenance!="generated-model-padding"||
            lookahead.SourceOwnerId is not null||lookahead.SourceOffset is not null||models[0x17134..].ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Collider delayed lookahead needs its separate generated owner; equal zero bytes are not ownership.");
        foreach(var span in new[]{(At:0x14,N:0x224),(At:0xFB0,N:4),(At:0x35A4,N:16)})
            if(!package.Slice(span.At,span.N).SequenceEqual(models.Slice(root.Offset+span.At,span.N)))
                throw new InvalidDataException("Installed collider metadata/vertex/descriptor lost source complement.");
    }

    // Neutral bounded grammar only. Unsupported *semantics* return Unknown;
    // malformed lengths, owner addresses or row class throw instead of being
    // silently transformed into a nullable/radius-zero shape.
    internal static NativeInitialSpawnClearanceFinding EvaluateWizard(ReadOnlySpan<byte> package,ReadOnlySpan<byte> row,
        NativeSpawnClearancePoint spawn,string editorId)
    {
        if(package.Length!=0x35B4||row.Length!=0x58)throw new InvalidDataException("Complete model/placed row required before reading fields.");
        if(U32(package,0)!=3||U32(package,0x14)!=0x800035A4||U32(package,0x34)!=0x238||
            U32(package,0x38)!=0x44||U32(package,0x3C)!=0x100||U32(package,0x40)!=0x19C||
            BinaryPrimitives.ReadUInt16LittleEndian(row[0x36..])!=0x11B)
            throw new InvalidDataException("Initial Wizard model/animation/row ownership grammar changed.");
        for(int i=1;i<8;i++)if(U32(package,0x14+i*4)!=0)throw new InvalidDataException("Unbound extra collider root.");
        if(row[0x3C]!=0||row[0x3E]!=0||(U32(row,0x44)&0xFFFFFF)!=0||row.Slice(0x20,18).ContainsAnyExcept((byte)0))
            return Unknown(NativeInitialSpawnUnknownReason.UnsupportedPose);
        if(BinaryPrimitives.ReadInt16LittleEndian(package[0x44..])!=19)
            throw new InvalidDataException("Animation0 complete frame table bound changed.");
        if(package[0x48]!=0||package[0x49]!=1)return Unknown(NativeInitialSpawnUnknownReason.UnsupportedAnimationEncoding);
        uint frame=U32(package,0x68);int selector=(int)((frame>>19)&0x1C);
        uint selected=U32(package,0x14+selector);
        if(selected==0)return Unknown(NativeInitialSpawnUnknownReason.NullCollider);
        if(selector!=0||selected!=0x800035A4)throw new InvalidDataException("Selected collider has no owned descriptor.");
        uint command=U32(package,0x35A8),index=U32(package,0x35AC),flags=U32(package,0x35B0);
        long at=0x238L+(frame&0x1FFFFF)+4L*index;
        if(index!=0||at<0x238||at>0x35A4-4)throw new InvalidDataException("Selected vertex aliases metadata/descriptor or escapes geometry owner.");
        if((command&0xFFFF)!=0x0301||(command>>16)!=400||U32(package,0x35A4)!=2011)
            return Unknown(NativeInitialSpawnUnknownReason.UnsupportedResponseSemantics);
        uint packed=U32(package,(int)at);
        int x=(unchecked((int)packed)>>21)<<1,y=(unchecked((int)(packed<<10))>>21)<<1,z=(unchecked((int)(packed<<20))>>19)<<1;
        // 4E8E4..F0 ADDs signed y to (z<<16), rather than masking/ORing:
        // negative y borrows from the high half. Preserve that native packing.
        uint packedV=unchecked(((uint)z<<16)+(uint)y);
        int vx=unchecked((short)packedV),vy=unchecked((short)(packedV>>16));
        // 52758's zero-angle branches produce diag4096 and translation0;
        // RTPS sf=1/lm=0 returns IR=(vx,vy,x).
        // For this bounded identity subcase: signed16 inputs, products fit44-bit
        // MAC and exact12-bit shift; IR values do not saturate. Perspective/SZ
        // are not consumed here. This is arithmetic, not GTE execution.
        if(x<short.MinValue||x>short.MaxValue||y<short.MinValue||y>short.MaxValue||z<short.MinValue||z>short.MaxValue)
            return Unknown(NativeInitialSpawnUnknownReason.UnsupportedTransformRange);
        var vertex=new NativeSpawnClearancePoint(x,y,z);
        var centre=new NativeSpawnClearancePoint(checked(I32(row,12)+x),checked(I32(row,16)-vx),checked(I32(row,20)-vy));
        // The subsequent native distance instruction consumes signed16 IR
        // deltas too. Do not generalize this exact local-pose witness to an
        // unbounded world-distance/native-saturation claim.
        if((long)centre.X-spawn.X is <short.MinValue or >short.MaxValue||
            (long)centre.Y-spawn.Y is <short.MinValue or >short.MaxValue||
            (long)centre.Z-spawn.Z is <short.MinValue or >short.MaxValue)
            return Unknown(NativeInitialSpawnUnknownReason.UnsupportedTransformRange);
        // Damage flags feed a distinct accumulator. Never use flags==0 to omit
        // the command's independently enabled positional response.
        var status=NativeInitialSpawnClearanceMath.Classify(centre,400,spawn,356);
        return new(editorId,status,NativeInitialSpawnUnknownReason.None,spawn,vertex,centre,400,flags,true);
        NativeInitialSpawnClearanceFinding Unknown(NativeInitialSpawnUnknownReason why)=>new(editorId,NativeInitialSpawnClearanceStatus.Unknown,why,spawn);
    }

    internal static IReadOnlyList<NativeInitialSpawnClearanceEvidence> ValidateExecutable(ReadOnlySpan<byte> exe)
    {
        if(exe.Length!=0x66000||Hash(exe)!=ExecutableHash)throw new InvalidDataException("Complete exact native executable required.");
        List<NativeInitialSpawnClearanceEvidence> evidence=[];
        foreach(var s in new[]{
            ("player-default-mode1-radius356",0x80040500u,0x940,"f7b7a6aee6b1ca474b5d088c64afa42b2ab95103aab67e44889c58971aa58714"),
            ("collider-shape-transform-response",0x8004E3C8u,0x7E0,"7bb841a0f73d9d42c747f8cc51667d7ce1721b725f77abbec6d4f40275c17504"),
            ("collider-selector-initial-identity",0x800526A8u,0x670,"f8a0459a64054397765abd0f44396bdbb7260cfe4df36111a6f8a1ecad1fc809"),
            ("model-root-eight-pointer-relocation",0x800133E0u,0x280,"4d38a5e373feaf98cf6478f43db3638121d24a909adce47c9aafb0cdd5f72cad"),
            ("animation-frame-native-relocation",0x80013DA0u,0x360,"1fa240fcd4808a454521efde3727b207739f28d9c9c7996d22b65beca7d74ffa")})
        {
            int at=checked((int)(s.Item2-0x8000F800));
            if(Hash(exe.Slice(at,s.Item3))!=s.Item4)throw new InvalidDataException("Native initial-pose consumer window changed.");
            evidence.Add(new(s.Item1,"original-executable",at,s.Item3,s.Item4));
        }
        foreach(var p in new (uint Pc,uint Word)[]{
            (0x80040764,0x24140001),(0x80040904,0x24050164),(0x80040914,0xAFA00010),(0x80040918,0x0C0138F2),(0x8004091C,0xAFB40014),
            (0x8004E8C4,0x000D5D43),(0x8004E8C8,0x000D6280),(0x8004E8CC,0x000C6543),(0x8004E8D0,0x000D6D00),(0x8004E8D4,0x000D6CC3),
            (0x8004E8D8,0x01CB5804),(0x8004E8DC,0x01CC6004),(0x8004E8E0,0x01CD6804),
            (0x8004E8E4,0x000D7400),(0x8004E8E8,0x01CC7020),(0x8004E8EC,0x488B0800),(0x8004E8F0,0x488E0000),(0x8004E8FC,0x4A180001),
            (0x8004E900,0x480C5800),(0x8004E904,0x480D4800),(0x8004E908,0x480E5000),
            (0x8004E90C,0x01836020),(0x8004E910,0x008D6822),(0x8004E914,0x00AE7022),
            (0x8004E924,0x00025402),(0x8004E928,0x01565020),(0x8004E984,0x8C30FFFC),(0x8004E98C,0x01F07825),(0x8004E990,0xAE8F0000),
            (0x8004E994,0x304F0002),(0x8004E998,0x15E0FEFD),(0x8004E9A0,0x326F0001),(0x8004E9A4,0x11E00007),
            (0x8004EB4C,0xAF970000),(0x8004EB50,0xAF980004),(0x8004EB54,0xAF990008),
            (0x80052770,0x200A1000),(0x80052774,0x200B0000),(0x80052778,0x200C1000),(0x8005277C,0x200D0000),(0x80052780,0x200E1000),
            (0x800527AC,0x1080002A),(0x8005285C,0x1080002B),(0x80052910,0x10800027)})
            if(U32(exe,checked((int)(p.Pc-0x8000F800)))!=p.Word)throw new InvalidDataException("Pinned native input/transform/response operand changed.");
        return evidence.AsReadOnly();
    }

    internal static Task<T> WithOriginalAsync<T>(Stream image,Func<T> action,CancellationToken token)=>WithSnapshotAsync(image,
        NativeMobySourceInventory.OriginalImageByteLength,NativeMobySourceInventory.OriginalImageSha256,action,token);
    // Shared snapshot checker with a neutral control seam: no stream/callback
    // can construct the private Plan. Final SHA/true EOF always uses None after
    // successful preauthentication, including error and late cancellation.
    internal static async Task<T> WithSnapshotAsync<T>(Stream image,long length,string hash,Func<T> action,CancellationToken token)
    {
        // A failed/partial initial authentication never executes the action and
        // has no already-authenticated owner to revalidate through this wrapper.
        await NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,length,hash,token);
        T result=default!;List<Exception> failures=[];
        try{token.ThrowIfCancellationRequested();result=action();}
        catch(Exception error){failures.Add(error);}
        finally
        {
            try
            {
                image.Position=0;
                await NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,length,hash,CancellationToken.None);
            }
            catch(Exception error){failures.Add(error);}
        }
        if(failures.Count==1)ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if(failures.Count>1)throw new AggregateException("Initial clearance source action/final validation failures (primary first).",failures);
        token.ThrowIfCancellationRequested();return result;
    }
    private static uint U32(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
    private static int I32(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
    internal static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}

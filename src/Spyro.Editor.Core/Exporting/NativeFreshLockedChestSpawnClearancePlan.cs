using System.Buffers.Binary;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Closed source-owned assessment of the actual placed AE's initial type1
/// sphere and the authored player's default mode1/radius356 query. The strict
/// XY policy also separates a fixed-XY landing column, without guessing a
/// grounded player Z. Neither the first native query, actual trajectory,
/// approach accessibility, other actors nor whole-family safety is certified.
/// Compose diagnoses the historical overlapping placement without changing it;
/// a future admission consumer must explicitly call RequireStaticLandingClearance.
/// </summary>
internal sealed class NativeFreshLockedChestSpawnClearancePlan
{
    private const string PackageHash="8edc9e9ac1e5fb1a92224d2f5f8ca541371940c1ae3f9873c02f6b802d6e22e7";
    private const int PackageLength=0x1008,DescriptorOffset=0xFF8,ChestRadius=600,PlayerRadius=356;
    private NativeFreshLockedChestSpawnClearancePlan(NativeFreshLockedChestPlacementPlan placement)
    {
        PlacementAncestor=placement;RequireAncestry(placement);
        var body=BodyAncestor;var host=HostAncestor;var source=host.FamilySourceAncestor;
        var resources=body.ResourceAncestor;
        resources.RequireLockedChestResourceAncestry(host.CurrentExecutableAncestor.FamilyCompositionAncestor!,source);
        var floor=resources.BaseBodyAncestor.HostAncestor.SceneAncestor.CompilationAncestor.Intent;
        byte[] scene=placement.CopyScene(),placedBody=placement.CopyLevelData(),package=source.CopyPackage();
        if(package.Length!=PackageLength||Hash(package)!=PackageHash||
            source.PackageRead.Extent.ByteLength!=PackageLength||source.PackageRead.ExpectedSha256!=PackageHash||
            scene.Length!=0x3800||placedBody.Length!=0x20F000||I32(scene,0xD0)!=9)
            throw new InvalidDataException("Chest clearance lost its complete actual source package or placed P9 Body.");
        int sceneOffset=I32(placedBody,0x18),sceneLength=I32(placedBody,0x1C);
        RequireExtent(sceneOffset,sceneLength,placedBody.Length);
        if(sceneLength!=scene.Length||!placedBody.AsSpan(sceneOffset,sceneLength).SequenceEqual(scene))
            throw new InvalidDataException("Chest clearance scene is not installed in its actual placed Body.");
        var spawn=new NativeSpawnClearancePoint(I32(scene,0),I32(scene,4),I32(scene,8));
        if(spawn!=new NativeSpawnClearancePoint(floor.SpawnX,floor.SpawnY,floor.SpawnZ)||
            floor.SpawnZ<floor.FloorZ)
            throw new InvalidDataException("Chest clearance requires the actual authored spawn and flat landing interval.");

        // Use the same complete actual Original domain as the Host issuer, not
        // a new file read or a caller's expected executable/hash certificate.
        var exeDomain=source.ResourceInputs["key.special-renderer-and-global-routines"].Single().Extent.Space;
        if(exeDomain.Kind!=NativeAddressKind.ExecutableFile||exeDomain.Revision!=NativeAddressRevision.Original||exeDomain.ByteLength!=0x66000)
            throw new InvalidDataException("Chest clearance requires the actual complete Original executable owner.");
        byte[] original=source.ReadSource(new(exeDomain,0,exeDomain.ByteLength));
        byte[] prototype=host.CopyExecutablePrototype();
        var evidence=NativeFreshInitialSpawnClearanceSource.ValidateExecutable(original).ToList();
        host.RequireExactOutputs(host.CopyOverlay(),host.CopyInitializer(),prototype);
        if(!host.CurrentExecutableAncestor.RevertExact(host.RevertExactExecutable(prototype)).SequenceEqual(original))
            throw new InvalidDataException("Chest clearance Host has a different complete Original executable ancestor.");
        foreach(var consumer in evidence.ToArray())
        {
            RequireExtent(consumer.Offset,consumer.ByteLength,prototype.Length);
            if(!prototype.AsSpan(consumer.Offset,consumer.ByteLength).SequenceEqual(original.AsSpan(consumer.Offset,consumer.ByteLength)))
                throw new InvalidDataException("Actual Host changed a retained native collider/player consumer.");
            Add("actual-host-"+consumer.Id,"host-executable-prototype",consumer.Offset,prototype.AsSpan(consumer.Offset,consumer.ByteLength));
        }
        RequireType1Operands(original);
        Add("source-complete-executable","original-executable",0,original);
        Add("actual-complete-host-executable","host-executable-prototype",0,prototype);
        Add("source-complete-AE","family:chest.model",0,package);

        // Decode the existing common registration grammar. Do not reconstruct
        // a second model importer or assume an unowned byte pattern is a root.
        int modelOffset=I32(placedBody,0x10),modelLength=I32(placedBody,0x14);
        RequireExtent(modelOffset,modelLength,placedBody.Length);
        var registrations=NativeMobyRegistrationTableLayout.Decode(placedBody.AsSpan(0,0x800),modelOffset,modelLength).Registrations;
        var root=registrations.SingleOrDefault(r=>r.ActorId==0xAE)??
            throw new InvalidDataException("Actual placed Body has no registered AE model owner.");
        var contribution=resources.ChestModelContribution??throw new InvalidDataException("Actual resource ancestor has no issued AE contribution.");
        if(resources.ChestModelContributions.Count!=4||!ReferenceEquals(contribution,resources.ChestModelContributions[0])||
            contribution.ActorId!=0xAE||contribution.Id!="chest.model"||contribution.ByteLength!=PackageLength)
            throw new InvalidDataException("Chest clearance lost its actual typed installed AE contribution.");
        int packageAt=checked((int)root.RootDataOffset);RequireExtent(packageAt,PackageLength,placedBody.Length);
        if(packageAt<modelOffset||packageAt+PackageLength>(long)modelOffset+modelLength)
            throw new InvalidDataException("Registered AE package escaped the actual model allocation.");
        ReadOnlySpan<byte> installed=placedBody.AsSpan(packageAt,PackageLength);
        if(!installed.SequenceEqual(contribution.CopyBytes())||
            !installed.Slice(0x14,0x54).SequenceEqual(package.AsSpan(0x14,0x54))||
            !installed[0xEF0..].SequenceEqual(package.AsSpan(0xEF0)))
            throw new InvalidDataException("Installed AE header/frame/collider complement differs from the actual authenticated source.");
        Add("actual-model-registration-header","placed-body",0,placedBody.AsSpan(0,0x800));
        Add("installed-complete-AE","placed-body",packageAt,installed);
        Add("installed-AE-collider-header","placed-body",packageAt+0x14,installed.Slice(0x14,0x54));

        // The native delayed lookahead after the16-byte descriptor belongs to
        // the next installed debris package, not fabricated AE zero padding.
        var next=registrations.SingleOrDefault(r=>r.ActorId==0x135)??
            throw new InvalidDataException("AE lookahead has no actual registered next-package owner.");
        var nextContribution=resources.ChestModelContributions[1];
        int nextAt=checked((int)next.RootDataOffset);
        if(nextContribution.ActorId!=0x135||nextAt!=packageAt+PackageLength)
            throw new InvalidDataException("AE lookahead is not the immediate actual debris contribution.");
        RequireExtent(nextAt,nextContribution.ByteLength,placedBody.Length);
        if(nextAt+nextContribution.ByteLength>(long)modelOffset+modelLength||
            !placedBody.AsSpan(nextAt,nextContribution.ByteLength).SequenceEqual(nextContribution.CopyBytes()))
            throw new InvalidDataException("The independent next-package lookahead owner changed.");
        Add("registered-debris-lookahead-not-AE-padding","placed-body",nextAt,placedBody.AsSpan(nextAt,4));

        var allocation=body.SceneAncestor.Allocation.Rows[3];
        int rowAt=checked((int)allocation.Row.Offset);RequireExtent(rowAt,0x58,scene.Length);
        ReadOnlySpan<byte> row=scene.AsSpan(rowAt,0x58);
        byte[] donor=source.ReadSource(source.PrivateRows[1].Row.Extent);
        if(rowAt!=0x1DC||donor.Length!=0x58||U16(row,0x36)!=0xAE||I32(row,0)!=allocation.Properties.Offset||
            !row.Slice(4,8).SequenceEqual(donor.AsSpan(4,8))||!row[24..].SequenceEqual(donor.AsSpan(24))||
            I32(row,12)!=placement.Intent.ChestX||I32(row,16)!=placement.Intent.ChestY||I32(row,20)!=placement.Intent.ChestZ)
            throw new InvalidDataException("Actual placed AE row lost its source non-coordinate complement or authored anchor.");
        Add("actual-initial-AE-row","placed-scene",rowAt,row);
        Add("actual-authored-player-spawn","placed-scene",0,scene.AsSpan(0,12));

        // Exactly the retained animation0/frame0 selector; type1 does not use
        // the animated-vertex/GTE matrix transform that the Wizard type3 uses.
        if(U32(package,0)!=1||U32(package,0x14)!=0x80000FF8)
            throw new InvalidDataException("Source AE initial animation/collider header changed.");
        for(int i=1;i<8;i++)if(U32(package,0x14+i*4)!=0)
            throw new InvalidDataException("An additional AE collider root is not covered by this assessment.");
        int animation=checked((int)U32(package,0x38));RequireExtent(animation,0x28,PackageLength);
        if(animation<0x3C)throw new InvalidDataException("AE animation header aliases its complete fixed header.");
        uint frame=U32(package,animation+0x24);int selector=checked((int)((frame>>19)&0x1C));
        long frameAt=(long)U32(package,0x34)+(frame&0x1FFFFF);
        if(selector!=0||frameAt!=0xEF0||U32(installed,animation+0x24)!=frame||
            U32(installed,0x14+selector)!=U32(package,0x14+selector))
            throw new InvalidDataException("Actual selected frame/collider no longer belongs to the bounded AE source owner.");
        Add("source-initial-animation-frame-selector","family:chest.model",animation+0x24,package.AsSpan(animation+0x24,4));
        Add("installed-selected-type1-descriptor","placed-body",packageAt+DescriptorOffset,installed.Slice(DescriptorOffset,16));
        uint broadphase=U32(installed,DescriptorOffset),command=U32(installed,DescriptorOffset+4);
        uint vertical=U32(installed,DescriptorOffset+8),flags=U32(installed,DescriptorOffset+12);
        if(broadphase!=0x3A8||command!=0x02580101||vertical!=0x01500000||flags!=4)
            throw new InvalidDataException("The exact source-owned AE type1 descriptor or positional-response semantics changed.");
        // Native SRL16 makes this radius an unsigned positive600; SRA16 of
        // the next word makes the height offset signed336. Incoming flags4
        // feed a distinct accumulator, not a switch that removes solidity.
        int radius=checked((int)(command>>16)),zOffset=unchecked((int)vertical)>>16;
        var centre=new NativeSpawnClearancePoint(I32(row,12),I32(row,16),checked(I32(row,20)+zOffset));
        var reason=NativeInitialSpawnUnknownReason.None;
        if(row.Slice(0x3C,4).ContainsAnyExcept((byte)0))reason=NativeInitialSpawnUnknownReason.UnsupportedPose;
        else if(!NativeDeltaRange(I32(row,12),spawn.X)||!NativeDeltaRange(I32(row,16),spawn.Y)||
            !NativeDeltaRange(I32(row,20),spawn.Z)||!NativeDeltaRange(centre.X,spawn.X)||
            !NativeDeltaRange(centre.Y,spawn.Y)||!NativeDeltaRange(centre.Z,spawn.Z)||
            !NativeDeltaRange(centre.Z,floor.FloorZ)||!NativeDeltaRange(I32(row,20),floor.FloorZ))
            reason=NativeInitialSpawnUnknownReason.UnsupportedTransformRange;
        var status=reason==NativeInitialSpawnUnknownReason.None?
            NativeInitialSpawnClearanceMath.Classify(centre,radius,spawn,PlayerRadius):NativeInitialSpawnClearanceStatus.Unknown;
        string editorId=body.SceneAncestor.RowSpans.Placements.Single(p=>p.FirstRowIndex==2&&p.RowCount==7).EditorId;
        InitialFinding=new(editorId,status,reason,spawn,centre:centre,radius:radius,flags:flags,response:true);
        var horizontalCentre=new NativeSpawnClearancePoint(centre.X,centre.Y,0);
        var horizontalSpawn=new NativeSpawnClearancePoint(spawn.X,spawn.Y,0);
        HorizontalDistanceSquared=NativeInitialSpawnClearanceMath.SquaredDistance(horizontalCentre,horizontalSpawn);
        CombinedRadiusSquared=BigInteger.Pow((BigInteger)radius+PlayerRadius,2);
        LandingColumnStatus=reason==NativeInitialSpawnUnknownReason.None?
            NativeInitialSpawnClearanceMath.Classify(horizontalCentre,radius,horizontalSpawn,PlayerRadius):NativeInitialSpawnClearanceStatus.Unknown;
        ChestMargins=NativeInitialSpawnClearanceMath.InitialFloorMargins(floor,centre);
        PlayerMargins=NativeInitialSpawnClearanceMath.InitialFloorMargins(floor,spawn);
        Evidence=evidence.AsReadOnly();
        PlanSha256=Hash(JsonSerializer.SerializeToUtf8Bytes(new{
            Profile="source-owned-AE-initial-and-static-landing-column-v1",SourceImageSha256,
            Placement=placement.PlanSha256,Body=body.PlanSha256,Host=host.PlanSha256,
            placement.SceneSha256,placement.LevelDataSha256,InitialFinding.EditorId,
            InitialFinding.Status,InitialFinding.UnknownReason,InitialFinding.Spawn,InitialFinding.Centre,
            InitialFinding.ColliderRadius,InitialFinding.PlayerRadius,InitialFinding.IncomingDamageFlags,InitialFinding.ResponsePermitted,
            initialDistanceSquared=InitialFinding.DistanceSquared?.ToString(CultureInfo.InvariantCulture),
            horizontalDistanceSquared=HorizontalDistanceSquared.ToString(CultureInfo.InvariantCulture),
            combinedRadiusSquared=CombinedRadiusSquared.ToString(CultureInfo.InvariantCulture),LandingColumnStatus,
            chestMargins=Margins(ChestMargins),playerMargins=Margins(PlayerMargins),Evidence,
            touchingPolicy="require-strict-separation-and-strict-XY-floor-disks",
            nativePremise="initial-AE-type1-and-default-player-mode1-radius356-not-observed-entry",
            landingPolicy="fixed-authored-XY-column-not-actual-trajectory",floor.FloorZ,floor.SpawnZ,
            SourceCertified,RuntimeVerified,FirstQueryProven,TrajectoryProven,WholeFamilyFootprintClearanceProven,PublicationApproved}));
        RequireAncestry(placement);
        void Add(string id,string domain,int at,ReadOnlySpan<byte> bytes)=>evidence.Add(new(id,domain,at,bytes.Length,Hash(bytes)));
    }

    internal NativeFreshLockedChestPlacementPlan PlacementAncestor{get;}
    internal NativeFreshLockedChestResearchBody BodyAncestor=>PlacementAncestor.BodyAncestor;
    internal NativeFreshLockedChestResearchHostExtension HostAncestor=>BodyAncestor.HostAncestor;
    internal NativeInitialSpawnClearanceFinding InitialFinding{get;}
    internal NativeInitialSpawnClearanceStatus LandingColumnStatus{get;}
    internal BigInteger HorizontalDistanceSquared{get;}
    internal BigInteger CombinedRadiusSquared{get;}
    internal NativeInitialFloorMargins ChestMargins{get;}
    internal NativeInitialFloorMargins PlayerMargins{get;}
    internal IReadOnlyList<NativeInitialSpawnClearanceEvidence> Evidence{get;}
    internal string SourceImageSha256=>NativeMobySourceInventory.OriginalImageSha256;
    internal string PlanSha256{get;}
    internal bool SourceCertified=>true;
    internal bool RuntimeVerified=>false;
    internal bool FirstQueryProven=>false;
    internal bool TrajectoryProven=>false;
    internal bool WholeFamilyFootprintClearanceProven=>false;
    internal bool PublicationApproved=>false;
    internal void RequireAncestry(NativeFreshLockedChestPlacementPlan placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if(!ReferenceEquals(placement,PlacementAncestor))
            throw new InvalidDataException("Chest clearance belongs to another actual placement, even when bytes and hashes agree.");
        placement.RequireAncestry(BodyAncestor,placement.Intent);
        placement.RequireExactLevelData(placement.CopyLevelData());
    }
    internal void RequireStaticLandingClearance(NativeFreshLockedChestPlacementPlan placement)
    {
        RequireAncestry(placement);
        NativeInitialSpawnClearanceMath.RequireInitialSeparation(InitialFinding);
        if(InitialFinding.UnknownReason!=NativeInitialSpawnUnknownReason.None||InitialFinding.ResponsePermitted!=true||
            InitialFinding.ColliderRadius!=ChestRadius||InitialFinding.PlayerRadius!=PlayerRadius||
            LandingColumnStatus!=NativeInitialSpawnClearanceStatus.InitialSeparation||
            HorizontalDistanceSquared<=CombinedRadiusSquared||
            !ChestMargins.StrictlyContains(ChestRadius)||!PlayerMargins.StrictlyContains(PlayerRadius))
            throw new InvalidDataException("Chest static landing clearance requires known strictly separated initial/column spheres and both strict floor disks; it is not route or gameplay approval.");
    }
    internal static NativeFreshLockedChestSpawnClearancePlan Compose(NativeFreshLockedChestPlacementPlan placement)
    {ArgumentNullException.ThrowIfNull(placement);return new(placement);}

    private static void RequireType1Operands(ReadOnlySpan<byte> executable)
    {
        // The common five executable owners are validated above. These extra
        // literal operands distinguish AE's type1 sphere from Wizard type3/GTE.
        foreach(var p in new (uint Pc,uint Word)[]{
            (0x80040904,0x24050164),(0x80040764,0x24140001),
            (0x8004E548,0x8FC10008),(0x8004E554,0x1020FFEF),(0x8004E558,0x94220000),
            (0x8004E560,0x00561020),(0x8004E590,0x30420001),(0x8004E598,0x8C220000),
            (0x8004E5A4,0x304AFF00),(0x8004E5A8,0x000A5142),(0x8004E5C0,0x0801399A),
            (0x8004E668,0x00025402),(0x8004E66C,0x01565020),(0x8004E674,0x8C2B0000),
            (0x8004E67C,0x000B5C03),(0x8004E680,0x01655820),(0x8004E684,0x016B0018),
            (0x8004E68C,0x01866020),(0x8004E690,0x01876020),(0x8004E694,0x018A6822),
            (0x8004E698,0x05A1FFBD),(0x8004E69C,0x20210008),(0x8004E6CC,0x8C30FFFC),
            (0x8004E6D4,0x01F07825),(0x8004E6D8,0xAE8F0000),(0x8004E6DC,0x304F0002),
            (0x8004E6E0,0x15E0FFAB),(0x8004E6E8,0x326F0001),(0x8004E6EC,0x11E00005),
            (0x8004E6FC,0x08013A76),(0x8004E700,0x21650000)})
            if(U32(executable,checked((int)(p.Pc-0x8000F800)))!=p.Word)
                throw new InvalidDataException("The source native type1/default-player query or positional-response operand changed.");
    }
    private static bool NativeDeltaRange(int a,int b)=>(long)a-b is >=short.MinValue and <=short.MaxValue;
    private static string[] Margins(NativeInitialFloorMargins m)=>new[]{m.Left,m.Right,m.Top,m.Bottom}
        .Select(v=>v.ToString(CultureInfo.InvariantCulture)).ToArray();
    private static void RequireExtent(int at,int length,int total)
    {if(at<0||length<=0||at>total||length>total-at)throw new InvalidDataException("Chest clearance owner escaped its complete bounded domain.");}
    private static uint U32(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static int I32(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]);
    private static ushort U16(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(bytes[at..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>NativeFreshInitialSpawnClearanceSource.Hash(bytes);
}

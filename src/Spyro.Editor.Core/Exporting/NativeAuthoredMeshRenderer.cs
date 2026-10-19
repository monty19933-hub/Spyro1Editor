using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Native LP/HP batches. The per-frame subdivision queues are proved for the whole level when every
/// textured face and its sector fit at once, and otherwise checked by simulating the game's own tests over camera
/// poses. Collision remains exact.</summary>
internal static class NativeAuthoredMeshRenderer
{
    /// <summary>A drawn face. <paramref name="Flags"/> is the face's subdivision byte: 0x40 keeps it out of the
    /// subdivision queues except within a few units of the camera (it then takes the normal 4-tile subdivision, not
    /// the 16-tile close one); otherwise it is drawn whole, textured. The face pass reads it at 0x80026D30/0x80026D74.</summary>
    private sealed record Face(Vector3f[] Points,ColorRgba[] Near,ColorRgba[] Far,int Material,int Group=-1,int Flags=0,ColorRgba[]? Distant=null)
    {
        /// <summary>The colours of the face's untextured far-tier copy (<see cref="AuthoredSurface.DistantColors"/>).</summary>
        internal ColorRgba[] Lp=>Distant??Far;
        /// <summary>Per corner: its near colour (the second near-tier table), far colour (the first) and distant colour (the
        /// far tier), which share one colour index.</summary>
        internal IEnumerable<(ColorRgba Near,ColorRgba Far,ColorRgba Distant)> Colours=>Near.Zip(Far,Lp);
    }
    /// <summary>Imported levels were made for a game with deeper subdivision queues; their faces subdivide only up close.</summary>
    internal const int ImportedFaceFlags=0x40;
    /// <summary>The face's back is drawn too: the HP face pass and every subdivision pass skip their NCLIP rejection
    /// when bit 2 is set (USA 0x80026C90, 0x800271F8, 0x80027E04, 0x800284E8, 0x80029304, 0x80029F3C); bit 1 would
    /// instead flip which side is the front.</summary>
    internal const int TwoSidedFaceFlag=0x04;
    /// <summary>The face's front is its other side: bit 1 negates the first NCLIP of the HP quad pass (USA 0x80026C60),
    /// the triangle pass (0x80027210) and every subdivision pass (0x80027E10, 0x800284F4, 0x80029310, 0x80029F48),
    /// so the face is drawn from the side its corners turn clockwise from, with its texture as authored.</summary>
    internal const int FrontReversedFaceFlag=0x02;
    /// <summary>LP (far) face word 0, low byte: bit 7 skips the far pass's second NCLIP rejection (USA 0x80026430..50),
    /// so the face is drawn from both sides; bits 0-4 push it back in the depth order. The far pass has no front flip:
    /// a reversed face is written with its corners reversed (LP faces are untextured).</summary>
    internal const uint FarTwoSidedFlag=0x80;
    /// <summary>A sector's far (LP) mesh when it differs from the near one: LP indices are 6 bits, so a sector with
    /// more than 64 near vertices (as retail and imported sectors have) draws a simplified copy far away, or the
    /// sector's own distant mesh when the scene gives one (<see cref="SceneDocument.FarMeshes"/>).
    /// <paramref name="FaceColors"/>: per face, its corners' colour indices (null: the vertex indices);
    /// <paramref name="FaceFlags"/>: per face, the low byte of LP word 0 (null: 0).</summary>
    private sealed record FarMesh(List<Vector3f> Vertices,List<ColorRgba> Colors,List<int[]> Faces,List<int[]>? FaceColors=null,List<uint>? FaceFlags=null,bool Supplied=false);
    private sealed class Batch
    {
        internal readonly List<Face> Faces=[];
        internal readonly List<Vector3f> Vertices=[];
        internal readonly List<(ColorRgba Near,ColorRgba Far,ColorRgba Distant)> Colors=[];
        internal FarMesh? Far;
        /// <summary>The scene sector an imported batch draws (a sector too big for one batch is split).</summary>
        internal int Group=-1;
        /// <summary>An imported sector: near tables up to 255 entries, with a far mesh of its own when needed.</summary>
        internal bool Imported;
        internal bool Fits(Face f)
        {
            var v=Vertices.Concat(f.Points).Distinct().ToArray();
            int colors=Colors.Concat(f.Colours).Distinct().Count();
            return v.Length<=64&&v.Length<=MaxNearVertices&&colors<=64&&v.Length+colors+2*(Faces.Count+1)<=255&&
                v.Max(p=>p.X)-v.Min(p=>p.X)<=2047&&v.Max(p=>p.Y)-v.Min(p=>p.Y)<=2047&&v.Max(p=>p.Z)-v.Min(p=>p.Z)<=1023&&
                v.Max(p=>p.X)-v.Min(p=>p.X)+v.Max(p=>p.Y)-v.Min(p=>p.Y)+v.Max(p=>p.Z)-v.Min(p=>p.Z)<=8191;
        }
        internal void Add(Face f)
        {
            if(!Imported&&!Fits(f))throw new InvalidDataException("Authored native sector exceeds LP/HP vertex, color, offset, or coordinate capacity.");
            Faces.Add(f);
            foreach(var p in f.Points)if(!Vertices.Contains(p))Vertices.Add(p);
            foreach(var c in f.Colours)if(!Colors.Contains(c))Colors.Add(c);
        }
    }
    internal static (byte[] Environment,byte[] Visibility) Compile(SceneDocument doc,IReadOnlyList<AuthoredMaterial> materials,IReadOnlyList<NativeAuthoredMesh.Triangle> direct)
    {
        List<Face> faces=[];
        foreach(var s in doc.Surfaces.Where(s=>s.Material!=null))
        {
            int material=Enumerable.Range(0,materials.Count).Single(i=>materials[i]==s.Material);
            if(material>127)throw new InvalidDataException("Authored materials exceed the native 7-bit textured descriptor index.");
            var near=s.NearColors??Enumerable.Repeat(s.Color,s.Vertices.Length).ToArray();
            var far=s.FarColors??Enumerable.Repeat(s.Color,s.Vertices.Length).ToArray();
            foreach(var p in s.Vertices){_=Unit(p.X);_=Unit(p.Y);_=Unit(p.Z);}
            AddFaces(faces,s,near,far,material);
        }
        foreach(var t in direct)faces.Add(new([t.A,t.B,t.C],t.Near,t.Far,255,-1,(t.TwoSided?TwoSidedFaceFlag:0)|(t.FrontReversed?FrontReversedFaceFlag:0)));
        // Small levels keep the original packing (spatial buckets filled in authoring order, the same bytes as
        // before): when every textured face and one header per textured sector fit a subdivision queue at once, no
        // camera can overflow it. Larger levels get compact sectors like retail ones (a few hundred units across)
        // with their true radius, because the game culls whole sectors against the view and a sprawling sector keeps
        // far-off faces in the per-frame queues: faces are split at the median along the longest axis until each
        // part fits one sector and is at most SectorSpan across (the span grows when that needs more sectors than
        // the game holds), and the queues are then checked by simulation. Membership is deterministic.
        // Imported faces keep the sectors of the level they came from (a sector too big for one native sector is
        // split at its median); the editor's own faces are grouped as before.
        List<Batch> batches;
        bool proved;
        if(faces.Any(f=>f.Group>=0))
        {
            batches=[];
            foreach(var group in faces.Where(f=>f.Group>=0).GroupBy(f=>f.Group))
            {
                int first=batches.Count;SplitImported([..group],batches);
                for(int k=first;k<batches.Count;k++)batches[k].Group=group.Key;
            }
            var own=faces.Where(f=>f.Group<0).ToList();
            if(own.Count>0)
            {
                List<Batch> rest=[];
                for(float span=SectorSpan;;span*=1.25f)
                {
                    rest=[];Split(own,span,rest);
                    if(batches.Count+rest.Count<=MaxSectors||span>16384)break;
                }
                batches.AddRange(rest);
            }
            var supplied=doc.FarMeshes?.ToDictionary(m=>m.Sector);
            foreach(var batch in batches.Where(b=>b.Imported))
                batch.Far=NeedsFarMesh(batch)?Supplied(batch,batches,supplied)??Simplify(batch)
                    :supplied!=null&&supplied.TryGetValue(batch.Group,out var always)&&always.Always?Supplied(batch,batches,supplied):null;
            LastFarReport=(batches.Count(b=>b.Far is {Supplied:true}),batches.Count(b=>b.Far is {Supplied:false}));
            proved=false;
        }
        else
        {
            batches=Bucketed(faces);LastFarReport=(0,0);
            proved=batches.Count<=MaxSectors&&TexturedLoad(batches)<=SubdivisionQueueCapacity;
        }
        if(!proved&&!faces.Any(f=>f.Group>=0))
            for(float span=SectorSpan;;span*=1.25f)
            {
                batches=[];Split(faces,span,batches);
                if(batches.Count<=MaxSectors||span>16384)break;
            }
        if(batches.Count>(SectorLimitOverride??MaxSectors))
            throw new InvalidDataException($"Authored terrain needs {batches.Count} sectors after batching" +
                (batches.Any(b=>b.Imported)?$" ({batches.Count(b=>b.Imported)} for {faces.Where(f=>f.Group>=0).Select(f=>f.Group).Distinct().Count()} imported sectors)":"") +
                $"; the game's sector queues hold {MaxSectors}. Reduce geometry.");
        int totalFaces=batches.Sum(b=>b.Faces.Count);
        if(totalFaces>MaxFaces)
            throw new InvalidDataException($"Authored terrain has {totalFaces} drawn faces; the game's per-face table holds {MaxFaces}. Reduce geometry.");
        RequireSubdivisionQueues(batches,NativeSubdivisionQueueGuard.Needed(doc));
        _lastDrawPlan=PlanDraw(doc,batches,proved,doc.FarMeshes?.ToDictionary(m=>m.Sector));
        byte[] env=Environment(batches,proved);
        int count=batches.Count;
        int list=(count+4)&~3,sky=16+list;byte[] visibility=new byte[sky+12];W(visibility,0,(uint)visibility.Length);W(visibility,4,(uint)(sky-4));W(visibility,8,1);W(visibility,12,12);
        visibility.AsSpan(16,list).Fill(255);for(int i=0;i<count;i++)visibility[16+i]=(byte)i;
        W(visibility,sky,1);W(visibility,sky+4,8);visibility.AsSpan(sky+8,4).Fill(255);
        return(env,visibility);
    }
    private static byte[] Environment(List<Batch> batches,bool proved)
    {
        var sectors=new List<byte[]>();int cache=1;
        foreach(var batch in batches){sectors.Add(Encode(batch,cache,!proved));cache+=batch.Faces.Count;}
        int count=sectors.Count,total=8+count*4+sectors.Sum(s=>s.Length);byte[] env=new byte[total];W(env,0,(uint)total);W(env,4,(uint)count);
        int cursor=8+count*4;
        for(int i=0;i<count;i++){W(env,8+i*4,(uint)(cursor-4));sectors[i].CopyTo(env,cursor);cursor+=sectors[i].Length;}
        return env;
    }

    /// <summary>How a compiled level's terrain loads the frame's draw buffer (<see cref="NativeDrawLoad"/>): the near
    /// distance the draw-buffer guard gives the level (2,048 as retail unless the terrain needs less), the most far-mesh
    /// faces a sector kept (null: as many as its tables hold), and the heaviest view the survey found.</summary>
    public sealed record DrawPlan(int NearDistance,int? FarFaceCap,NativeDrawLoad.Load Worst,System.Numerics.Vector3 WorstSpot,
        System.Numerics.Vector3 WorstEye,int Spots,int Cameras)
    {
        public bool OverBudget=>Worst.Whole>NativeDrawLoad.TerrainBudget;
        /// <summary>A readable line for reports.</summary>
        public string Describe()=>$"heaviest view {Worst.Whole:N0} bytes of terrain drawn whole ({Worst.Far:N0} distant, {Worst.Near:N0} near; {Worst.Subdivided:N0} more subdivided up close) " +
            $"from ({WorstSpot.X:0}, {WorstSpot.Y:0}, {WorstSpot.Z:0}); budget {NativeDrawLoad.TerrainBudget:N0}" +
            (NearDistance<NativeDrawLoad.NearDistance?$"; textured terrain drawn to {NearDistance:N0} units (retail 2,048)":"") +
            (FarFaceCap is int cap?$"; distant meshes at most {cap} faces a sector":"") + $" ({Spots:N0} spots, {Cameras:N0} views)";
        /// <summary>The plan in everyday words for an import's summary (null: nothing changed and nothing to say).</summary>
        public string? Note()
        {
            List<string> parts=[];
            if(FarFaceCap!=null)parts.Add("distant scenery is drawn simpler");
            if(NearDistance<NativeDrawLoad.NearDistance)parts.Add($"textures show to {NearDistance:N0} units instead of 2,048");
            string? changed=parts.Count==0?null:"So the game can draw its busiest views, "+string.Join(" and ",parts)+".";
            if(!OverBudget)return changed;
            return (changed==null?"":changed+" ")+$"Its busiest view (from {WorstSpot.X:0}, {WorstSpot.Y:0}, {WorstSpot.Z:0}) is still heavy: with many objects in view, the farthest terrain may flicker out there.";
        }
    }
    [ThreadStatic] private static DrawPlan? _lastDrawPlan;
    /// <summary>The draw plan of this thread's last <see cref="Compile"/>.</summary>
    internal static DrawPlan? LastDrawPlan=>_lastDrawPlan;
    /// <summary>Lab only: skip the draw-load survey (null plan).</summary>
    internal static bool DrawPlanDisabled;
    internal static readonly int[] FarFaceCaps=[48,32,24,16,12,8], NearDistances=[1792,1536,1280,1024];
    /// <summary>Distant meshes are coarsened only while they draw more than this in the heaviest view.</summary>
    internal const int FarFloor=0x4000;
    internal const string DrawLoadRefusal="Too much terrain in view";

    /// <summary>Surveys Spyro's views over the level (<see cref="NativeDrawLoad.Spots"/>) and, while the heaviest one draws
    /// more terrain (drawn whole) than <see cref="NativeDrawLoad.TerrainBudget"/>: coarsens the sectors' distant meshes (fewer faces
    /// each, refitted from the scene's own distant meshes or simplified from the near faces) while they draw more than
    /// <see cref="FarFloor"/>, then shortens the near distance the guard gives the level (the passes draw textured
    /// faces only that close, and distant meshes from 256 units nearer). Each step is checked on the heaviest views found
    /// so far and confirmed on all. A level still over the budget keeps its last step (the guard then trims the terrain
    /// in its heaviest views); one over <see cref="NativeDrawLoad.TerrainLimit"/> even so is refused.</summary>
    private static DrawPlan? PlanDraw(SceneDocument doc,List<Batch> batches,bool proved,Dictionary<int,AuthoredFarMesh>? supplied)
    {
        if(DrawPlanDisabled||batches.Count==0)return null;
        var spots=NativeDrawLoad.Spots(doc);
        var cameras=NativeDrawLoad.Cameras(spots).ToList();
        var original=batches.Select(b=>b.Far).ToArray();
        int near=(int)NativeDrawLoad.NearDistance;int? cap=null;
        var all=NativeDrawLoad.Survey(NativeDrawLoad.Parse(Environment(batches,proved)),cameras,near);
        var hot=all.Take(96).Select(v=>(v.Pose,v.Spot)).ToList();
        var worst=all[0];bool confirmed=true;
        DrawPlan Plan()=>new(near,cap,worst.Load,worst.Spot,worst.Pose.Eye,spots.Count,cameras.Count);
        while(worst.Load.Whole>NativeDrawLoad.TerrainBudget)
        {
            int nextCap=FarFaceCaps.FirstOrDefault(c=>c<(cap??int.MaxValue));
            if(worst.Load.Far>FarFloor&&nextCap>0)
            {
                cap=nextCap;
                for(int i=0;i<batches.Count;i++)batches[i].Far=CappedFar(batches[i],original[i],batches,supplied,nextCap);
            }
            else if(NearDistances.FirstOrDefault(n=>n<near) is int nextNear&&nextNear>0)near=nextNear;
            else break;
            var sectors=NativeDrawLoad.Parse(Environment(batches,proved));
            var trial=NativeDrawLoad.Survey(sectors,hot,near);
            if(trial[0].Load.Whole>NativeDrawLoad.TerrainBudget){worst=trial[0];confirmed=false;continue;}
            all=NativeDrawLoad.Survey(sectors,cameras,near);worst=all[0];confirmed=true;
            foreach(var v in all.Take(32))if(!hot.Contains((v.Pose,v.Spot)))hot.Add((v.Pose,v.Spot));
        }
        if(!confirmed)worst=NativeDrawLoad.Survey(NativeDrawLoad.Parse(Environment(batches,proved)),cameras,near)[0];
        LastFarReport=(batches.Count(b=>b.Far is {Supplied:true}),batches.Count(b=>b.Far is {Supplied:false}));
        var plan=Plan();
        if(worst.Load.Whole>NativeDrawLoad.TerrainLimit)
            throw new InvalidDataException($"{DrawLoadRefusal} near ({worst.Spot.X:0}, {worst.Spot.Y:0}, {worst.Spot.Z:0}): the game would draw {worst.Load.Whole:N0} bytes of it " +
                $"({worst.Load.Far:N0} distant, {worst.Load.Near:N0} near) even with textured terrain only {near:N0} units out and " + (cap is int most?$"distant meshes of {most} faces; ":"its distant meshes; ") +
                $"a frame holds {NativeDrawLoad.FrameBufferBytes:N0} for everything. Remove geometry seen from there (backdrop, small details) or split the level.");
        return plan;
    }

    /// <summary>A sector's distant mesh with at most <paramref name="cap"/> faces (unchanged when it has no more).</summary>
    private static FarMesh? CappedFar(Batch batch,FarMesh? original,List<Batch> batches,Dictionary<int,AuthoredFarMesh>? supplied,int cap)
    {
        int faces=original?.Faces.Count??batch.Faces.Count;
        if(faces<=cap)return original;
        if(original is {Supplied:true}&&Supplied(batch,batches,supplied,cap) is {} refit)return refit;
        return Simplify(batch,cap);
    }

    /// <summary>A textured surface as drawn faces. Imported quads are rarely flat; they stay whole (as the game they came
    /// from draws them) when convex, so their texture spans the quad instead of being squeezed into each half.</summary>
    private static void AddFaces(List<Face> faces,AuthoredSurface s,ColorRgba[] near,ColorRgba[] far,int material)
    {
        int group=s.Sector??-1;
        int flags=group>=0?(ImportedFlagsOverride?.Invoke(s)??ImportedFaceFlags):0;
        if(s.TwoSided)flags|=TwoSidedFaceFlag;
        if(s.FrontReversed)flags|=FrontReversedFaceFlag;
        var distant=s.DistantColors??far;
        if(s.Vertices.Length==4&&(group>=0?ConvexQuad(s.Vertices):CompatibleQuad(s.Vertices)))faces.Add(new(s.Vertices,near,far,material,group,flags,distant));
        else
        {
            faces.Add(new(s.Vertices[..3],near[..3],far[..3],material,group,flags,distant[..3]));
            if(s.Vertices.Length==4)faces.Add(new([s.Vertices[0],s.Vertices[2],s.Vertices[3]],[near[0],near[2],near[3]],[far[0],far[2],far[3]],material,group,flags,[distant[0],distant[2],distant[3]]));
        }
    }
    /// <summary>How many native sectors the textured faces of one imported sector need: the drawn faces <see cref="Compile"/>
    /// makes of them, split as it splits an imported sector whose near tables or packed offsets overflow.</summary>
    internal static int ImportedSectorCount(IEnumerable<AuthoredSurface> surfaces)
    {
        List<Face> faces=[];
        foreach(var s in surfaces.Where(s=>s.Material!=null))
            AddFaces(faces,s with{Sector=s.Sector??0},s.NearColors??Enumerable.Repeat(s.Color,s.Vertices.Length).ToArray(),s.FarColors??Enumerable.Repeat(s.Color,s.Vertices.Length).ToArray(),0);
        if(faces.Count==0)return 0;
        List<Batch> output=[];SplitImported(faces,output);return output.Count;
    }
    // Per-frame native queues (USA sector pass 0x80025A30.., face pass 0x80026C80.. quads, 0x80027244.. triangles):
    // - a sector is near when its header sphere passes the view-frustum test (culled when d+r <= 0, when
    //   4(|x|-0.78r) >= 3(d+0.59r) or when 32(|y|-0.875r) >= 17(d+0.44r); d depth, x/y across, in units) and d-r is
    //   under the near-detail distance (2048 units); with at most 127 sectors neither sector list can overflow;
    // - a front-facing textured face of a near sector (the GTE NCLIP test runs first) whose SZ sum (quad: four
    //   corners; triangle: a+b+2c; 4 SZ per unit) is above 0 and under 0x2000 is queued for subdivision, with one
    //   header per sector and queue: the close queue (+0x1E00, 127 entries before the normal queue) when a corner's
    //   SZ is under 0x140, otherwise the normal queue (+0x2000); untextured faces carry the no-subdivision flag;
    // - one cache byte per face from 0x8006FCF4 + sector cache index, below the near list at +0x1C00.
    // The queues are checked by running those tests for camera poses over the whole level volume: a 128-unit grid
    // from below the lowest face to 768 above the highest, 24 headings and pitches from 60 degrees down to 30 up.
    // Faces with a corner near or behind the camera count whichever way they face, triangles take the smaller
    // weighting, and the two queues together must keep 16 of one queue's entries spare (a margin for poses
    // between grid points). Measured in the emulator (CrashLevelSmoke's village, 128 poses), the real queues
    // never exceeded this model's total.
    // A queue holds 127 entries before its terminator; when every textured face of the level and one header per
    // textured sector fit that, no camera can overflow it (exact, no margin needed).
    // One cache byte per face in a 0x1C00-byte table, index 0 unused: 7,167 faces (retail Dark Passage uses them all).
    internal const int MaxSectors=127, MaxFaces=7167, SubdivisionQueueCapacity=127, SubdivisionQueueEntries=126, SubdivisionMargin=16;
    /// <summary>A sector's near (HP) vertices are transformed into the 1 KB scratchpad (USA 0x8002683C..0x800269B4: screen
    /// x/y 4 bytes each from 0x1F800000, depth 2 bytes each from 0x1F8002A8), so a sector drawn up close holds at most
    /// 170; more run past the scratchpad (bus error at 0x8002699C, seen with an imported Crash 3 sector of 200+).</summary>
    internal const int MaxNearVertices=170;
    private const float NearDistance=2048, PoseSpacing=128;

    /// <summary>The last compile's sector count and heaviest simulated subdivision-queue load (for smoke tests).</summary>
    internal static (int Sectors,int QueueLoad,string Where) LastReport{get;private set;}
    /// <summary>The last compile's far meshes: sectors drawing their scene-given distant mesh, and simplified copies.</summary>
    internal static (int Supplied,int Simplified) LastFarReport{get;private set;}
    internal static IReadOnlyList<QueueSector> LastQueueSectors{get;private set;}=[];
    /// <summary>The last compile's heaviest load per grid column (x, y from the grid origin in PoseSpacing steps).</summary>
    internal static (float X,float Y,int[,] Load) LastLoadMap{get;private set;}
    internal sealed record QueueSector(float X,float Y,float Z,float R,float[] Corners,int[] Counts,float[] Normals,int[]? Flags=null);
    private static int TexturedLoad(List<Batch> batches)=>
        batches.Sum(b=>b.Faces.Count(f=>f.Material!=255))+batches.Count(b=>b.Faces.Any(f=>f.Material!=255));
    private static List<Batch> Bucketed(List<Face> faces)
    {
        // Spatial buckets prevent a long authored row from producing a chain of mutually incompatible sector origins.
        float ox=faces.Min(f=>f.Points.Min(p=>p.X)),oy=faces.Min(f=>f.Points.Min(p=>p.Y)),oz=faces.Min(f=>f.Points.Min(p=>p.Z));
        List<Batch> batches=[];
        foreach(var bucket in faces.GroupBy(f=>((int)((f.Points.Min(p=>p.X)-ox)/1536),(int)((f.Points.Min(p=>p.Y)-oy)/1536),(int)((f.Points.Min(p=>p.Z)-oz)/768))))
        {
            Batch? batch=null;
            foreach(var f in bucket)
            {
                if(batch==null||!batch.Fits(f)){batch=new();batches.Add(batch);}
                batch.Add(f);
            }
        }
        return batches;
    }
    /// <summary>Lab only: imported sector span and sector limit overrides (experiments, never exported).</summary>
    internal static int? ImportedSpanOverride, SectorLimitOverride;
    internal static Func<AuthoredSurface,int>? ImportedFlagsOverride;
    /// <summary>Lab only: limits the simulated camera poses (null: the whole level volume).</summary>
    internal static Func<float,float,float,bool>? PoseFilter;
    /// <summary>Lab only: measure, but do not refuse (experimental images for real-queue measurements).</summary>
    internal static bool QueueCheckDisabled;
    private static void RequireSubdivisionQueues(List<Batch> batches,bool guarded)
    {
        if(QueueCheckDisabled){LastReport=(batches.Count,-1,"not checked");return;}
        // Imported levels were made for deeper queues; the export installs NativeSubdivisionQueueGuard for them, so a
        // full queue draws the rest whole instead of overrunning (measured: Lost Fleet fills at most 87 of 127).
        if(batches.Any(b=>b.Imported)){LastReport=(batches.Count,-1,"guarded");return;}
        int load=TexturedLoad(batches);
        LastReport=(batches.Count,load,"everywhere");LastQueueSectors=[];LastLoadMap=default;
        if(load<=SubdivisionQueueCapacity)return;
        var sectors=batches.Where(b=>b.Faces.Any(f=>f.Material!=255)).Select(b=>
        {
            int x=(int)b.Vertices.Min(p=>p.X),y=(int)b.Vertices.Min(p=>p.Y),z=(int)b.Vertices.Min(p=>p.Z),mx=(int)b.Vertices.Max(p=>p.X),my=(int)b.Vertices.Max(p=>p.Y),mz=(int)b.Vertices.Max(p=>p.Z);
            var faces=b.Faces.Where(f=>f.Material!=255).ToArray();
            return new QueueSector((x+mx)/2,(y+my)/2,(z+mz)/2,Radius(b.Vertices,(x+mx)/2,(y+my)/2,(z+mz)/2),
                faces.SelectMany(f=>f.Points.SelectMany(p=>new[]{p.X,p.Y,p.Z})).ToArray(),faces.Select(f=>f.Points.Length).ToArray(),
                faces.SelectMany(f=>{var a=f.Points[0];var u=f.Points[1];var v=f.Points[2];
                    float ux=u.X-a.X,uy=u.Y-a.Y,uz=u.Z-a.Z,vx=v.X-a.X,vy=v.Y-a.Y,vz=v.Z-a.Z;
                    return new[]{uy*vz-uz*vy,uz*vx-ux*vz,ux*vy-uy*vx};}).ToArray(),faces.Select(f=>f.Flags).ToArray());
        }).ToArray();
        LastQueueSectors=sectors;
        float x0=sectors.Min(s=>s.X-s.R),y0=sectors.Min(s=>s.Y-s.R),x1=sectors.Max(s=>s.X+s.R),y1=sectors.Max(s=>s.Y+s.R);
        float z0=batches.Min(b=>b.Vertices.Min(p=>p.Z))-128,z1=batches.Max(b=>b.Vertices.Max(p=>p.Z))+768;
        const int Headings=24;float[] pitches=[-60,-45,-30,-18,-8,0,10,30];
        var bases=new List<(float[] F,float[] R,float[] U)>();
        for(int h=0;h<Headings;h++)foreach(float pitch in pitches)
        {
            double a=h*2*Math.PI/Headings,b=pitch*Math.PI/180;
            float[] f=[(float)(Math.Cos(b)*Math.Cos(a)),(float)(Math.Cos(b)*Math.Sin(a)),(float)Math.Sin(b)];
            float[] r=[(float)Math.Sin(a),(float)-Math.Cos(a),0];
            float[] u=[r[1]*f[2]-r[2]*f[1],r[2]*f[0]-r[0]*f[2],r[0]*f[1]-r[1]*f[0]];
            bases.Add((f,r,u));
        }
        int nx=(int)((x1-x0)/PoseSpacing)+1,ny=(int)((y1-y0)/PoseSpacing)+1,nz=(int)((z1-z0)/PoseSpacing)+1;
        int worst=0;string? where=null;object gate=new();var map=new int[nx,ny];
        Parallel.For(0,nx,i=>
        {
            float px=x0+i*PoseSpacing;var reach=new List<QueueSector>();
            for(int j=0;j<ny;j++)for(int k=0;k<nz;k++)
            {
                float py=y0+j*PoseSpacing,pz=z0+k*PoseSpacing;
                if(PoseFilter!=null&&!PoseFilter(px,py,pz))continue;
                // A sector can only be queued from here if it can be both in the frustum and under 512 deep.
                reach.Clear();
                foreach(var s in sectors){float dx=s.X-px,dy=s.Y-py,dz=s.Z-pz,lim=2.3f*(512+s.R)+2.4f*s.R;if(dx*dx+dy*dy+dz*dz<lim*lim)reach.Add(s);}
                if(reach.Count==0)continue;
                foreach(var (f,r,u) in bases)
                {
                    int normal=0,close=0;
                    foreach(var s in reach)
                    {
                        float dx=s.X-px,dy=s.Y-py,dz=s.Z-pz,d=dx*f[0]+dy*f[1]+dz*f[2],x=Math.Abs(dx*r[0]+dy*r[1]+dz*r[2]),y=Math.Abs(dx*u[0]+dy*u[1]+dz*u[2]),R=s.R;
                        if(d+R<=0||4*(x-0.78125f*R)>=3*(d+0.59375f*R)||32*(y-0.875f*R)>=17*(d+0.4375f*R)||d-R>=NearDistance||d-R>=512)continue;
                        int sn=0,sc=0,at=0;
                        for(int n=0;n<s.Counts.Length;n++)
                        {
                            int corners=s.Counts[n];float sum=0,min=float.MaxValue;bool near=false;
                            for(int c=0;c<corners;c++)
                            {
                                int o=(at+c)*3;float sz=Math.Max(0,4*((s.Corners[o]-px)*f[0]+(s.Corners[o+1]-py)*f[1]+(s.Corners[o+2]-pz)*f[2]));
                                sum+=sz;min=Math.Min(min,sz);if(sz<0x140)near=true;
                            }
                            if(corners==3)sum+=min;
                            int a3=at*3;at+=corners;
                            if(sum<=0||sum>=0x2000)continue;
                            int flags=s.Flags?[n]??0;
                            if((flags&0x80)!=0||!near&&(flags&0x40)!=0)continue;
                            float facing=(px-s.Corners[a3])*s.Normals[n*3]+(py-s.Corners[a3+1])*s.Normals[n*3+1]+(pz-s.Corners[a3+2])*s.Normals[n*3+2];
                            if(!near&&(flags&TwoSidedFaceFlag)==0&&((flags&FrontReversedFaceFlag)!=0?facing>=0:facing<=0))continue;
                            if(near&&(flags&0x40)==0)sc++;else sn++;
                        }
                        if(sn>0)normal+=sn+1;if(sc>0)close+=sc+1;
                    }
                    int load=normal+close;
                    if(load>map[i,j])map[i,j]=load;
                    if(load>worst)lock(gate)if(load>worst){worst=load;where=$"({px:0}, {py:0}, {pz:0})";}
                }
            }
        });
        LastReport=(batches.Count,worst,where??"nowhere");LastLoadMap=(x0,y0,map);
        // The simulation is a bound (measured queues in a built village were 4-10x below it): with the guard installed
        // a camera that does fill a queue draws its remaining faces whole, so only an unguarded level is refused.
        if(worst>SubdivisionQueueEntries-SubdivisionMargin&&!guarded)
            throw new InvalidDataException($"Too many textured tiles close together near {where}: a camera there can need {worst} of the game's {SubdivisionQueueEntries} close-up texture slots (keep {SubdivisionMargin} spare). Use larger tiles there, or untextured faces.");
    }

    internal const float SectorSpan=640;
    private static void Split(List<Face> faces,float span,List<Batch> output)
    {
        var points=faces.SelectMany(f=>f.Points).ToArray();
        float[] extent=[points.Max(p=>p.X)-points.Min(p=>p.X),points.Max(p=>p.Y)-points.Min(p=>p.Y),points.Max(p=>p.Z)-points.Min(p=>p.Z)];
        if(extent.Max()<=span||faces.Count==1)
        {
            Batch batch=new();
            if(faces.All(f=>{if(!batch.Fits(f))return false;batch.Add(f);return true;})){output.Add(batch);return;}
            if(faces.Count==1)throw new InvalidDataException("Authored native sector exceeds LP/HP vertex, color, offset, or coordinate capacity.");
        }
        int axis=extent[0]>=extent[1]&&extent[0]>=extent[2]?0:extent[1]>=extent[2]?1:2;
        float Centre(Face f)=>f.Points.Average(p=>axis==0?p.X:axis==1?p.Y:p.Z);
        var ordered=faces.Select((f,i)=>(f,i)).OrderBy(t=>Centre(t.f)).ThenBy(t=>t.i).Select(t=>t.f).ToList();
        int half=ordered.Count/2;
        Split(ordered[..half],span,output);Split(ordered[half..],span,output);
    }
    /// <summary>Near tables of an imported sector: up to 255 colour pairs and faces (8-bit indices) and up to
    /// <see cref="MaxNearVertices"/> vertices, within the packed vertex offsets.</summary>
    private static bool ImportedFits(List<Face> faces)
    {
        var v=faces.SelectMany(f=>f.Points).Distinct().ToArray();
        int colors=faces.SelectMany(f=>f.Colours).Distinct().Count();
        int span=ImportedSpanOverride??2047;
        return faces.Count<=255&&v.Length<=MaxNearVertices&&colors<=255&&
            v.Max(p=>p.X)-v.Min(p=>p.X)<=span&&v.Max(p=>p.Y)-v.Min(p=>p.Y)<=span&&v.Max(p=>p.Z)-v.Min(p=>p.Z)<=Math.Min(span,1023)&&
            v.Max(p=>p.X)-v.Min(p=>p.X)+v.Max(p=>p.Y)-v.Min(p=>p.Y)+v.Max(p=>p.Z)-v.Min(p=>p.Z)<=8191;
    }
    private static void SplitImported(List<Face> faces,List<Batch> output)
    {
        if(ImportedFits(faces))
        {
            Batch batch=new(){Imported=true};
            foreach(var f in faces)batch.Add(f);
            output.Add(batch);return;
        }
        if(faces.Count==1)throw new InvalidDataException("Authored native sector exceeds LP/HP vertex, color, offset, or coordinate capacity.");
        // Cut along the axis furthest over its limit: faces in order along it, each piece as long as it still fits.
        var points=faces.SelectMany(f=>f.Points).ToArray();
        float[] over=[(points.Max(p=>p.X)-points.Min(p=>p.X))/2047,(points.Max(p=>p.Y)-points.Min(p=>p.Y))/2047,(points.Max(p=>p.Z)-points.Min(p=>p.Z))/1023];
        int axis=over[0]>=over[1]&&over[0]>=over[2]?0:over[1]>=over[2]?1:2;
        float Low(Face f)=>f.Points.Min(p=>axis==0?p.X:axis==1?p.Y:p.Z);
        var ordered=faces.Select((f,i)=>(f,i)).OrderBy(t=>Low(t.f)).ThenBy(t=>t.i).Select(t=>t.f).ToList();
        List<Face> piece=[];
        foreach(var f in ordered)
        {
            piece.Add(f);
            if(piece.Count>1&&!ImportedFits(piece)){piece.RemoveAt(piece.Count-1);SplitImported(piece,output);piece=[f];}
        }
        // A piece that cannot shrink along this axis (one face over a limit) is split by count.
        if(piece.Count==faces.Count){int half=faces.Count/2;SplitImported(ordered[..half],output);SplitImported(ordered[half..],output);return;}
        SplitImported(piece,output);
    }
    private static bool NeedsFarMesh(Batch b)=>b.Vertices.Count>64||b.Colors.Count>64||b.Vertices.Count+b.Colors.Count+2*b.Faces.Count>255;
    /// <summary>The far mesh of a big sector: its vertices merged on a grid (the cell grows until the LP tables fit
    /// their 6-bit indices and one-byte HP offset), one colour per merged vertex (the faces' distant colours averaged),
    /// faces that collapse dropped. Drawn only beyond the near-detail distance.</summary>
    /// <remarks>Never an empty far table: the far pass transforms its vertices in a loop that ends when its pointer
    /// equals the table's end (USA 0x80026350), and with no vertex it is already past it, so it runs on through the
    /// scratchpad into the I/O registers (seen as a hang in Sunny Villa). With <paramref name="maxFaces"/> the cell grows
    /// until at most that many faces remain; when the next size leaves none, the last mesh with faces is kept.</remarks>
    private static FarMesh Simplify(Batch b,int maxFaces=int.MaxValue)
    {
        float x0=b.Vertices.Min(p=>p.X),y0=b.Vertices.Min(p=>p.Y),z0=b.Vertices.Min(p=>p.Z);
        FarMesh? fewest=null;
        foreach(float cell in new[]{8f,12,16,24,32,48,64,96,128,192,256,384,512,768,1024,1536,2048,4096})
        {
            Dictionary<(int,int,int),int> ids=[];
            List<(double X,double Y,double Z,double R,double G,double B,int N)> sums=[];
            int Cluster(Vector3f p,ColorRgba c)
            {
                var key=((int)((p.X-x0)/cell),(int)((p.Y-y0)/cell),(int)((p.Z-z0)/cell));
                if(!ids.TryGetValue(key,out int id)){id=sums.Count;ids.Add(key,id);sums.Add(default);}
                var s=sums[id];sums[id]=(s.X+p.X,s.Y+p.Y,s.Z+p.Z,s.R+c.R,s.G+c.G,s.B+c.B,s.N+1);
                return id;
            }
            List<int[]> faces=[];List<uint> flags=[];HashSet<string> seen=[];
            foreach(var f in b.Faces)
            {
                var ring=FarOrder(f).Select(i=>Cluster(f.Points[i],f.Lp[i])).ToList();
                List<int> corners=[];
                foreach(int id in ring)if(corners.Count==0||corners[^1]!=id)corners.Add(id);
                if(corners.Count>1&&corners[0]==corners[^1])corners.RemoveAt(corners.Count-1);
                if(corners.Distinct().Count()!=corners.Count||corners.Count<3)continue;
                if(!seen.Add(string.Join(",",corners.Order())))continue;
                faces.Add([..corners]);flags.Add((f.Flags&TwoSidedFaceFlag)!=0?FarTwoSidedFlag:0);
            }
            var used=faces.SelectMany(f=>f).Distinct().Order().ToList();
            if(used.Count>64||2*used.Count+2*faces.Count>255||faces.Count==0)continue;
            var remap=used.Select((id,i)=>(id,i)).ToDictionary(t=>t.id,t=>t.i);
            var vertices=used.Select(id=>{var s=sums[id];return new Vector3f((float)Math.Round(s.X/s.N),(float)Math.Round(s.Y/s.N),(float)Math.Round(s.Z/s.N));}).ToList();
            var colors=used.Select(id=>{var s=sums[id];return new ColorRgba((byte)Math.Round(s.R/s.N),(byte)Math.Round(s.G/s.N),(byte)Math.Round(s.B/s.N));}).ToList();
            fewest=new(vertices,colors,faces.Select(f=>f.Select(id=>remap[id]).ToArray()).ToList(),null,flags);
            if(faces.Count<=maxFaces)return fewest;
        }
        return fewest??LargestFace(b);
    }
    /// <summary>A distant mesh of the sector's largest face alone (its faces are too small and scattered to survive any
    /// merging grid).</summary>
    private static FarMesh LargestFace(Batch b)
    {
        static double Area(Face f)
        {
            var a=f.Points[0];double sum=0;
            for(int i=1;i+1<f.Points.Length;i++)
            {
                var u=f.Points[i];var v=f.Points[i+1];
                double ux=u.X-a.X,uy=u.Y-a.Y,uz=u.Z-a.Z,vx=v.X-a.X,vy=v.Y-a.Y,vz=v.Z-a.Z;
                double cx=uy*vz-uz*vy,cy=uz*vx-ux*vz,cz=ux*vy-uy*vx;sum+=Math.Sqrt(cx*cx+cy*cy+cz*cz)/2;
            }
            return sum;
        }
        var face=b.Faces.MaxBy(Area)!;var order=FarOrder(face);
        return new([..order.Select(i=>face.Points[i])],[..order.Select(i=>face.Lp[i])],[[..Enumerable.Range(0,order.Length)]],null,
            [(face.Flags&TwoSidedFaceFlag)!=0?FarTwoSidedFlag:0]);
    }
    /// <summary>A face's corners in far (LP) perimeter order: reversed for a face whose front is reversed, since the far
    /// pass cannot flip the front (its colours follow the corners).</summary>
    private static int[] FarOrder(Face f)=>(f.Flags&FrontReversedFaceFlag)==0?[..Enumerable.Range(0,f.Points.Length)]:f.Points.Length==3?[0,2,1]:[0,3,2,1];
    private static float Distance2(Vector3f a,Vector3f b){float x=a.X-b.X,y=a.Y-b.Y,z=a.Z-b.Z;return x*x+y*y+z*z;}
    /// <summary>The sector's own distant mesh (<see cref="SceneDocument.FarMeshes"/>) fitted to the far tables, or null
    /// (none given, or it cannot fit). A sector split into pieces gives each piece the distant faces most of whose
    /// corners are its own near corners (otherwise the piece whose box centre is nearest the face).</summary>
    private static FarMesh? Supplied(Batch batch,List<Batch> batches,Dictionary<int,AuthoredFarMesh>? meshes,int maxFaces=int.MaxValue)
    {
        if(meshes==null||!meshes.TryGetValue(batch.Group,out var mesh))return null;
        var pieces=batches.Where(b=>b.Imported&&b.Group==batch.Group).ToList();
        IReadOnlyList<AuthoredFarFace> faces=mesh.Faces;
        if(pieces.Count>1)
        {
            var own=pieces.Select(p=>p.Vertices.ToHashSet()).ToList();
            var centres=pieces.Select(p=>new Vector3f((p.Vertices.Min(v=>v.X)+p.Vertices.Max(v=>v.X))/2,(p.Vertices.Min(v=>v.Y)+p.Vertices.Max(v=>v.Y))/2,(p.Vertices.Min(v=>v.Z)+p.Vertices.Max(v=>v.Z))/2)).ToList();
            int Owner(AuthoredFarFace f)
            {
                var pts=f.Corners.Select(i=>mesh.Vertices[i]).ToArray();
                int[] score=[..own.Select(h=>pts.Count(h.Contains))];
                int best=score.Max();
                if(best>0&&score.Count(x=>x==best)==1)return Array.IndexOf(score,best);
                var c=new Vector3f(pts.Average(p=>p.X),pts.Average(p=>p.Y),pts.Average(p=>p.Z));
                return Enumerable.Range(0,pieces.Count).Where(k=>score[k]==best).MinBy(k=>Distance2(centres[k],c));
            }
            int me=pieces.IndexOf(batch);
            faces=[..mesh.Faces.Where(f=>Owner(f)==me)];
        }
        return Fit(batch,mesh,faces,maxFaces);
    }
    /// <summary>A distant mesh fitted to one sector's far tables, which share the sector's origin: corners are kept
    /// inside the sector's box, repeated corners and faces dropped, the shortest edge collapsed (onto its corner used
    /// by more faces) until at most 64 corners remain and the nearest colours merged until at most 64 remain. Null
    /// when nothing is left or the tables still overflow the header's one-byte offset.</summary>
    private static FarMesh? Fit(Batch batch,AuthoredFarMesh mesh,IReadOnlyList<AuthoredFarFace> source,int maxFaces=int.MaxValue)
    {
        // The sector's box grows to take the distant corners when the packed offsets still fit; otherwise they are
        // pulled into the near faces' box.
        var all=batch.Vertices.Concat(source.SelectMany(f=>f.Corners).Select(i=>mesh.Vertices[i]).Select(p=>new Vector3f(MathF.Round(p.X),MathF.Round(p.Y),MathF.Round(p.Z)))).ToList();
        bool grow=all.Max(p=>p.X)-all.Min(p=>p.X)<=2047&&all.Max(p=>p.Y)-all.Min(p=>p.Y)<=2047&&all.Max(p=>p.Z)-all.Min(p=>p.Z)<=1023&&all.Min(p=>Math.Min(p.X,Math.Min(p.Y,p.Z)))>=0;
        var box=grow?all:batch.Vertices;
        float x0=box.Min(p=>p.X),y0=box.Min(p=>p.Y),z0=box.Min(p=>p.Z);
        float x1=box.Max(p=>p.X),y1=box.Max(p=>p.Y),z1=box.Max(p=>p.Z);
        List<Vector3f> vertices=[];Dictionary<Vector3f,int> vertexIds=[];
        int Vertex(Vector3f p)
        {
            var q=new Vector3f(Math.Clamp(MathF.Round(p.X),x0,x1),Math.Clamp(MathF.Round(p.Y),y0,y1),Math.Clamp(MathF.Round(p.Z),z0,z1));
            if(!vertexIds.TryGetValue(q,out int id)){id=vertices.Count;vertexIds.Add(q,id);vertices.Add(q);}
            return id;
        }
        List<ColorRgba> colors=[];Dictionary<ColorRgba,int> colorIds=[];
        int Colour(ColorRgba c)
        {
            c=c with{A=255};
            if(!colorIds.TryGetValue(c,out int id)){id=colors.Count;colorIds.Add(c,id);colors.Add(c);}
            return id;
        }
        List<(int[] V,int[] C)> faces=[..source.Select(f=>((int[])[..f.Corners.Select(i=>Vertex(mesh.Vertices[i]))],(int[])[..f.Colors.Select(i=>Colour(mesh.Colors[i]))]))];
        int[] merged=[..Enumerable.Range(0,vertices.Count)];
        int Find(int a){while(merged[a]!=a)a=merged[a]=merged[merged[a]];return a;}
        List<(int[] V,int[] C)> Clean()
        {
            List<(int[] V,int[] C)> output=[];HashSet<string> seen=[];
            foreach(var (v,c) in faces)
            {
                List<int> cv=[],cc=[];
                for(int k=0;k<v.Length;k++){int r=Find(v[k]);if(cv.Count>0&&cv[^1]==r)continue;cv.Add(r);cc.Add(c[k]);}
                if(cv.Count>1&&cv[0]==cv[^1]){cv.RemoveAt(cv.Count-1);cc.RemoveAt(cc.Count-1);}
                if(cv.Count<3||cv.Distinct().Count()!=cv.Count||!seen.Add(string.Join(",",cv.Order())))continue;
                output.Add(([..cv],[..cc]));
            }
            return output;
        }
        var live=Clean();
        while(live.Count>0&&(live.SelectMany(f=>f.V).Distinct().Count()>64||live.Count>maxFaces))
        {
            var uses=live.SelectMany(f=>f.V).GroupBy(i=>i).ToDictionary(g=>g.Key,g=>g.Count());
            int ea=-1,eb=-1;float best=float.MaxValue;
            foreach(var (v,_) in live)
                for(int k=0;k<v.Length;k++)
                {
                    int a=Math.Min(v[k],v[(k+1)%v.Length]),b=Math.Max(v[k],v[(k+1)%v.Length]);float d=Distance2(vertices[a],vertices[b]);
                    if(d<best||d==best&&(a<ea||a==ea&&b<eb)){best=d;ea=a;eb=b;}
                }
            if(uses[ea]>=uses[eb])merged[eb]=ea;else merged[ea]=eb;
            live=Clean();
        }
        if(live.Count==0)return null;
        // Colours: the closest pair (by their use-weighted averages) merges until 64 remain.
        Dictionary<int,(double R,double G,double B,int N)> sums=[];
        foreach(int i in live.SelectMany(f=>f.C)){var c=colors[i];sums[i]=sums.TryGetValue(i,out var s)?(s.R+c.R,s.G+c.G,s.B+c.B,s.N+1):(c.R,c.G,c.B,1);}
        int[] colourTo=[..Enumerable.Range(0,colors.Count)];
        int ColourFind(int a){while(colourTo[a]!=a)a=colourTo[a]=colourTo[colourTo[a]];return a;}
        while(sums.Count>64)
        {
            var keys=sums.Keys.Order().ToArray();int ka=-1,kb=-1;double best=double.MaxValue;
            for(int i=0;i<keys.Length;i++)for(int j=i+1;j<keys.Length;j++)
            {
                var a=sums[keys[i]];var b=sums[keys[j]];
                double dr=a.R/a.N-b.R/b.N,dg=a.G/a.N-b.G/b.N,db=a.B/a.N-b.B/b.N,d=dr*dr+dg*dg+db*db;
                if(d<best){best=d;ka=keys[i];kb=keys[j];}
            }
            var p=sums[ka];var q=sums[kb];sums[ka]=(p.R+q.R,p.G+q.G,p.B+q.B,p.N+q.N);sums.Remove(kb);colourTo[kb]=ka;
        }
        var usedVertices=live.SelectMany(f=>f.V).Distinct().Order().ToList();
        var usedColours=sums.Keys.Order().ToList();
        var vertexMap=usedVertices.Select((id,i)=>(id,i)).ToDictionary(t=>t.id,t=>t.i);
        var colourMap=usedColours.Select((id,i)=>(id,i)).ToDictionary(t=>t.id,t=>t.i);
        int lv=usedVertices.Count,lc=usedColours.Count,lf=live.Count;
        if(lv>64||lc>64||lv+lc+2*lf>255)return null;
        return new([..usedVertices.Select(i=>vertices[i])],
            [..usedColours.Select(i=>{var s=sums[i];return new ColorRgba((byte)Math.Round(s.R/s.N),(byte)Math.Round(s.G/s.N),(byte)Math.Round(s.B/s.N));})],
            [..live.Select(f=>f.V.Select(i=>vertexMap[i]).ToArray())],[..live.Select(f=>f.C.Select(i=>colourMap[ColourFind(i)]).ToArray())],
            mesh.TwoSided?[..live.Select(_=>FarTwoSidedFlag)]:null,true);
    }
    /// <summary>The header's bounding radius: the farthest corner from the header centre, rounded up (retail
    /// headers store this; the game culls and picks the detail tier with it).</summary>
    private static int Radius(List<Vector3f> vertices,int cx,int cy,int cz)
    {
        double r=vertices.Max(p=>Math.Sqrt((p.X-cx)*(p.X-cx)+(p.Y-cy)*(p.Y-cy)+(double)(p.Z-cz)*(p.Z-cz)));
        int radius=(int)Math.Ceiling(r)+1;
        if(radius>0x1FFF)throw new InvalidDataException("Authored native sector is too large for its header radius.");
        return radius;
    }
    /// <summary>A quad whose corners turn the same way round seen along its average normal (Newell), flat or not.</summary>
    private static bool ConvexQuad(Vector3f[] p)
    {
        double nx=0,ny=0,nz=0;
        for(int i=0;i<4;i++){var a=p[i];var b=p[(i+1)%4];nx+=(a.Y-b.Y)*(a.Z+b.Z);ny+=(a.Z-b.Z)*(a.X+b.X);nz+=(a.X-b.X)*(a.Y+b.Y);}
        double[] n=[nx,ny,nz];
        if(n.All(x=>x==0))return false;
        int drop=Enumerable.Range(0,3).MaxBy(i=>Math.Abs(n[i]));int[] axes=Enumerable.Range(0,3).Where(i=>i!=drop).ToArray();
        double Coordinate(Vector3f v,int axis)=>axis==0?v.X:axis==1?v.Y:v.Z;
        int sign=0;
        for(int i=0;i<4;i++)
        {
            var x=p[i];var y=p[(i+1)%4];var z=p[(i+2)%4];
            double cross=(Coordinate(y,axes[0])-Coordinate(x,axes[0]))*(Coordinate(z,axes[1])-Coordinate(y,axes[1]))-(Coordinate(y,axes[1])-Coordinate(x,axes[1]))*(Coordinate(z,axes[0])-Coordinate(y,axes[0]));
            if(cross==0||sign!=0&&Math.Sign(cross)!=sign)return false;sign=Math.Sign(cross);
        }
        return true;
    }
    private static bool CompatibleQuad(Vector3f[] p)
    {
        double[] a=[p[1].X-p[0].X,p[1].Y-p[0].Y,p[1].Z-p[0].Z],b=[p[2].X-p[0].X,p[2].Y-p[0].Y,p[2].Z-p[0].Z],c=[p[3].X-p[0].X,p[3].Y-p[0].Y,p[3].Z-p[0].Z];
        double[] n=[a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
        if(n.Zip(c).Sum(x=>x.First*x.Second)!=0||n.All(x=>x==0))return false;
        int drop=Enumerable.Range(0,3).MaxBy(i=>Math.Abs(n[i]));int[] axes=Enumerable.Range(0,3).Where(i=>i!=drop).ToArray();
        double Coordinate(Vector3f v,int axis)=>axis==0?v.X:axis==1?v.Y:v.Z;
        int sign=0;
        for(int i=0;i<4;i++)
        {
            var x=p[i];var y=p[(i+1)%4];var z=p[(i+2)%4];
            double cross=(Coordinate(y,axes[0])-Coordinate(x,axes[0]))*(Coordinate(z,axes[1])-Coordinate(y,axes[1]))-(Coordinate(y,axes[1])-Coordinate(x,axes[1]))*(Coordinate(z,axes[0])-Coordinate(y,axes[0]));
            if(cross==0||sign!=0&&Math.Sign(cross)!=sign)return false;sign=Math.Sign(cross);
        }
        return true;
    }
    private static byte[] Encode(Batch batch,int cache,bool trueRadius)
    {
        int v=batch.Vertices.Count,c=batch.Colors.Count,f=batch.Faces.Count;
        var far=batch.Far;
        // The far pass loops over a sector's far vertices until its pointer meets the table's end: none would never end.
        if(far!=null&&(far.Vertices.Count==0||far.Faces.Count==0))throw new InvalidDataException("Authored native sector has an empty distant mesh.");
        int lv=far?.Vertices.Count??v,lc=far?.Colors.Count??c,lf=far?.Faces.Count??f;
        int lpv=28,lpc=lpv+4*lv,lpf=lpc+4*lc,hpv=lpf+8*lf,hpfar=hpv+4*v,hpnear=hpfar+4*c,hpf=hpnear+4*c,end=hpf+16*f;
        // A scene-given distant mesh may reach past the near faces: the header's box and sphere hold both tiers.
        var box=far is {Supplied:true}?[..batch.Vertices,..far.Vertices]:batch.Vertices;
        byte[] s=new byte[end+4];int x=(int)box.Min(p=>p.X),y=(int)box.Min(p=>p.Y),z=(int)box.Min(p=>p.Z);
        int mx=(int)box.Max(p=>p.X),my=(int)box.Max(p=>p.Y),mz=(int)box.Max(p=>p.Z);
        W(s,0,(uint)(((x+mx)/2<<16)|((y+my)/2)));W(s,4,(uint)(((z+mz)/2<<16)|(trueRadius?Radius(box,(x+mx)/2,(y+my)/2,(z+mz)/2):mx-x+my-y+mz-z)));W(s,8,(uint)(x<<16|y));W(s,12,(uint)(z<<16|cache));
        s[16]=(byte)lv;s[17]=(byte)lc;s[18]=(byte)lf;s[20]=(byte)v;s[21]=(byte)c;s[22]=(byte)f;W(s,24,uint.MaxValue);
        uint Pack(Vector3f p)=>(uint)(((int)p.X-x)<<21|((int)p.Y-y)<<10|((int)p.Z-z));
        for(int i=0;i<v;i++){uint packed=Pack(batch.Vertices[i]);if(far==null)W(s,lpv+4*i,packed);W(s,hpv+4*i,packed);}
        // The face pass modulates a textured face with the first near table (hpfar) up close and fades to the second (hpnear)
        // by the near distance (USA 0x80026DB8..0x80026FB4; subdivided faces read the first only); the far tier draws the
        // face untextured in its distant colours, which share the near tables' colour indices.
        for(int i=0;i<c;i++){if(far==null)W(s,lpc+4*i,Rgb(batch.Colors[i].Distant));W(s,hpfar+4*i,Rgb(batch.Colors[i].Far));W(s,hpnear+4*i,Rgb(batch.Colors[i].Near));}
        if(far!=null)
        {
            for(int i=0;i<lv;i++)W(s,lpv+4*i,Pack(far.Vertices[i]));
            for(int i=0;i<lc;i++)W(s,lpc+4*i,Rgb(far.Colors[i]));
            for(int i=0;i<lf;i++)
            {
                int[] corners=far.Faces[i],colors=far.FaceColors?[i]??corners;int[] order=corners.Length==3?[2,1,0,0]:[3,2,0,1];
                uint Word(int[] k)=>(uint)(k[order[0]]<<26|k[order[1]]<<20|k[order[2]]<<14|k[order[3]]<<8);
                W(s,lpf+i*8,Word(corners)|(far.FaceFlags?[i]??0));W(s,lpf+i*8+4,Word(colors));
            }
        }
        for(int i=0;i<f;i++)
        {
            var face=batch.Faces[i];int[] vertex=face.Points.Select(p=>batch.Vertices.IndexOf(p)).ToArray(),color=face.Colours.Select(p=>batch.Colors.IndexOf(p)).ToArray();
            // HP quads test (0,1,3), then (2,1,3); LP stores the
            // corresponding GPU strip (0,1,3,2). Reverse the authored
            // perimeter, as the established HP triangle path does, so both
            // tiers accept the same side. Colors follow their vertex corners.
            int[] lp=face.Points.Length==3?[2,1,0,0]:[3,2,0,1],hp=face.Points.Length==3?[2,1,0,0]:[3,2,1,0];
            uint Low(int[] indexes)=>(uint)(indexes[lp[0]]<<26|indexes[lp[1]]<<20|indexes[lp[2]]<<14|indexes[lp[3]]<<8);
            uint High(int[] indexes)=>(uint)(indexes[hp[0]]<<24|indexes[hp[1]]<<16|indexes[hp[2]]<<8|indexes[hp[3]]);
            if(far==null)
            {
                int[] ring=FarOrder(face);
                W(s,lpf+i*8,Low([..ring.Select(k=>vertex[k])])|((face.Flags&TwoSidedFaceFlag)!=0?FarTwoSidedFlag:0));W(s,lpf+i*8+4,Low([..ring.Select(k=>color[k])]));
            }
            W(s,hpf+i*16,High(vertex));W(s,hpf+i*16+4,High(color));W(s,hpf+i*16+8,(uint)face.Material);
            W(s,hpf+i*16+12,face.Material==255?(uint)(128|face.Flags&(TwoSidedFaceFlag|FrontReversedFaceFlag)):(uint)face.Flags);
        }
        Id65EnvironmentSectorHeaderContract.Stamp(s.AsSpan(0,end));_=Id65EnvironmentSectorHeaderContract.ReadPointerSpan(s,s.Length,false,"authored batch");return s;
    }
    private static int Unit(float v){if(v!=MathF.Truncate(v)||v is <0 or >16383)throw new InvalidDataException("Native authored vertices require whole editor units.");return(int)v;}
    private static uint Rgb(ColorRgba c)=>(uint)(c.R|c.G<<8|c.B<<16);
    private static void W(Span<byte>b,int at,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b[at..],v);
}

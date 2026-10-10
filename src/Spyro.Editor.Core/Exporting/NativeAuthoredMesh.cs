using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Authored triangles use the original executable's packed LP/HP and collision consumers.
/// Sector guards, full cell membership and per-face material bindings are generated together.</summary>
internal static class NativeAuthoredMesh
{
    /// <summary>Collision triangles for a whole built level. The lookup index takes 15-bit triangle numbers; the
    /// game's own levels use a few thousand (Dark Hollow about 3,000), imported levels more.</summary>
    internal const int MaxCollisionTriangles = 0x7FFF;
    /// <summary>Terrain and collision data of a built level. Retail levels carry up to 656 KB of it in their data
    /// subfile (Misty Bog; Artisans 566 KB); the export's RAM checks decide whether a level fits with its objects.</summary>
    internal const int MaxSurfaceBytes = 0xA0000;
    internal sealed record Triangle(Vector3f A, Vector3f B, Vector3f C, ColorRgba Color, int Material, ColorRgba[] Near, ColorRgba[] Far)
    {
        /// <summary>The face's ground kind (null: solid ground); every lattice piece keeps it.</summary>
        public AuthoredSurfaceKind? Kind { get; init; }
        /// <summary>The surface's <see cref="AuthoredSurface.TwoSided"/> and <see cref="AuthoredSurface.FrontReversed"/>;
        /// every lattice piece keeps them.</summary>
        public bool TwoSided { get; init; }
        public bool FrontReversed { get; init; }
    }
    internal static byte[] Compile(SceneDocument document, IReadOnlyList<AuthoredMaterial> materials) => CompileWithSky(document, materials, null);
    /// <summary>As <see cref="Compile"/>, with an optional borrowed retail sky block in place of the plain sky color.</summary>
    internal static byte[] CompileWithSky(SceneDocument document, IReadOnlyList<AuthoredMaterial> materials, byte[]? skyBlock,
        IEnumerable<(Vector3f A, Vector3f B, Vector3f C, AuthoredSurfaceKind Kind)>? collisionOnly = null)
    {
        document.Validate();
        List<Triangle> triangles = [];
        foreach (var s in document.Surfaces)
        {
            int material = s.Material == null ? 255 : materials.IndexOf(s.Material);
            if (material < 0) throw new InvalidDataException("An authored material was omitted.");
            var near=s.NearColors??Enumerable.Repeat(s.Color,s.Vertices.Length).ToArray();
            var far=s.FarColors??Enumerable.Repeat(s.Color,s.Vertices.Length).ToArray();
            triangles.Add(new(s.Vertices[0], s.Vertices[1], s.Vertices[2], s.Color,material,[near[0],near[1],near[2]],[far[0],far[1],far[2]]) { Kind = s.Kind, TwoSided = s.TwoSided, FrontReversed = s.FrontReversed });
            if (s.Vertices.Length == 4) triangles.Add(new(s.Vertices[0],s.Vertices[2],s.Vertices[3],s.Color,material,[near[0],near[2],near[3]],[far[0],far[2],far[3]]) { Kind = s.Kind, TwoSided = s.TwoSided, FrontReversed = s.FrontReversed });
        }
        List<Triangle> nativeTriangles=[];
        foreach(var triangle in triangles)
        {
            // FF material uses the source renderer's direct untextured packet
            // path, which cannot near-clip a large face. Keep editor faces intact;
            // render and collision consume the same exact generated lattice.
            // A textured face is drawn whole by the batched renderer, which takes it from the document.
            // Only its collision is taken from this list, and one collision triangle can span at most
            // 255 units per axis, so a larger textured face is split here for collision alone.
            if(triangle.Material==255) AppendLattice(nativeTriangles,triangle,64);
            else AppendLattice(nativeTriangles,triangle,255);
            if(nativeTriangles.Count>MaxCollisionTriangles)throw new InvalidDataException($"The level needs more than {MaxCollisionTriangles} collision triangles; reduce geometry.");
        }
        triangles=nativeTriangles;
        if (triangles.Count < 1 || triangles.Count > MaxCollisionTriangles) throw new InvalidDataException($"Authored geometry requires 1 to {MaxCollisionTriangles} generated collision triangles.");
        foreach(var t in triangles)
        {
            var a=new Vector3f(t.B.X-t.A.X,t.B.Y-t.A.Y,t.B.Z-t.A.Z);var b=new Vector3f(t.C.X-t.A.X,t.C.Y-t.A.Y,t.C.Z-t.A.Z);
            if(a.Y*b.Z-a.Z*b.Y==0&&a.Z*b.X-a.X*b.Z==0&&a.X*b.Y-a.Y*b.X==0)
                throw new InvalidDataException("An authored triangle has zero area.");
        }
        // Legacy direct-only geometry has one sector per lattice triangle.
        // Textured or mixed geometry proves its actual batched queue cost below.
        int n = triangles.Count;
        byte[] environment,visibility;
        if(triangles.Any(t=>t.Material!=255)||triangles.Count>127)
            (environment,visibility)=NativeAuthoredMeshRenderer.Compile(document,materials,triangles.Where(t=>t.Material==255).ToArray());
        else
        {
            const int span = 116;
            environment = new byte[8+n*(4+span)]; W(environment,0,(uint)environment.Length); W(environment,4,(uint)n);
            for (int i=0;i<n;i++)
            {
                int at=8+n*4+i*span; W(environment,8+i*4,(uint)(at-4));
                EncodeSector(environment.AsSpan(at,span),triangles[i],i+1);
            }
            int list=(n+4)&~3, skyAt=16+list;
            visibility=new byte[skyAt+12]; W(visibility,0,(uint)visibility.Length);W(visibility,4,(uint)(skyAt-4));W(visibility,8,1);W(visibility,12,12);
            visibility.AsSpan(16,list).Fill(255);for(int i=0;i<n;i++)visibility[16+i]=(byte)i;
            W(visibility,skyAt,1);W(visibility,skyAt+4,8);visibility.AsSpan(skyAt+8,4).Fill(255);
        }
        // Collision without a drawn face (a homeworld doorway's entry triangles).
        foreach (var (a, b, c, kind) in collisionOnly ?? [])
            triangles.Add(new(a, b, c, new(0, 0, 0), 255, [new(0, 0, 0), new(0, 0, 0), new(0, 0, 0)], [new(0, 0, 0), new(0, 0, 0), new(0, 0, 0)]) { Kind = kind });
        n = triangles.Count;
        if (n > MaxCollisionTriangles) throw new InvalidDataException($"The level needs more than {MaxCollisionTriangles} collision triangles; reduce geometry.");
        // Special surfaces: the ground kinds the level's faces use, in order of first use. The component is its
        // length, the record count, one offset per record (from the count word) and the records; a collision
        // triangle's flag byte names its record (0x3F: solid ground).
        var kinds=triangles.Select(t=>t.Kind).OfType<AuthoredSurfaceKind>().Distinct().ToList();
        if(kinds.Count>63)throw new InvalidDataException("A built level can use at most 63 ground kinds.");
        byte[] special;
        {
            var records=kinds.Select(k=>k.NativeRecord()).ToArray();
            special=new byte[8+4*kinds.Count+records.Sum(r=>r.Length)];W(special,0,(uint)special.Length);W(special,4,(uint)kinds.Count);
            int at=8+4*kinds.Count;
            for(int i=0;i<records.Length;i++){W(special,8+4*i,(uint)(at-4));records[i].CopyTo(special,at);at+=records[i].Length;}
        }
        Dictionary<NativeCollisionCell,List<int>> cells=[];
        for(int i=n-1;i>=0;i--)
        {
            var t=triangles[i];var vs=new[]{t.A,t.B,t.C};
            for(int z=Raw(vs.Min(v=>v.Z))>>12;z<=Raw(vs.Max(v=>v.Z))>>12;z++)
            for(int y=Raw(vs.Min(v=>v.Y))>>12;y<=Raw(vs.Max(v=>v.Y))>>12;y++)
            for(int x=Raw(vs.Min(v=>v.X))>>12;x<=Raw(vs.Max(v=>v.X))>>12;x++)
            {var cell=new NativeCollisionCell(x,y,z);if(!cells.TryGetValue(cell,out var rows))cells.Add(cell,rows=[]);rows.Add(i);}
        }
        var index=NativeCollisionIndexCodec.Create(cells.Select(p=>new KeyValuePair<NativeCollisionCell,IReadOnlyList<int>>(p.Key,p.Value)),n);
        byte[] tree=index.CopyTree(),blocks=index.CopyBlocks();int start=32+tree.Length+blocks.Length,assign=start+n*12,flags=assign+Align4(n);
        byte[] collision=new byte[flags+Align4(n)];W(collision,0,(uint)collision.Length);W(collision,4,(uint)n);W(collision,8,(uint)n);W(collision,12,28);
        W(collision,16,(uint)(28+tree.Length));W(collision,20,(uint)(start-4));W(collision,24,(uint)(assign-4));W(collision,28,(uint)(flags-4));
        tree.CopyTo(collision,32);blocks.CopyTo(collision,32+tree.Length);
        for(int i=0;i<n;i++)
        {
            // A reversed face shows its other side, so a wall of it is solid from that side (CollisionWinding keeps a
            // vertical triangle's order, reversed from the side it is given as visible).
            var t=triangles[i];var (a,b,c)=t.FrontReversed?CollisionWinding(t.A,t.C,t.B):CollisionWinding(t.A,t.B,t.C);
            W(collision,start+i*12,Axis(a.X,b.X,c.X));W(collision,start+i*12+4,Axis(a.Y,b.Y,c.Y));W(collision,start+i*12+8,HeightAxis(a.Z,b.Z,c.Z));
        }
        for(int i=0;i<n;i++)collision[flags+i]=(byte)(triangles[i].Kind is {} kind?kinds.IndexOf(kind):0x3F);
        byte[] sky=new byte[16];W(sky,0,16);W(sky,4,Rgb(document.SkyColor));
        if(skyBlock!=null){sky=skyBlock;visibility=WithSkyParts(visibility,skyBlock);}
        byte[] bundle=[..environment,..visibility,..special,..collision,..sky];
        if(bundle.Length>MaxSurfaceBytes)throw new InvalidDataException("The level's terrain and collision data is larger than the game can load; reduce geometry.");
        return bundle;
    }
    /// <summary>A collision triangle's stored corner order. The game takes the solid side from the winding:
    /// floors are stored clockwise seen from above, and a wall must be stored the same way round (reversed from its
    /// visible side), or Spyro walks through it from the side he can see. Native collision Z has unsigned byte deltas
    /// at 16/24, unlike the signed 9-bit X/Y deltas at 14/23, so the order is then rotated cyclically to put the
    /// lowest corner first without reversing it.</summary>
    internal static (Vector3f A,Vector3f B,Vector3f C) CollisionWinding(Vector3f a,Vector3f b,Vector3f c)
    {
        if((b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X)>=0)(b,c)=(c,b);
        if(b.Z<a.Z&&b.Z<=c.Z)(a,b,c)=(b,c,a);
        else if(c.Z<a.Z&&c.Z<b.Z)(a,b,c)=(c,a,b);
        return (a,b,c);
    }
    /// <summary>Reads the sky of a retail level for a scene that borrows it. The block is copied whole; its part
    /// pointers are relative to the block, so it needs no relocation.</summary>
    internal static byte[]? CaptureSky(string originalUSA, SceneDocument document)
    {
        if (document.SkySourceWadEntry is not int entry) return null;
        var level=new Spyro.Editor.Core.Universal.UniversalLevelData(Spyro.Editor.Core.Universal.UniversalDiscImage.Read(originalUSA).EntryBytes(entry));
        var model=level.Subfiles()[1];
        return Spyro1SkyBlockAnalyzer.ReadPrimarySkyBlock(level.Bytes.AsSpan(model.Offset,model.Length).ToArray())
            ?? throw new InvalidDataException("The chosen sky level has no native sky.");
    }
    /// <summary>A built level has one view group, so its sky group lists every part of the borrowed sky.
    /// Retail groups use the same form: part ids terminated by 255, padded to a word.</summary>
    private static byte[] WithSkyParts(byte[] visibility,byte[] skyBlock)
    {
        if(!Spyro1SkyBlockAnalyzer.IsValidStandaloneSkyBlock(skyBlock))throw new InvalidDataException("The borrowed sky is not one native sky block.");
        int parts=(int)BinaryPrimitives.ReadUInt32LittleEndian(skyBlock.AsSpan(8));
        if(parts>254)throw new InvalidDataException("The borrowed sky has more parts than one view group can list.");
        int skyAt=4+(int)BinaryPrimitives.ReadUInt32LittleEndian(visibility.AsSpan(4));
        if(skyAt!=visibility.Length-12)throw new InvalidDataException("The built level's view groups changed shape.");
        int list=Align4(parts+1);
        byte[] result=new byte[skyAt+8+list];visibility.AsSpan(0,skyAt).CopyTo(result);
        W(result,0,(uint)result.Length);W(result,skyAt,1);W(result,skyAt+4,8);
        result.AsSpan(skyAt+8,list).Fill(255);for(int i=0;i<parts;i++)result[skyAt+8+i]=(byte)i;
        return result;
    }
    /// <summary>Splits a triangle into an even lattice whose pieces span at most <paramref name="cell"/> units per
    /// axis. 64 is the direct untextured renderer's limit: those pieces are drawn and collided with, so their
    /// corners must fall exactly on whole units. 255 is the collision encoding's limit and applies to textured
    /// faces, whose pieces are used for collision only: when no exact lattice exists their inner corners are
    /// rounded to the nearest whole unit (less than one unit from the drawn face).</summary>
    private static void AppendLattice(List<Triangle> output,Triangle t,int cell)
    {
        int[][] v=[new[]{Unit(t.A.X),Unit(t.A.Y),Unit(t.A.Z)},new[]{Unit(t.B.X),Unit(t.B.Y),Unit(t.B.Z)},new[]{Unit(t.C.X),Unit(t.C.Y),Unit(t.C.Z)}];
        int extent=Enumerable.Range(0,3).Max(d=>v.Max(p=>p[d])-v.Min(p=>p[d]));
        int gcd=0;
        for(int d=0;d<3;d++){gcd=Gcd(gcd,Math.Abs(v[1][d]-v[0][d]));gcd=Gcd(gcd,Math.Abs(v[2][d]-v[0][d]));}
        int divisions=Math.Max(1,(extent+cell-1)/cell);
        while(divisions<=15&&gcd%divisions!=0)divisions++;
        bool rounded=false;
        if(divisions>15&&cell==255)
        {
            // Collision only: one unit of headroom so rounding cannot push a piece past the 255-unit range.
            divisions=Math.Max(1,(extent+253)/254);rounded=true;
        }
        if(divisions>15||output.Count+divisions*divisions>MaxCollisionTriangles)
            throw new InvalidDataException(cell==64
                ?"Untextured authored faces need exact whole-unit subdivision to at most 64 units per axis. This face cannot fit that lattice; change its vertices or reduce geometry."
                :$"The level's textured tiles need more collision pieces than the game allows ({MaxCollisionTriangles} for the whole level). Use fewer or smaller textured tiles.");
        if(divisions==1){output.Add(t);return;}
        Vector3f Point(int i,int j)=>rounded
            ?new(MathF.Round(v[0][0]+((v[1][0]-v[0][0])*i+(v[2][0]-v[0][0])*j)/(float)divisions),
                 MathF.Round(v[0][1]+((v[1][1]-v[0][1])*i+(v[2][1]-v[0][1])*j)/(float)divisions),
                 MathF.Round(v[0][2]+((v[1][2]-v[0][2])*i+(v[2][2]-v[0][2])*j)/(float)divisions))
            :new(
            v[0][0]+((v[1][0]-v[0][0])*i+(v[2][0]-v[0][0])*j)/divisions,
            v[0][1]+((v[1][1]-v[0][1])*i+(v[2][1]-v[0][1])*j)/divisions,
            v[0][2]+((v[1][2]-v[0][2])*i+(v[2][2]-v[0][2])*j)/divisions);
        ColorRgba Color(ColorRgba[] colors,int i,int j)
        {
            int a=divisions-i-j;
            byte Channel(byte x,byte y,byte z)=>(byte)((a*x+i*y+j*z+divisions/2)/divisions);
            return new(Channel(colors[0].R,colors[1].R,colors[2].R),Channel(colors[0].G,colors[1].G,colors[2].G),Channel(colors[0].B,colors[1].B,colors[2].B));
        }
        void Add((int I,int J) a,(int I,int J) b,(int I,int J) c)
        {
            Vector3f pa=Point(a.I,a.J),pb=Point(b.I,b.J),pc=Point(c.I,c.J);
            if(rounded)
            {
                // Rounding can flatten a sliver of a very thin face; a zero-area piece adds no collision.
                float ux=pb.X-pa.X,uy=pb.Y-pa.Y,uz=pb.Z-pa.Z,wx=pc.X-pa.X,wy=pc.Y-pa.Y,wz=pc.Z-pa.Z;
                if(uy*wz-uz*wy==0&&uz*wx-ux*wz==0&&ux*wy-uy*wx==0)return;
            }
            output.Add(new(pa,pb,pc,t.Color,t.Material,
                [Color(t.Near,a.I,a.J),Color(t.Near,b.I,b.J),Color(t.Near,c.I,c.J)],
                [Color(t.Far,a.I,a.J),Color(t.Far,b.I,b.J),Color(t.Far,c.I,c.J)]) { Kind = t.Kind, TwoSided = t.TwoSided, FrontReversed = t.FrontReversed });
        }
        for(int i=0;i<divisions;i++)for(int j=0;j<divisions-i;j++)
        {
            Add((i,j),(i+1,j),(i,j+1));
            if(i+j<divisions-1)Add((i+1,j),(i+1,j+1),(i,j+1));
        }
    }
    private static int Gcd(int a,int b){while(b!=0)(a,b)=(b,a%b);return a;}
    private static void EncodeSector(Span<byte> s,Triangle t,int faceCacheStart)
    {
        Vector3f[] v=[t.A,t.B,t.C];int x=v.Min(v=>Unit(v.X)),y=v.Min(v=>Unit(v.Y)),z=v.Min(v=>Unit(v.Z));
        int maxX=v.Max(v=>Unit(v.X)),maxY=v.Max(v=>Unit(v.Y)),maxZ=v.Max(v=>Unit(v.Z));
        int radius=maxX-x+maxY-y+maxZ-z;
        if(radius==0)throw new InvalidDataException("An authored triangle is degenerate.");
        var ab=new Vector3f(t.B.X-t.A.X,t.B.Y-t.A.Y,t.B.Z-t.A.Z);var ac=new Vector3f(t.C.X-t.A.X,t.C.Y-t.A.Y,t.C.Z-t.A.Z);
        if(ab.Y*ac.Z-ab.Z*ac.Y==0 && ab.Z*ac.X-ab.X*ac.Z==0 && ab.X*ac.Y-ab.Y*ac.X==0)throw new InvalidDataException("An authored triangle has zero area.");
        // Native 80026810..34 indexes per-face subdivision scratch with the low
        // halfword. Retail HP ranges partition 1..totalFaces; byte zero is reserved.
        // Each authored sector has one HP face, so give it a distinct native index.
        W(s,0,(uint)(((x+maxX)/2<<16)|((y+maxY)/2)));W(s,4,(uint)(((z+maxZ)/2<<16)|radius));W(s,8,(uint)((x<<16)|y));W(s,12,(uint)((z<<16)|faceCacheStart));
        new byte[]{3,3,1,0,3,3,1,8}.CopyTo(s[16..]);W(s,24,uint.MaxValue);
        for(int i=0;i<3;i++)
        {
            int dx=Unit(v[i].X)-x,dy=Unit(v[i].Y)-y,dz=Unit(v[i].Z)-z;
            if(dx>2047||dy>2047||dz>1023)throw new InvalidDataException("Authored sector vertex exceeds native packed coordinates.");
            uint vertex=(uint)((dx<<21)|(dy<<10)|dz);W(s,28+i*4,vertex);W(s,60+i*4,vertex);
            W(s,40+i*4,Rgb(t.Far[i]));W(s,72+i*4,Rgb(t.Far[i]));W(s,84+i*4,Rgb(t.Near[i]));
        }
        uint low=(1u<<20)|(2u<<14)|(2u<<8), high=(1u<<16)|(2u<<24);
        // A reversed face: its far copy turns the other way (the far pass has no front flip); the near flag below.
        if(t.FrontReversed)low=(2u<<20)|(1u<<14)|(1u<<8);
        // Retail textured faces use the native subdivision/near-plane path.
        // FF has no descriptor: retain its direct packet flag and bounded mesh.
        uint sides=(t.TwoSided?(uint)NativeAuthoredMeshRenderer.TwoSidedFaceFlag:0)|(t.FrontReversed?(uint)NativeAuthoredMeshRenderer.FrontReversedFaceFlag:0);
        W(s,52,low|(t.TwoSided?NativeAuthoredMeshRenderer.FarTwoSidedFlag:0));W(s,56,low);W(s,96,high);W(s,100,high);W(s,104,(uint)t.Material);W(s,108,(t.Material==255?0x80u:0u)|sides);
        Id65EnvironmentSectorHeaderContract.Stamp(s[..112]);
        var header=Id65EnvironmentSectorHeaderContract.ReadPointerSpan(s,s.Length,false,"authored triangle");
        if(header.ExactSectorByteLength!=112||header.PointerGapByteLength!=4)throw new InvalidDataException("Authored triangle sector lost its native prefetch guard.");
    }
    private static uint Axis(float a,float b,float c)
    {
        int origin=Unit(a),db=Unit(b)-origin,dc=Unit(c)-origin;
        if(db is <-256 or >255||dc is <-256 or >255)throw new InvalidDataException("An authored collision triangle spans more than its exact native delta range. Subdivide it before export.");
        return (uint)origin|(((uint)db&511)<<14)|(((uint)dc&511)<<23);
    }
    private static int Raw(float value)=>checked(Unit(value)*16);
    private static uint HeightAxis(float a,float b,float c)
    {
        int origin=Unit(a),db=Unit(b)-origin,dc=Unit(c)-origin;
        if(db is <0 or >255||dc is <0 or >255)throw new InvalidDataException("Authored collision Z requires its lowest vertex origin and exact unsigned byte deltas.");
        return (uint)origin|((uint)db<<16)|((uint)dc<<24);
    }
    private static int Unit(float value)
    {if(value!=MathF.Truncate(value)||value is <0 or >16383)throw new InvalidDataException("Native authored vertices require whole editor units (the exact 16-unit game grid).");return (int)value;}
    private static int Align4(int x)=>(x+3)&~3;
    private static uint Rgb(ColorRgba c)=>(uint)(c.R|c.G<<8|c.B<<16);
    private static void W(Span<byte>b,int at,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(b[at..],v);
    private static int IndexOf<T>(this IReadOnlyList<T> list,T item){for(int i=0;i<list.Count;i++)if(EqualityComparer<T>.Default.Equals(list[i],item))return i;return -1;}
}

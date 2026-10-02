using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Conservative whole-footprint floor support, using the exact
/// (0,1,2)/(0,2,3) triangles emitted by NativeAuthoredMesh. Not path clearance.</summary>
public static class NativeAuthoredSpawn
{
    public const float PlayerRadius = 356f / 16;
    // Conservative authoring policy: ordinary entry keeps moving after control
    // returns. This is an area-support certificate, not a complete trajectory proof.
    public const float PortalArrivalAreaRadius = 192;
    private const int MaximumFragments = 4096;
    private readonly record struct Point(double X, double Y);

    public static void RequireSupported(SceneDocument document)
        => RequireArea(document,PlayerRadius,"Move the player start above a solid floor, with room around its edges. The entire player footprint needs a floor within 128 editor units below the start and room for Spyro.");

    /// <summary>Certify a continuous 384-wide landing area around the rounded
    /// spawn. Retains the original 128 maximum drop and 22.25 vertical clearance;
    /// does not certify the incoming animation, airborne obstacles or held input.</summary>
    public static void RequirePortalArrivalSupported(SceneDocument document)
        => RequireArea(document,PortalArrivalAreaRadius,"Portal arrival needs a continuous solid landing area at least 384 editor units wide, centered on the player start. Add floor around the start or move the start onto a wider platform; keep the floor within 128 units below it and leave room above the floor for Spyro.");

    private static void RequireArea(SceneDocument document,double areaRadius,string failure)
    {
        document.Validate();
        // Use the same native 1/16-unit rounding as the compiled scene header.
        var p = new Vector3f(MathF.Round(document.Spawn.X*16)/16,
            MathF.Round(document.Spawn.Y*16)/16,MathF.Round(document.Spawn.Z*16)/16);
        double r = areaRadius;
        // Certify the whole square, including its corners. For spawn-only checks
        // it contains the circular native footprint; sampling cannot exclude holes.
        List<List<Point>> uncovered = [[new(-r,-r),new(r,-r),new(r,r),new(-r,r)]];
        foreach (var face in document.Surfaces)
        {
            var v = face.Vertices;
            Cover(v[0],v[1],v[2]);
            if (v.Length == 4) Cover(v[0],v[2],v[3]);
            if (uncovered.Count == 0) return;
        }
        throw new InvalidDataException(failure);

        void Cover(Vector3f a,Vector3f b,Vector3f c)
        {
            double ax=a.X-p.X,ay=a.Y-p.Y,bx=b.X-p.X,by=b.Y-p.Y,cx=c.X-p.X,cy=c.Y-p.Y;
            double determinant=(bx-ax)*(cy-ay)-(by-ay)*(cx-ax);
            if (Math.Abs(determinant)<1e-12) return;
            double zx=((b.Z-a.Z)*(cy-ay)-(c.Z-a.Z)*(by-ay))/determinant;
            double zy=((bx-ax)*(c.Z-a.Z)-(cx-ax)*(b.Z-a.Z))/determinant;
            if (zx*zx+zy*zy>1) return;
            double z0=a.Z-zx*ax-zy*ay;
            List<Point> floor=[new(ax,ay),new(bx,by),new(cx,cy)];
            if (determinant<0) floor.Reverse();
            floor=Clip(floor,q=>p.Z-PlayerRadius-(z0+zx*q.X+zy*q.Y));
            floor=Clip(floor,q=>z0+zx*q.X+zy*q.Y-(p.Z-128));
            if (floor.Count<3) return;
            List<List<Point>> next=[];
            foreach (var fragment in uncovered)
            {
                var remaining=fragment;
                for (int i=0;i<floor.Count && remaining.Count>=3;i++)
                {
                    Point e=floor[i],f=floor[(i+1)%floor.Count];
                    double Side(Point q)=>(f.X-e.X)*(q.Y-e.Y)-(f.Y-e.Y)*(q.X-e.X);
                    var outside=Clip(remaining,q=>-Side(q));
                    if (HasArea(outside)) next.Add(outside);
                    remaining=Clip(remaining,Side);
                }
                if (next.Count>MaximumFragments)
                    throw new InvalidDataException("Player start floor support is too fragmented to certify. Use a continuous floor beneath the start.");
            }
            uncovered=next;
        }
    }

    // Convex half-plane clipping. Boundary lines have zero area; calculations
    // use spawn-local doubles to preserve precision at large world coordinates.
    private static List<Point> Clip(List<Point> polygon,Func<Point,double> side)
    {
        List<Point> result=[];
        if (polygon.Count==0) return result;
        Point previous=polygon[^1]; double before=side(previous);
        foreach (Point current in polygon)
        {
            double after=side(current);
            if ((before>=0)!=(after>=0))
            {
                double t=before/(before-after);
                result.Add(new(previous.X+t*(current.X-previous.X),previous.Y+t*(current.Y-previous.Y)));
            }
            if (after>=0) result.Add(current);
            previous=current;before=after;
        }
        return result;
    }

    private static bool HasArea(List<Point> polygon)
    {
        if (polygon.Count<3) return false;
        double area=0;
        for(int i=1;i+1<polygon.Count;i++)
            area+=(polygon[i].X-polygon[0].X)*(polygon[i+1].Y-polygon[0].Y)-
                  (polygon[i].Y-polygon[0].Y)*(polygon[i+1].X-polygon[0].X);
        // Numerical residue far below the native whole-unit vertex grid.
        return Math.Abs(area)>1e-9;
    }
}

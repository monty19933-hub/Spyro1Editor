using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

internal static class PortalLandingCases
{
    internal static void Run(Action<bool,string> check,Action<Action,string> refuse)
    {
        AuthoredSurface Rect(float x0,float y0,float x1,float y1,float z=544,float slope=0)
            =>new(Guid.NewGuid().ToString("N"),[new(x0,y0,z+slope*(x0-560)),new(x1,y0,z+slope*(x1-560)),new(x1,y1,z+slope*(x1-560)),new(x0,y1,z+slope*(x0-560))],new(128,96,64),null);
        SceneDocument With(params AuthoredSurface[] surfaces)=>SceneDocument.New("Landing Proof") with {Surfaces=surfaces};
        void Accept(SceneDocument scene,string name)
        {string before=scene.Fingerprint();NativeAuthoredSpawn.RequirePortalArrivalSupported(scene);check(scene.Fingerprint()==before,name+" accepts without changing terrain");}
        void Reject(SceneDocument scene,string name)
        {string before=scene.Fingerprint();refuse(()=>NativeAuthoredSpawn.RequirePortalArrivalSupported(scene),name);check(scene.Fingerprint()==before,name+" preserves terrain");}
        var small=With(Rect(496,496,624,624));NativeAuthoredSpawn.RequireSupported(small);
        check(true,"original spawn-only128 platform check remains supported");Reject(small,"128 platform has no complete arrival area");
        Reject(With(Rect(400,400,720,720)),"320 platform remains narrower than conservative384 policy");
        var exact=With(Rect(368,368,752,752));Accept(exact,"exact384 continuous area");
        var two=With(new(Guid.NewGuid().ToString("N"),[new(368,368,544),new(752,368,544),new(368,752,544)],new(128,96,64),null),
            new(Guid.NewGuid().ToString("N"),[new(752,368,544),new(752,752,544),new(368,752,544)],new(128,96,64),null));
        Accept(two,"adjacent triangles cover area across a shared edge");
        Reject(With(two.Surfaces[0]),"single triangle leaves opposite corner unsupported");
        // Tiny off-center hole misses both spawn footprint and ordinary sampling grids.
        var hole=With(Rect(368,368,630,752),Rect(631,368,752,752),Rect(630,368,631,630),Rect(630,631,631,752));
        NativeAuthoredSpawn.RequireSupported(hole);check(true,"off-center hole does not alter original spawn proof");Reject(hole,"exact polygon subtraction finds one-unit off-center hole");
        var slit=With(Rect(368,368,630,752),Rect(630.01f,368,752,752));Reject(slit,"sub-unit slit is not hidden by point sampling");
        Accept(With(Rect(368,368,752,752,448)),"exact128 maximum vertical drop");
        Reject(With(Rect(368,368,752,752,447)),"floor more than128 below spawn");
        Accept(With(Rect(368,368,752,752,554)) with {Spawn=new(560,560,576.25f)},"vertical clearance remains22.25 independent of area192");
        Reject(With(Rect(368,368,752,752,554)),"too little vertical player clearance");
        Accept(With(Rect(368,368,752,752,544,1f/32)),"gentle slope retains full-area vertical support");
        Reject(With(Rect(368,368,752,752,544,1)),"slope clips high and low unsupported parts of landing area");
        var rounded=exact with {Spawn=new(560.03f,560,576)};Accept(rounded,"spawn rounds to native one-sixteenth unit");
        Reject(exact with {Spawn=new(560.04f,560,576)},"one-sixteenth spawn shift exposes edge strip");
        var translated=With(Rect(14368,14368,14752,14752)) with {Spawn=new(14560,14560,576)};Accept(translated,"area-local clipping remains stable at large coordinates");
        check(NativeAuthoredSpawn.PortalArrivalAreaRadius==192&&NativeAuthoredSpawn.PlayerRadius==22.25f,"horizontal policy and vertical native footprint stay independent");
        Console.WriteLine("NATIVE_PORTAL_LANDING_AREA_OK areaWidth=384 maxFloorDrop=128 verticalClearance=22.25 trajectoryProof=false");
    }
}

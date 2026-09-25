using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks=0;
void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
void Refuse(Action action){try{action();}catch(InvalidDataException){checks++;return;}throw new Exception("Expected refusal");}
var positions=NativeJapaneseEggArtisansPlacementPlan.CopyFixedRecipe();
NativeJapaneseEggArtisansPlacementPlan.RequireFixedRecipe(positions);
Check(positions.Length==2&&positions.All(p=>p.X==72704&&p.Z==6656&&p.ProfileIndex==1),"original-mode1 fixed lawn pair");
Check(positions[1].Y-positions[0].Y==2048,"separate egg centers");
foreach(int i in new[]{0,1})
{
 foreach(var bad in new[]{positions[i] with{X=positions[i].X+1},positions[i] with{Z=6144},positions[i] with{ProfileIndex=9},positions[i] with{Id="unowned"}})
 {var changed=positions.ToArray();changed[i]=bad;Refuse(()=>NativeJapaneseEggArtisansPlacementPlan.RequireFixedRecipe(changed));}
}
Refuse(()=>NativeJapaneseEggArtisansPlacementPlan.RequireFixedRecipe([]));
Refuse(()=>NativeJapaneseEggArtisansPlacementPlan.RequireFixedRecipe(positions.Reverse().ToArray()));
positions[0]=positions[0] with{X=1};
Check(NativeJapaneseEggArtisansPlacementPlan.CopyFixedRecipe()[0].X==72704,"caller recipe copy is not source authority or mutable shared state");
Check(NativeJapaneseEggArtisansPlacementPlan.SquaredCorridorDistance(72704,47104)==0,"endpoint distance");
Check(NativeJapaneseEggArtisansPlacementPlan.SquaredCorridorDistance(72704,48000)==0,"interior distance");
Check(NativeJapaneseEggArtisansPlacementPlan.SquaredCorridorDistance(74240,48000)==1536L*1536,"exact lateral separation boundary");
Check(NativeJapaneseEggArtisansPlacementPlan.SquaredCorridorDistance(72704,45568)==1536L*1536,"exact longitudinal separation boundary");
if(args.Length==0){Console.WriteLine($"JP_EGG_PLACEMENT_SOURCE_FREE_OK checks={checks}");return;}
if(args.Length!=3||args[0]!="--source")throw new ArgumentException("No args or --source <JP-BIN> <USA-BIN>");
using var jp=NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.JapanScps10083);
using var usa=NativeRegionalSourceSession.Open(args[2],NativeDiscRevision.UsaScus94228);
var plan=NativeJapaneseEggArtisansPlacementPlan.Capture(usa);
Check(plan.SourceCertified&&!plan.GameplayVerified,"source support is not gameplay");
Check(plan.Neighbors.Count==174&&plan.Neighbors.All(n=>n.SquaredHorizontalDistance>=1536L*1536),"entire corridor separates all original centers without vertical exemption");
Check(plan.SupportTriangleCount>0&&plan.NativeCollisionBindingCount>=plan.SupportTriangleCount,"full continuous dry support has actual native index bindings");
var integration=NativeJapaneseEggArtisansIntegrationPlan.Capture(jp,usa,plan.Placements);
plan.RequireIntegration(integration);Check(true,"actual integration uses exact source-issued recipe");
var another=NativeJapaneseEggArtisansIntegrationPlan.Capture(jp,usa,[new("egg-a",82000,75000,6500,32,1),new("egg-b",83024,75000,6500,96,1)]);
Refuse(()=>plan.RequireIntegration(another));Refuse(()=>NativeJapaneseEggArtisansPlacementPlan.Capture(jp));
Check(plan.PlanSha256==NativeJapaneseEggArtisansPlacementPlan.Capture(usa).PlanSha256,"source capture is deterministic");
jp.VerifyUnchanged();usa.VerifyUnchanged();
Console.WriteLine($"JP_EGG_PLACEMENT_SOURCE_OK checks={checks} plan={plan.PlanSha256} support={plan.SupportTriangleCount} bindings={plan.NativeCollisionBindingCount} gameplayVerified=false");

namespace Spyro.Editor.Core.Universal;
/// <summary>Immutable exact original USA enrollment. Counts never imply admission.</summary>
internal sealed class GreenDruidWorldSourceProfile
{
 internal int DruidRow{get;} internal int TargetRow{get;} internal byte Group{get;}
 internal bool HasTarget=>TargetRow>=0;
 internal int[] Members=>HasTarget?[DruidRow,TargetRow]:[DruidRow];
 internal int[] Classes=>HasTarget?[0x10E,0x148]:[0x10E]; internal int Sector{get;}
 internal int[] Sectors=>[Sector]; internal int[] Materials=>DruidRow==5?[21,28,43]:[24,41,42];
 internal int[] FixedTriangles=>DruidRow==5?[4207,4217,4218,4219,4220,4229,4259,4290,4291,4292,4293,4294,4295,4296,4297,4298,4299,4300,4301,4302,4303,4304,4312,4313,4314,4315,4316,4319]:[];
 internal int CollisionFirst=>DruidRow switch{5=>0,15=>246,16=>234,17=>258,_=>throw new InvalidDataException()}; internal int CollisionCount=>DruidRow==5?16:12;
 internal int CollisionFrames=>DruidRow==5?7:3; internal int Commands=>DruidRow==5?100:30;
 internal int FrameOffset=>12+Commands*8; internal int GeometryFrames=>2;
 internal int AnimatedVertices=>DruidRow==5?12:8; internal int Vertices=>DruidRow==5?30:8;
 internal int Faces=>DruidRow==5?22:6; internal int FixedFaceStart=>DruidRow==5?8:6;
 private GreenDruidWorldSourceProfile(int druid,int target,byte group,int sector){DruidRow=druid;TargetRow=target;Group=group;Sector=sector;}
 internal static GreenDruidWorldSourceProfile ForDruid(int row)=>row switch{5=>new(5,48,0,170),15=>new(15,137,6,247),16=>new(16,-1,5,251),17=>new(17,-1,7,246),_=>throw new InvalidDataException("Green Druid source profile is not issued.")};
 internal static GreenDruidWorldSourceProfile ForRow(int row)=>ForDruid(row switch{5 or 48=>5,15 or 137=>15,16=>16,17=>17,_=>throw new InvalidDataException("Green Druid source row is not issued.")});
}

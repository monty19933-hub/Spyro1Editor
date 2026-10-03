using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Universal;
namespace Spyro.Editor.Core.Universal;
/// <summary>Detached authenticated eight-material allocation. It does not issue
/// terrain queue/collision/actor/capacity permission or mutate a destination.</summary>
internal sealed class HighCavesWorldMaterialStorage
{
 private readonly byte[] _gpu;
 private readonly byte[][] _records;
 internal int AllocatedByteCount{get;}
 internal int FreeByteCount{get;}
 internal IReadOnlyList<NativeStaticTerrainTextureAllocationRegion> Regions{get;}
 private HighCavesWorldMaterialStorage(NativeStaticTerrainGpuAllocation result)
 {_gpu=result.CopyGpuPayload();_records=result.Records.Select(r=>r.CopyRecord()).ToArray();AllocatedByteCount=result.AllocatedByteCount;FreeByteCount=result.FreeByteCount;Regions=result.Allocations.ToArray();}
 internal static HighCavesWorldMaterialStorage Capture(string key,UniversalLevelData source,UniversalOverlay overlay,byte[]exe,byte[]hostGpu,UniversalVramAllocator allocator)
 {
  var issued=UniversalHighCavesWorldPrerequisites.CaptureSource(key,source,overlay,exe);byte[] used=new byte[UniversalVramAllocator.ImageBytes];
  for(int i=0;i<used.Length;i++)used[i]=(byte)(allocator.IsFree(i,1)?0:1);
  var inputs=issued.CopyMaterials().Select((material,n)=>new NativeStaticTerrainTextureInput(n,"original-highcaves-group0","original-highcaves-gpu",NativeStaticTerrainTextureAllocation.RelocationPolicyId,material.Record,issued.CopySourceVram())).ToArray();
  return new(NativeStaticTerrainGpuAllocation.Compile(hostGpu,used,inputs));
 }
 internal byte[] CopyGpu()=>_gpu.ToArray();
 internal byte[][] CopyRecords()=>_records.Select(r=>r.ToArray()).ToArray();
 internal void RequireFinal(byte[]gpu,byte[][]records)
 {
  if(!gpu.AsSpan().SequenceEqual(_gpu)||records.Length!=8||records.Where((r,n)=>!r.AsSpan().SequenceEqual(_records[n])).Any())throw new InvalidDataException("High Caves detached material allocation changed issued final bytes.");
 }
}

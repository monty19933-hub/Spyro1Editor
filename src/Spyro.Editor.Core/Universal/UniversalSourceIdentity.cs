using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Universal;

/// <summary>Equivalence is source-bound and compares assets and relocated class-specific behavior.
/// Row positions and private property values are intentionally excluded: those are placement variants.</summary>
public static class UniversalSourceIdentity
{
    private const int CacheCapacity = 256;
    private static readonly Dictionary<(string DataA, string OverlayA, string DataB, string OverlayB, int Class, bool Shared), (bool Equivalent, string Reason)> Cache = [];
    private static string Digest(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));

    public static bool Equivalent(UniversalLevelData a, UniversalOverlay ao, UniversalLevelData b, UniversalOverlay bo,
        int classId, IReadOnlySet<int> shared, out string reason)
    {
        // Content identities prevent stale equivalence after mutation and never retain whole level clones.
        var key = (Digest(a.Bytes), Digest(ao.Bytes), Digest(b.Bytes), Digest(bo.Bytes), classId, shared.Contains(classId));
        lock (Cache)
        {
            if (Cache.TryGetValue(key, out var hit)) { reason = hit.Reason; return hit.Equivalent; }
        }
        bool equivalent = Compare(a, ao, b, bo, classId, shared, out reason);
        lock (Cache)
        {
            if (Cache.Count >= CacheCapacity) Cache.Clear();
            Cache[key] = (equivalent, reason);
        }
        return equivalent;
    }

    private static bool Compare(UniversalLevelData a, UniversalOverlay ao, UniversalLevelData b, UniversalOverlay bo,
        int classId, IReadOnlySet<int> shared, out string reason)
    {
        bool am = a.Registrations().Any(r => r.ClassId == classId), bm = b.Registrations().Any(r => r.ClassId == classId);
        if (am != bm && !shared.Contains(classId)) { reason = "model registration differs"; return false; }
        if (am && bm)
        {
            byte[] ap = a.Package(classId), bp = b.Package(classId);
            // Package pointers and texture UVs are relative; differing bytes require a separately mapped class.
            if (!ap.AsSpan().SequenceEqual(bp)) { reason = "model package differs"; return false; }
            foreach (int f in UniversalAssetImport.TextureDescriptorOffsets(ap))
            {
                int page = BinaryPrimitives.ReadUInt16LittleEndian(ap.AsSpan(f + 6));
                int depth = (page >> 7) & 3;
                int minU = 255, maxU = 0, minV = 255, maxV = 0;
                foreach (int k in new[] { 0, 4, 8, 10 })
                { minU = Math.Min(minU, ap[f + k]); maxU = Math.Max(maxU, ap[f + k]); minV = Math.Min(minV, ap[f + k + 1]); maxV = Math.Max(maxV, ap[f + k + 1]); }
                if (depth > 2) { reason = "unsupported texture depth"; return false; }
                int pixelStart = depth == 0 ? minU / 2 : minU << (depth - 1);
                int pixelEnd = depth == 0 ? maxU / 2 : (maxU << (depth - 1)) + (depth == 2 ? 1 : 0);
                for (int y = minV; y <= maxV; y++)
                {
                    int off = (((page & 16) != 0 ? 256 : 0) + y) * 1024 + (page & 15) * 128 - 1024 + pixelStart;
                    if (off < 0 || off + pixelEnd - pixelStart + 1 > a.Vram().Length ||
                        !a.Vram().Slice(off, pixelEnd - pixelStart + 1).SequenceEqual(b.Vram().Slice(off, pixelEnd - pixelStart + 1)))
                    { reason = "model texture differs or is outside level VRAM"; return false; }
                }
                if (depth < 2)
                {
                    int pal = BinaryPrimitives.ReadUInt16LittleEndian(ap.AsSpan(f + 2));
                    int off = (pal >> 6) * 1024 + (pal & 63) * 32 - 1024, len = depth == 0 ? 32 : 512;
                    if (off < 0 || off + len > a.Vram().Length || !a.Vram().Slice(off, len).SequenceEqual(b.Vram().Slice(off, len)))
                    { reason = "model palette differs or is outside level VRAM"; return false; }
                }
            }
            if (BinaryPrimitives.ReadInt32LittleEndian(ap) > 0)
            {
                var ab = new NativeLevelSoundLayout(a.Bytes); var bb = new NativeLevelSoundLayout(b.Bytes);
                foreach (byte slot in ap.Skip(4).Take(16).Where(s => s != 0xFF))
                {
                    if (slot >= ab.Sound.Descriptors.Count || slot >= bb.Sound.Descriptors.Count ||
                        ab.Sound.Descriptors[slot].Sample.Sha256 != bb.Sound.Descriptors[slot].Sample.Sha256 ||
                        !ab.Sound.Descriptors[slot].CopyDescriptor().AsSpan(4).SequenceEqual(bb.Sound.Descriptors[slot].CopyDescriptor().AsSpan(4)))
                    { reason = "model sound binding differs"; return false; }
                }
            }
        }
        var ast = UniversalDispatchAnalysis.FindUpdateSwitch(ao); var bst = UniversalDispatchAnalysis.FindUpdateSwitch(bo);
        bool ah = UniversalDispatchAnalysis.Handles(ao, ast, classId), bh = UniversalDispatchAnalysis.Handles(bo, bst, classId);
        if (ah != bh) { reason = "class dispatch differs"; return false; }
        if (ah)
        {
            try
            {
                // Fixed destination, source label, and assembly address make overlay relocation comparable.
                byte[] ac = UniversalBehaviorBundleBuilder.Build(ao, [("identity", ao, [classId])], 0x80100000).Code;
                byte[] bc = UniversalBehaviorBundleBuilder.Build(ao, [("identity", bo, [classId])], 0x80100000).Code;
                if (!ac.AsSpan().SequenceEqual(bc)) { reason = "class update/init behavior differs"; return false; }
            }
            catch (InvalidDataException e) { reason = "behavior equivalence unproven: " + e.Message; return false; }
        }
        reason = "same model, textures, sounds and relocated update/init code"; return true;
    }
}

public sealed class UniversalSourceCollisionException(string message) : IOException(message);

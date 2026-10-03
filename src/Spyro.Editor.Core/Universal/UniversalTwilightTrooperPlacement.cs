using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Universal;

/// <summary>Original Twilight93/94 world inputs. Dynamic child ownership is separate.</summary>
public static class UniversalTwilightTrooperPlacement
{
    public const string DataSHA256="a230505350794dcae8205b142a9d60621a4c20bf4c20d3c83818c5a4f7ca0b26";
    public const string OverlaySHA256="55d0845c750c0fcbd13d69633d1481d288bb3b02ce33bf60e3b17866111739dd";
    public static bool Applies(UniversalRowTemplate t)=>string.Equals(t.SourceLevelKey,"twilightharbor",StringComparison.OrdinalIgnoreCase)&&t.ClassId is 0x93 or 0x94;
    internal static void RequireSourceLabel(string key)=>Require(string.Equals(key,"twilightharbor",StringComparison.OrdinalIgnoreCase),"original native source owner was mislabeled");
    private static string H(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static readonly UniversalExeBinding[] Bindings=[
      new(0x800785D8u,0x8005B5A0u,0x8005B5A4u,0x80086004u),
      new(0x800758CCu,0x8005B5B0u,0x8005B5B4u,0x80083108u),
      new(0x80075734u,0x8005B5C0u,0x8005B5C4u,0x8007AE5Cu),
      new(0x800758E4u,0x8005B5D0u,0x8005B5D4u,0x80084EACu),
      new(0x800756BCu,0x8005B5E0u,0x8005B5E4u,0x80083B4Cu)
    ];
    private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Twilight trooper: "+why);}
    internal static void RequireOverlay(UniversalOverlay overlay)
    {Require(overlay.Bindings.Count==Bindings.Length&&Bindings.All(b=>overlay.Bindings.TryGetValue(b.Global,out var got)&&got==b),"complete callback/executable binding identity changed");Require(H(overlay.Bytes)==OverlaySHA256&&overlay.Update==0x8007AE5C&&overlay.Init==0x80083108&&overlay.CopyBuffer==0x80086004,"complete source overlay/callback owner changed");}
    public static void RequireSource(UniversalLevelData data,UniversalOverlay overlay)
    {RequireOverlay(overlay);Require(H(data.Bytes)==DataSHA256,"complete original source data owner changed");}
    internal static void RequireNative(byte[] exe)
    {
        // Complete native moby helpers, including rotated rectangle38C4C and
        // PathData39E94. The caller packets are authenticated in the copied helper.
        Require(exe.Length>=0x2AF20&&H(exe.AsSpan(0x28790,10128))=="5b7e67a79df04ef66ee63fda7d084b73989a87a7886cce5de46db1ad6e93b222","native rectangle/path/helper owner changed");
        foreach(var owner in new[]{(0x8006CBF8u,640,"795a93bee7b4d49bef89d4df8c8628a4b4b3a91d694053dbc2b26b4293d4fa55"),(0x8003A720u,124,"e2d690c6097745e4fb354355436805a619c7f31b9d96fa088b90c0518bc6b056"),(0x800524C4u,164,"6625282792e81215f1f35e1aa8b4240937206bcc8bd494eef9e000f65a4bbf9e"),(0x800526A8u,804,"5483d5e337730567fd89f6118036d31f8c3a93fd281ff444b5d8149b4e2f03d5")})
        {int at=checked((int)(owner.Item1-UniversalExeBindings.ExeBase));Require(at>=0&&at+owner.Item2<=exe.Length&&H(exe.AsSpan(at,owner.Item2))==owner.Item3,"native dynamic allocation/default initializer/model activation owner changed");}

    }
    private static void RequireTemplate(UniversalLevelData data,UniversalRowTemplate t)
    {
        Require(Applies(t),"no original93/94 actor owner");var original=data.CaptureRow(t.SourceLevelKey,t.SourceRowIndex);
        Require(original.ClassId==t.ClassId&&t.Row.SequenceEqual(original.Row)&&t.Properties.SequenceEqual(original.Properties)&&t.InternalPointers.SequenceEqual(original.InternalPointers)&&t.ExternalRowPointers.SequenceEqual(original.ExternalRowPointers)&&t.DroppedExternalPointers==original.DroppedExternalPointers,"complete source row/property/pointer identity changed");
        Require(t.ExternalRowPointers.Count==0&&t.DroppedExternalPointers==0&&t.ImportedRowPointers.Count==0&&t.SourceRowIndices.Count==0&&t.ImportedRowIndices.Count==0&&t.SourcePrerequisiteRows.Count==0,"unproved external source pointer/index/prerequisite");
        if(t.ClassId==0x94)Require(BinaryPrimitives.ReadInt32LittleEndian(t.Properties.AsSpan(0x38))==0,"placed94 must start with its exact empty dynamic B0 pointer");
    }
    public static UniversalRowTemplate Rebase(UniversalLevelData data,UniversalOverlay overlay,UniversalRowTemplate t,byte[] placedRow)
    {
        if(t.ClassId is 0x93 or 0x94 && UniversalTwilightTrooperOwnership.SourceFamily(overlay))RequireSourceLabel(t.SourceLevelKey);
        if(!Applies(t))return t;RequireSource(data,overlay);RequireTemplate(data,t);
        Require(placedRow.Length==UniversalLevelData.RowStride,"placed row is truncated");
        byte[] properties=t.Properties.ToArray();int[] delta=Enumerable.Range(0,3).Select(axis=>checked(BinaryPrimitives.ReadInt32LittleEndian(placedRow.AsSpan(12+axis*4))-BinaryPrimitives.ReadInt32LittleEndian(t.Row.AsSpan(12+axis*4)))).ToArray();HashSet<int> moved=[];
        void Vector(int at)
        {
            Require(at>=0&&at%4==0&&at+12<=properties.Length,"world vector leaves exact source block");
            for(int axis=0;axis<3;axis++)
            {
                int field=at+axis*4;Require(moved.Add(field)&&!t.InternalPointers.Any(p=>p.Offset==field),"world coordinate overlaps another vector/pointer");
                BinaryPrimitives.WriteInt32LittleEndian(properties.AsSpan(field),checked(BinaryPrimitives.ReadInt32LittleEndian(properties.AsSpan(field))+delta[axis]));
            }
        }
        int[] fields=t.ClassId==0x93?[4]:[0x28,0x2C,0x3C];
        Require(t.InternalPointers.Select(p=>p.Offset).Order().SequenceEqual(fields)&&t.InternalPointers.Select(p=>p.Target).Distinct().Count()==fields.Length,"exact native PathData field membership changed");
        foreach(var path in t.InternalPointers)
        {
            int start=path.Target,end=t.InternalPointers.Where(p=>p.Target>start).Select(p=>p.Target).DefaultIfEmpty(properties.Length).Min();
            Require(start>=0&&start+8<=end&&properties[start] is 2 or 3&&start+8+16*properties[start]==end,"native path node count/block ownership changed");
            // Mutable node, reverse/wait fields and each node's fourth word stay
            // native. All world XYZ nodes shift together, including B0's paths.
            for(int node=0;node<properties[start];node++)Vector(start+8+16*node);
        }
        foreach(int at in t.ClassId==0x93?new[]{0x34,0x4C,0x64}:new[]{0x10,0x44,0x5C})Vector(at);
        return t with{Properties=properties};
    }
    public static void RequireInitialRow(UniversalRowTemplate t,ReadOnlySpan<byte> row)
    {
        if(!Applies(t))return;Require(row.Length==UniversalLevelData.RowStride,"placed row is truncated");
        for(int at=4;at<row.Length;at++)if(at is not(>=12 and<24)&&at is not(0x43 or 0x46 or 0x4A))Require(row[at]==t.Row[at],"retain original actor state, frame, material and collision inputs");
        Require(row[0x43]==255&&row[0x4A]==255,"imported actor requires unrestricted activation/visibility");
    }
}

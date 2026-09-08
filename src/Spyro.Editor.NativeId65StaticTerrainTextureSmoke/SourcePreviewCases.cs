// Source-free decoder controls and explicit actual-source preview comparisons.
// Neutral fixtures are never an issued source or preview authority.
using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

internal static class SourcePreviewCases
{
    private const string Original = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const long OriginalLength = 661547040;
    private const string Oracle = "f99881d5641d1d3c255e2fa71b97ded092176500bc41a6731514419ed663f149";

    internal static void Free(Action<bool,string> check, Action<Action,string> reject)
    {
        Type type = typeof(NativeFreshMaterialSourcePreview);
        check(type.IsPublic && type.IsSealed && type.GetConstructors().Length == 0, "Public closed actual-source preview, no public constructor.");
        check(type.GetProperties().All(p => p.SetMethod == null), "All preview properties readonly.");
        MethodInfo capture = type.GetMethod("Capture", BindingFlags.Public | BindingFlags.Static)!;
        check(capture.GetParameters().Length == 1 && capture.GetParameters()[0].ParameterType == typeof(NativeStaticTerrainTextureSource),
            "Only an actual source enters Capture; no bytes/path/hash/metadata constructor.");
        var copy = type.GetMethod("CopyRgba", BindingFlags.Public | BindingFlags.Instance)!.GetParameters();
        check(copy.Length == 1 && copy[0].ParameterType == typeof(int) && Equals(copy[0].DefaultValue, 0),
            "Only palette row is selectable; descriptor is fixed0, never an unchecked descriptor index.");
        reject(() => NativeFreshMaterialSourcePreview.Capture(null!), "Null actual source.");

        // Handwritten neutral palette/nibble truth, not an issued source or wrapper.
        byte[] raw = [0,0,32,0,31,0,8,128], packed = new byte[512], palette = new byte[512];
        for(int i=0;i<packed.Length;i++) packed[i]=(byte)(((i*2)%16)|(((i*2+1)%16)<<4));
        ushort[] words = [0,0x8000,0x001F,0x03E0,0x7C00,0x7FFF,0x4210,0xFFFF,0,0,0,0,0,0,0,0];
        for(int i=0;i<16;i++) BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(i*2,2),words[i]);
        BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(15*32,2),0x001F);
        BinaryPrimitives.WriteUInt16LittleEndian(palette.AsSpan(15*32+2,2),0x7C00);
        var payload = new NativeTerrainLqTextureDescriptorPayload(0,raw,packed,palette);
        Rgba32[] expected = [new(0,0,0,0),new(0,0,0,255),new(255,0,0,255),new(0,255,0,255),
            new(0,0,255,255),new(255,255,255,255),new(132,132,132,255),new(255,255,255,255)];
        check(payload.MaterializeRgba(0).Take(8).SequenceEqual(expected), "Low nibble first; zero transparent,8000 black, channels and midpoints exact.");
        check(payload.MaterializeRgba(15)[0] == new Rgba32(255,0,0,255) &&
            payload.MaterializeRgba(15)[1] == new Rgba32(0,0,255,255), "Exact palette row15, not a distance inference.");
        raw[0]=255; packed[0]=255; Array.Clear(palette);
        check(payload.MaterializeRgba(0).Take(8).SequenceEqual(expected), "Existing decoder copied neutral constructor bytes.");
        var rgba=payload.MaterializeRgba(0); rgba[0]=new(1,2,3,4);
        check(payload.MaterializeRgba(0)[0] == expected[0], "Existing decoder returns fresh RGBA.");
        reject(()=>payload.MaterializeRgba(-1),"Negative palette row");
        reject(()=>payload.MaterializeRgba(16),"Palette row16");
        foreach(var (x,y) in new[]{(-1,0),(32,0),(0,-1),(0,32)})
            reject(()=>payload.ReadPaletteIndex(x,y),"Descriptor32x32 pixel bounds");
        foreach(var (row,index) in new[]{(-1,0),(16,0),(0,-1),(0,16)})
            reject(()=>payload.ReadPaletteWord(row,index),"Descriptor16x16 palette-word bounds");
        var record = new NativeTerrainLqTextureRecordPayload(0, [payload]);
        check(ReferenceEquals(record.GetDescriptor(0),payload), "Neutral existing descriptor lookup retains its actual payload.");
        reject(()=>record.GetDescriptor(-1),"Negative descriptor index");
        reject(()=>record.GetDescriptor(2),"First invalid descriptor index2 in the existing two-entry LQ cache");
        reject(()=>record.GetDescriptor(23),"Far invalid descriptor index23 in the two-entry cache, distinct from the source's23-descriptor layout");
    }

    internal static void Source(string originalBin, Action<bool,string> check, Action<Action,string> reject)
    {
        using var literal = ReadOracle();
        string path = Path.GetFullPath(originalBin);
        FileStream? held = null; List<Exception> failures = [];
        try
        {
            held = File.OpenRead(path); Authenticate(held);
            foreach(var row in literal.RootElement.GetProperty("records").EnumerateArray())
            {
                int entry=row.GetProperty("wadEntry").GetInt32(), id=row.GetProperty("textureId").GetInt32();
                var source=NativeStaticTerrainTextureSource.Capture(path,entry,id);
                var preview=NativeFreshMaterialSourcePreview.Capture(source);
                check(ReferenceEquals(preview.Source,source) && preview.DescriptorIndex==0 && preview.Width==32 &&
                    preview.Height==32 && preview.PaletteRowCount==16, "Preview retains actual source and exact32x32 LQ0 dimensions.");
                check(source.SourceDataSha256==row.GetProperty("dataSha256").GetString() &&
                    source.Layout.RecordSha256==row.GetProperty("recordSha256").GetString(),
                    "Actual source data/record match original-only oracle, not a cached thumbnail.");
                byte[] vram=source.CopySourceVram();
                var descriptor=source.Layout.Descriptors[0];
                byte[] raw=descriptor.CopyDescriptor(), packed=descriptor.CopyPixels(vram), palettes=descriptor.CopyPalette(vram);
                check(raw.Length==8 && Convert.ToHexStringLower(raw)==row.GetProperty("descriptor0Hex").GetString() &&
                    Hash(raw)==row.GetProperty("descriptor0Sha256").GetString() &&
                    Hash(packed)==row.GetProperty("packedIndicesSha256").GetString() &&
                    Hash(palettes)==row.GetProperty("paletteWordsSha256").GetString(),
                    "Actual eight descriptor/512 packed/512 full palette bytes match independent extraction.");
                foreach(int alias in new[]{1,2})
                    check(source.Layout.Descriptors[alias].CopyDescriptor().SequenceEqual(raw) &&
                        source.Layout.Descriptors[alias].CopyPixels(vram).SequenceEqual(packed) &&
                        source.Layout.Descriptors[alias].CopyPalette(vram).SequenceEqual(palettes),
                        "Actual LQ1/leadingHQ aliases agree, not a separate source authority.");
                int count=0;
                foreach(var expected in row.GetProperty("paletteRows").EnumerateArray())
                {
                    int paletteRow=expected.GetProperty("row").GetInt32();
                    check(paletteRow==count++, "All16 ordered palette rows");
                    Rgba32[] pixels=preview.CopyRgba(paletteRow);
                    check(pixels.Length==1024 && Hash(Bytes(pixels))==expected.GetProperty("rgbaSha256").GetString() &&
                        pixels.Count(p=>p.A==0)==expected.GetProperty("transparentPixels").GetInt32(),
                        $"Actual source WAD{entry}/T{id}/palette{paletteRow} independent RGBA literal.");
                    pixels[0]=new(1,2,3,4);
                    check(Hash(Bytes(preview.CopyRgba(paletteRow)))==expected.GetProperty("rgbaSha256").GetString(),
                        "Mutating returned wrapper image cannot change later output.");
                }
                check(count==16 && preview.CopyRgba().SequenceEqual(preview.CopyRgba(0)), "Default explicitly means palette row0.");
                var second=NativeFreshMaterialSourcePreview.Capture(source);
                check(!ReferenceEquals(second,preview) && ReferenceEquals(second.Source,source) &&
                    second.CopyRgba(0).SequenceEqual(preview.CopyRgba(0)), "Independent wrapper from same actual source remains valid.");
                if(entry==14 && id==0)
                {
                    var recaptured=NativeStaticTerrainTextureSource.Capture(path,entry,id);
                    var reissued=NativeFreshMaterialSourcePreview.Capture(recaptured);
                    check(!ReferenceEquals(recaptured,source) && recaptured.Handle==source.Handle &&
                        ReferenceEquals(reissued.Source,recaptured) && !ReferenceEquals(reissued.Source,preview.Source) &&
                        reissued.CopyRgba().SequenceEqual(preview.CopyRgba()),
                        "Equal-byte genuine re-capture is valid but never aliases the earlier actual source reference.");
                }
                Array.Clear(vram);Array.Clear(raw);Array.Clear(packed);Array.Clear(palettes);
                check(Hash(Bytes(preview.CopyRgba(0)))==row.GetProperty("paletteRows")[0].GetProperty("rgbaSha256").GetString(),
                    "Unrelated returned source copies cannot mutate preview ownership.");
                reject(()=>preview.CopyRgba(-1),"Actual preview negative row");
                reject(()=>preview.CopyRgba(16),"Actual preview row16");
                check(!preview.DestinationAllocationProven && !preview.SchematicRenderingProven &&
                    !preview.RuntimeVerified && !preview.PublicationApproved, "Thumbnail never claims relocation, projected floor, GPU or runtime.");
            }
        }
        catch(Exception error){failures.Add(error);}
        finally
        {
            try{if(held!=null)Authenticate(held);}catch(Exception error){failures.Add(error);}
            FileStream? current=null;
            try{current=File.OpenRead(path);Authenticate(current);}catch(Exception error){failures.Add(error);}
            finally{try{current?.Dispose();}catch(Exception error){failures.Add(error);}}
            try{held?.Dispose();}catch(Exception error){failures.Add(error);}
        }
        if(failures.Count==1)ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if(failures.Count>1)throw new IOException("Preview primary and all original final/close failures.",new AggregateException(failures));
    }

    private static JsonDocument ReadOracle()
    {
        using var stream=typeof(SourcePreviewCases).Assembly.GetManifestResourceStream("MaterialPreviewSourceOracle") ??
            throw new InvalidDataException("Missing frozen original-only preview oracle.");
        Require(stream.Length==14746,"Exact bounded oracle length.");
        byte[] raw=new byte[14746];stream.ReadExactly(raw);
        Require(stream.Position==raw.Length && stream.ReadByte()==-1 && stream.Position==raw.Length &&
            Hash(raw)==Oracle,"Exact complete oracle SHA/EOF.");
        var result=JsonDocument.Parse(raw);
        try
        {
            var root=result.RootElement;
            Require(root.GetProperty("records").GetArrayLength()==4 && root.GetProperty("sourceSha256").GetString()==Original &&
                root.GetProperty("heldAndNamedOriginalFinalAndClosed").GetBoolean() &&
                !root.GetProperty("actualCSharpPreviewCompared").GetBoolean() && !root.GetProperty("staticEligibilityIssued").GetBoolean() &&
                !root.GetProperty("outputImageWritten").GetBoolean(),"Original-only expected metadata is not an actual preview result.");
            return result;
        }
        catch { result.Dispose(); throw; }
    }
    private static byte[] Bytes(Rgba32[] pixels)
    {
        byte[] result=new byte[checked(pixels.Length*4)];
        for(int i=0;i<pixels.Length;i++){result[i*4]=pixels[i].R;result[i*4+1]=pixels[i].G;result[i*4+2]=pixels[i].B;result[i*4+3]=pixels[i].A;}
        return result;
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Authenticate(Stream stream)
    {
        Require(stream.CanRead && stream.CanSeek && stream.Length==OriginalLength,"Exact whole original size/capabilities.");
        stream.Position=0;Require(Convert.ToHexStringLower(SHA256.HashData(stream))==Original && stream.Position==OriginalLength &&
            stream.Length==OriginalLength && stream.ReadByte()==-1 && stream.Position==OriginalLength && stream.Length==OriginalLength,
            "Complete original SHA and true EOF.");
    }
    private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
}

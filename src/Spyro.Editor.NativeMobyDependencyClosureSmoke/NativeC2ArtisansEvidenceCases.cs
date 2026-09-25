using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

// Test-only observations, not a C2 schema, dependency closure or instantiation
// issuer. The eight zero bytes are an inspected window, never a full-size claim.
internal static class NativeC2ArtisansEvidenceCases
{
    private const int SceneAt=0x1C9800,SceneBytes=0x14800,RowsAt=0xA2AC,FixupsAt=0x13ED8;
    private const string DataSha="86cde4f886d97821a3a4b869100b586842dbd4b9fb696558bead274bb547af4f";
    private const string OverlaySha="7df6cf2f3ed4a71a123f03621019470bd6fe0cd243f75b1e8c3a115e6b1fcb55";
    private const string ExeSha="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private const string SceneSha="6ca481a134fa5e2801b31cfeb97ec22a8bfb56c46cd0990ecc5718ab61a99342";
    private sealed record Window(int Ordinal,int RowAt,int PropertiesAt,int ObservedBytes);

    internal static async Task Source(string image,Action<bool,string> check,Action<Action,string> reject)
    {
        string path=Path.GetFullPath(image);FileStream? held=null;List<Exception> failures=[];
        try
        {
            held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(held,CancellationToken.None);
            var layout=DiscImage.DetectLayout(held);
            var wad=DiscImage.FindRootFileRecord(held,layout,n=>n.Equals("WAD.WAD",StringComparison.OrdinalIgnoreCase));
            var exe=DiscImage.FindRootFileRecord(held,layout,n=>n.Equals("SCUS_942.28",StringComparison.OrdinalIgnoreCase));
            Require(layout==new DiscLayout(2352,24,22,2048)&&wad.Lba==37&&wad.Size==0x6927000&&
                exe.Lba==53875&&exe.Size==0x66000,"C2 evidence original ISO owners");
            byte[] entries=DiscImage.ReadFileBytes(held,layout,wad.Lba,9*8,16);
            Require(Word(entries,0)==0x7F2800&&Word(entries,4)==0xE000&&Word(entries,8)==0x800800&&
                Word(entries,12)==0x383000,"C2 evidence original WAD entries");
            byte[] data=DiscImage.ReadFileBytes(held,layout,wad.Lba,0x800800,0x383000);
            byte[] overlay=DiscImage.ReadFileBytes(held,layout,wad.Lba,0x7F2800,0xE000);
            byte[] executable=DiscImage.ReadFileBytes(held,layout,exe.Lba,0,exe.Size);
            Exercise(data,overlay,executable,check,reject);
        }
        catch(Exception error){failures.Add(error);}
        finally
        {
            // Even a failed assertion must finish both independent whole-image
            // observations. Preserve primary/final/close errors; never write.
            if(held is not null)await Observe(()=>NativeId65FreshDiscSource.RequireWholeOriginalAsync(held,CancellationToken.None));
            FileStream? current=null;
            try
            {
                current=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
                await NativeId65FreshDiscSource.RequireWholeOriginalAsync(current,CancellationToken.None);
            }
            catch(Exception error){failures.Add(error);}
            finally{if(current is not null)Attempt(current.Dispose);}
            if(held is not null)Attempt(held.Dispose);
        }
        NativeFreshResearchSourceOwners.ThrowFailures(failures);
        Console.WriteLine("NATIVE_C2_ARTISANS_EVIDENCE_NOOP_SOURCE_OK rows=50,51 observedBytes=8 completeExtentProven=false mutableSchema=false placementApproved=false outputWritten=false");
        async Task Observe(Func<Task> action){try{await action();}catch(Exception error){failures.Add(error);}}
        void Attempt(Action action){try{action();}catch(Exception error){failures.Add(error);}}
    }

    private static void Exercise(byte[] data,byte[] overlay,byte[] exe,Action<bool,string> check,Action<Action,string> reject)
    {
        RequireOwners(data,overlay,exe);
        byte[] scene=data.AsSpan(SceneAt,SceneBytes).ToArray();
        var windows=ObserveWindows(scene);
        check(windows.SequenceEqual(new[]{new Window(50,0xB3DC,0x11394,8),new Window(51,0xB434,0x1139C,8)}),
            "C2 evidence retains two actual row identities and observed windows, not allocation extents.");
        byte[] noOp=CopyObservedWindowNoOp(scene,null);
        check(!ReferenceEquals(noOp,scene)&&noOp.AsSpan().SequenceEqual(scene)&&Hash(noOp)==SceneSha,
            "T50-to-T51 observed-window copy is a whole-scene byte-identical no-op, not instantiation.");
        foreach(var w in windows)
            check(noOp.AsSpan(w.RowAt,0x58).SequenceEqual(scene.AsSpan(w.RowAt,0x58))&&
                Word(noOp,w.RowAt)==w.PropertiesAt&&noOp[w.RowAt+0x53]==0x54,
                "No-op preserves destination row, placement, properties pointer, ordinal and reward.");
        check(noOp.AsSpan(FixupsAt,4+201*4).SequenceEqual(scene.AsSpan(FixupsAt,4+201*4)),
            "No-op cannot append, reorder or replace native fixups.");
        noOp[0x1139C]=1;
        check(scene[0x1139C]==0&&data[SceneAt+0x1139C]==0,
            "The detached no-op result cannot mutate either original source owner.");
        reject(()=>CopyObservedWindowNoOp(scene,8),"unproven complete properties extent");
        reject(()=>CopyObservedWindowNoOp(scene,24),"unproven complete properties extent");

        // All corruptions below are detached in-memory fixtures. No mutated
        // source file, output image or native instance is ever constructed.
        BadData(b=>b[0]^=1,"complete Artisans data");
        BadOverlay(b=>b[^1]^=1,"complete Artisans overlay");
        BadExe(b=>b[^1]^=1,"complete original executable");
        reject(()=>RequireOwners(data[..^1],overlay,exe),"complete owner lengths");
        BadData(b=>b[SceneAt+0x11394]^=1,"observed zero window");
        BadData(b=>Put(b,SceneAt+RowsAt+52*0x58,0x11398),"row alias");
        BadData(b=>Put(b,SceneAt+FixupsAt,202),"fixup count");
        BadData(b=>Put(b,SceneAt+FixupsAt+8,Word(b,SceneAt+FixupsAt+4)),"fixup grammar");
        BadData(b=>Put(b,SceneAt+FixupsAt+4,SceneBytes),"fixup grammar");
        BadData(b=>{
            Put(b,SceneAt+FixupsAt+4+200*4,0x14700);
            Put(b,SceneAt+0x14700,0x11398);
        },"registered inbound alias");
        BadData(b=>Put(b,SceneAt+FixupsAt+4+200*4,0x11394),"internal fixup");
        int selectedFixup=Enumerable.Range(0,201).Single(i=>Word(scene,FixupsAt+4+i*4)==0xB3DC);
        BadData(b=>Put(b,SceneAt+FixupsAt+4+selectedFixup*4,0x14704),"selected row pointer registration");
        BadData(b=>b[SceneAt+0xB434+0x53]^=1,"source row/reward");
        BadData(b=>b[0x1AE664+4]^=1,"model/sound observation");
        BadData(b=>Put(b,0x50+9*4,0x1AE668),"model/sound observation");
        BadOverlay(b=>b[0x800829FC-0x8007AA38+4]^=1,"C2 update window");
        BadExe(b=>b[0x8005A734-0x8000F800]^=1,"Artisans callback binding");
        BadExe(b=>b[0x80038594-0x8000F800]^=1,"sound consumer");
        BadExe(b=>b[0x8003ABF4-0x8000F800]^=1,"reward consumer window");
        BadExe(b=>b[0x8003B7C0-0x8000F800]^=1,"retirement consumer");
        RequireOwners(data,overlay,exe);
        check(Hash(data)==DataSha&&Hash(overlay)==OverlaySha&&Hash(exe)==ExeSha&&Hash(scene)==SceneSha,
            "Every refusal preserves all complete original owners and their unrelated complement.");

        void BadData(Action<byte[]> mutate,string why){byte[] b=data.ToArray();mutate(b);reject(()=>RequireOwners(b,overlay,exe),why);}
        void BadOverlay(Action<byte[]> mutate,string why){byte[] b=overlay.ToArray();mutate(b);reject(()=>RequireOwners(data,b,exe),why);}
        void BadExe(Action<byte[]> mutate,string why){byte[] b=exe.ToArray();mutate(b);reject(()=>RequireOwners(data,overlay,b),why);}
    }

    private static void RequireOwners(byte[] data,byte[] overlay,byte[] exe)
    {
        Require(data.Length==0x383000&&overlay.Length==0xE000&&exe.Length==0x66000,"complete owner lengths");
        _=ObserveWindows(data.AsSpan(SceneAt,SceneBytes));
        foreach(var (slot,id,root) in new[]{(9,0xC2,0x1AE664),(27,0xFF,0x1C73F4),(28,0x100,0x1C7550),(29,0x101,0x1C7654)})
            Require(Word(data,0x50+slot*4)==root&&BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0x150+slot*2))==id,
                "model/sound observation");
        Require(data[0x1AE664+4]==0x41,"model/sound observation");
        Pin(overlay,(int)(0x8007DB94-0x8007AA38),0xC,"a04097b1b24ef96de5d2ddd89d8b12b4faddee3152d7201d519d14b8f6129383","C2 dispatch window");
        Pin(overlay,(int)(0x800829FC-0x8007AA38),0x160,"8db54c32b58e46715f89f990c1fabf219fe9cc5d6dd5ccefd826e07f2c0cab05","C2 update window");
        Pin(exe,(int)(0x8005A718-0x8000F800),0x98,"fe806d69534bcb66ae79423ff1c325fe7785c5c3857b4d92898df8b565494dfa","Artisans callback binding");
        Pin(exe,(int)(0x8003851C-0x8000F800),0xA0,"af902cc21e59f2794bc459d6cfaf528f36c28b73f39fc4221c1fe6d384331e91","sound consumer");
        Pin(exe,(int)(0x8003ABC0-0x8000F800),0x204,"ce0849697c1233433fd1b38b26bd2ee2ab65609bfdd59d4851130cf8a7c6f3e5","reward consumer window");
        Pin(exe,(int)(0x8003B7C0-0x8000F800),0x94,"a8378b5d829c8a0661affbb6e50eb64b606c3114e116a38ec1b7137ba8a30ec4","retirement consumer");
        Pin(data,0,data.Length,DataSha,"complete Artisans data");
        Pin(overlay,0,overlay.Length,OverlaySha,"complete Artisans overlay");
        Pin(exe,0,exe.Length,ExeSha,"complete original executable");
    }

    private static Window[] ObserveWindows(ReadOnlySpan<byte> scene)
    {
        Require(scene.Length==SceneBytes&&Word(scene,RowsAt-4)==174,"observed scene/row count");
        Require(Word(scene,FixupsAt)==201,"fixup count");
        HashSet<int> fields=[];
        for(int i=0;i<201;i++)
        {
            uint raw=Word(scene,FixupsAt+4+i*4);
            Require(raw<=SceneBytes-4&&raw%4==0&&fields.Add((int)raw),"fixup grammar");
        }
        Window[] windows=[new(50,0xB3DC,0x11394,8),new(51,0xB434,0x1139C,8)];
        foreach(var w in windows)
        {
            Require(Word(scene,w.RowAt)==w.PropertiesAt&&BinaryPrimitives.ReadUInt16LittleEndian(scene.Slice(w.RowAt+0x36,2))==0xC2&&
                scene[w.RowAt+0x53]==0x54,"source row/reward");
            Require(!scene.Slice(w.PropertiesAt,w.ObservedBytes).ContainsAnyExcept((byte)0),"observed zero window");
            Require(fields.Contains(w.RowAt),"selected row pointer registration");
            Require(!fields.Any(at=>at>=w.PropertiesAt&&at<w.PropertiesAt+w.ObservedBytes),"internal fixup");
            for(int i=0;i<174;i++)
            {
                uint pointer=Word(scene,RowsAt+i*0x58);
                Require(pointer<w.PropertiesAt||pointer>=w.PropertiesAt+w.ObservedBytes||i==w.Ordinal,"row alias");
            }
            foreach(int at in fields)
            {
                uint pointer=Word(scene,at);
                Require(pointer<w.PropertiesAt||pointer>=w.PropertiesAt+w.ObservedBytes||at==w.RowAt,"registered inbound alias");
            }
        }
        Pin(scene,0xB3DC,0x58,"f9590fda039363e5af9466630121efb876ce0a202226706d030f1ab503418988","T50 source row/reward");
        Pin(scene,0xB434,0x58,"5c6f987340c32d6af27024c0dd305b4d64a4cb0c8905e95d3509aebaf0e4c05b","T51 source row/reward");
        Pin(scene,FixupsAt,4+201*4,"7d677b0b8d6ad795a6319bd31e781c4e5f52a76be67ed81ed1b1c6fbdb4775fb","complete fixup owner");
        Pin(scene,0,scene.Length,SceneSha,"complete Scene owner");
        return windows;
    }

    private static byte[] CopyObservedWindowNoOp(byte[] scene,int? claimedCompletePropertiesBytes)
    {
        Require(claimedCompletePropertiesBytes is null,"unproven complete properties extent");
        var windows=ObserveWindows(scene);byte[] result=scene.ToArray();
        scene.AsSpan(windows[0].PropertiesAt,windows[0].ObservedBytes).CopyTo(result.AsSpan(windows[1].PropertiesAt,windows[1].ObservedBytes));
        Require(result.AsSpan().SequenceEqual(scene),"observed-window control must be a byte-identical no-op");
        return result;
    }

    private static void Pin(ReadOnlySpan<byte> bytes,int at,int length,string sha,string why)=>Require(Hash(bytes.Slice(at,length))==sha,why);
    private static uint Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(at,4));
    private static void Put(byte[] bytes,int at,int value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at,4),(uint)value);
    private static void Put(byte[] bytes,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at,4),value);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool ok,string reason){if(!ok)throw new InvalidDataException("C2 evidence-only refusal: "+reason);}
}

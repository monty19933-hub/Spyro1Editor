using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeTownSquareLockedChestPublication(string Directory,string Image,string Cue,string Receipt,
    string ImageSha256,string GraphSha256,int ChangedRawSectors);
internal enum NativeTownSquareClosureFault { None, AfterCopy, AfterWrite, BeforePublish }

/// <summary>Immutable research artifacts only. No editor registry, project persistence or runtime promotion.</summary>
internal static class NativeTownSquareLockedChestClosurePublisher
{
    private const string BinName="town-square-native-closure-v2.bin", CueName="town-square-native-closure-v2.cue", ReceiptName="native-closure.receipt.json";
    internal static NativeTownSquareLockedChestPublication VerifyPublished(string sourcePath,string sourceCue,string outputDirectory)
    {
        if(!DiscCueSheet.ReferencesDataImage(sourceCue,sourcePath))throw new InvalidDataException("Native closure requires a real source-bound CUE.");
        NativeTownSquareLockedChestClosure closure=NativeTownSquareLockedChestClosureCompiler.Capture(sourcePath);
        outputDirectory=Path.GetFullPath(outputDirectory);
        if(!Directory.Exists(outputDirectory)||!Directory.EnumerateFiles(outputDirectory).Select(Path.GetFileName).Order(StringComparer.Ordinal)
            .SequenceEqual(new[]{BinName,CueName,ReceiptName}.Order(StringComparer.Ordinal))||Directory.EnumerateDirectories(outputDirectory).Any())
            throw new InvalidDataException("Native closure published artifact set is missing, partial or occupied by other data.");
        using FileStream source=new(sourcePath,FileMode.Open,FileAccess.Read,FileShare.Read);
        var verified=PhysicalPlan.Create(source,closure).Verify(source,Path.Combine(outputDirectory,BinName),closure);
        byte[] expectedCue=DiscCueSheet.TextEncoding.GetBytes(DiscCueSheet.BuildForImage(sourceCue,sourcePath,BinName));
        NativeTownSquareClosurePhysicalReadback.RequireCompanions(File.ReadAllBytes(Path.Combine(outputDirectory,CueName)),expectedCue,
            File.ReadAllBytes(Path.Combine(outputDirectory,ReceiptName)),BuildReceipt(closure,verified));
        if(!DiscCueSheet.ReferencesDataImage(Path.Combine(outputDirectory,CueName),Path.Combine(outputDirectory,BinName)))
            throw new InvalidDataException("Published native closure CUE references another data image.");
        return new(outputDirectory,Path.Combine(outputDirectory,BinName),Path.Combine(outputDirectory,CueName),Path.Combine(outputDirectory,ReceiptName),verified.Hash,closure.Graph.Fingerprint,verified.Changed);
    }
    internal static NativeTownSquareLockedChestPublication Publish(string sourcePath,string sourceCue,string outputDirectory,
        NativeTownSquareClosureFault fault=NativeTownSquareClosureFault.None)
    {
        if(!Enum.IsDefined(fault))throw new InvalidDataException("Unknown native closure publication fault.");
        outputDirectory=Path.GetFullPath(outputDirectory);
        PersistencePathSafety.EnsureTreesDoNotOverlap(sourcePath,outputDirectory,"Native closure output must not overlap its source.");
        PersistencePathSafety.EnsureTreesDoNotOverlap(sourceCue,outputDirectory,"Native closure output must not overlap its source CUE.");
        if(File.Exists(outputDirectory)||Directory.Exists(outputDirectory)&&Directory.EnumerateFileSystemEntries(outputDirectory).Any())
            throw new IOException("Native closure refuses to replace an existing or partial candidate.");
        NativeTownSquareLockedChestClosure closure=NativeTownSquareLockedChestClosureCompiler.Capture(sourcePath);
        byte[] cue=DiscCueSheet.TextEncoding.GetBytes(DiscCueSheet.BuildForImage(sourceCue,sourcePath,BinName));
        if(!DiscCueSheet.ReferencesDataImage(sourceCue,sourcePath))throw new InvalidDataException("Native closure requires a real source-bound CUE.");
        if(Directory.Exists(outputDirectory)&&Directory.EnumerateFileSystemEntries(outputDirectory).Any())
            throw new IOException("Native closure refuses to replace an existing or partial candidate.");
        Directory.CreateDirectory(outputDirectory);
        string stage=Path.Combine(outputDirectory,".native-closure-stage-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        string image=Path.Combine(stage,BinName);bool published=false;
        try
        {
            using FileStream source=new(sourcePath,FileMode.Open,FileAccess.Read,FileShare.Read);
            PhysicalPlan physical=PhysicalPlan.Create(source,closure);
            using(var output=new FileStream(image,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None))
            {
                source.Position=0;source.CopyTo(output);Inject(NativeTownSquareClosureFault.AfterCopy);
                physical.Preparation.ApplyToExactCopy(output);
                DiscImage.WriteFileBytes(output,closure.Layout,37,0,physical.Wad);
                DiscImage.WriteFileBytes(output,closure.Layout,53880,0,closure.Executable);
                DiscImage.WriteFileBytes(output,closure.Layout,closure.Layout.RootExtent,0,physical.RootDirectory);
                output.Flush(flushToDisk:true);
            }
            Inject(NativeTownSquareClosureFault.AfterWrite);
            var verified=physical.Verify(source,image,closure);
            var fresh=NativeTownSquareLockedChestClosureCompiler.Capture(sourcePath);
            if(fresh.Graph.Fingerprint!=closure.Graph.Fingerprint||!DiscCueSheet.ReferencesDataImage(sourceCue,sourcePath)||
                !DiscCueSheet.TextEncoding.GetBytes(DiscCueSheet.BuildForImage(sourceCue,sourcePath,BinName)).SequenceEqual(cue))
                throw new InvalidDataException("Native closure source or CUE changed before publication.");
            byte[] receipt=BuildReceipt(closure,verified);
            WriteNew(Path.Combine(stage,ReceiptName),receipt);WriteNew(Path.Combine(stage,CueName),cue);
            NativeTownSquareClosurePhysicalReadback.RequireCompanions(File.ReadAllBytes(Path.Combine(stage,CueName)),cue,
                File.ReadAllBytes(Path.Combine(stage,ReceiptName)),receipt);
            Inject(NativeTownSquareClosureFault.BeforePublish);
            ExportArtifactTransaction.PublishNew(outputDirectory,new[]{BinName,ReceiptName,CueName}.Select(n=>
                new ExportArtifactReplacement(Path.Combine(outputDirectory,n),Path.Combine(stage,n))).ToArray());
            published=true;
            var final=physical.Verify(source,Path.Combine(outputDirectory,BinName),closure);
            NativeTownSquareClosurePhysicalReadback.RequireCompanions(File.ReadAllBytes(Path.Combine(outputDirectory,CueName)),cue,
                File.ReadAllBytes(Path.Combine(outputDirectory,ReceiptName)),receipt);
            if(final!=verified||!DiscCueSheet.ReferencesDataImage(Path.Combine(outputDirectory,CueName),Path.Combine(outputDirectory,BinName)))
                throw new InvalidDataException("Published native closure changed or has an invalid CUE.");
            return new(outputDirectory,Path.Combine(outputDirectory,BinName),Path.Combine(outputDirectory,CueName),Path.Combine(outputDirectory,ReceiptName),verified.Hash,closure.Graph.Fingerprint,verified.Changed);
        }
        catch(Exception error) when(published){throw new IOException("Native closure was published but final verification failed; preserve "+outputDirectory,error);}
        finally
        {
            // Only this publisher's explicit temporary directory; never remove artifacts or recovery journals.
            if(!Directory.EnumerateDirectories(outputDirectory,".export-recovery-*").Any()&&Directory.Exists(stage))
            {foreach(string file in Directory.EnumerateFiles(stage))File.Delete(file);Directory.Delete(stage);}
        }
        void Inject(NativeTownSquareClosureFault point){if(fault==point)throw new IOException("Deliberate native closure publication fault: "+point);}
    }

    private static byte[] BuildReceipt(NativeTownSquareLockedChestClosure closure,(string Hash,int Changed) verified)
    {
        return JsonSerializer.SerializeToUtf8Bytes(new {
                profile=NativeTownSquareLockedChestClosureCompiler.Profile,sourceImageSha256=NativeTownSquareLockedChestClosureCompiler.SourceSha256,
                sourceImagePath=Path.GetFullPath(closure.SourcePath),graph=closure.Graph,outputImageSha256=verified.Hash,changedRawSectors=verified.Changed,
                finalDataSha256=Hash(closure.Data),finalOverlaySha256=Hash(closure.Overlay),finalExecutableSha256=Hash(closure.Executable),
                oldRecipeDataSha256=closure.LegacyDataSha256,oldRecipeUnchanged=true,
                graphDrivesExtractionInstallsReferences=true,sourceFactoryOnly=true,wholeImagePhysicalReadback=true,
                sourceUnchanged=true,unrelatedWadEntriesByteIdentical=true,propertiesComponentOffset=0x4E38,propertiesBytesBefore=0x10D4,propertiesBytesAfter=0x11B8,
                propertiesAppendOffset=0x5F0C,propertiesAppendBytes=0xE4,sceneFixupOffset=0x800C,fixupsBefore=129,fixupsAfter=136,
                nativeModelSoundDescriptorBefore=47,nativeModelSoundDescriptorAfter=54,modelSoundSampleSha256="f10bef3c98281716bf79cb09c77ff6017c2959d10f11ba4f0b9cda1868c977c3",
                nativeKeyTrueIndex=107,nativeChestTrueIndex=108,rewardTrueIndices=new[]{109,110,111,112,113},
                keyXyz=new[]{130150,111032,11264},chestXyz=new[]{132198,111032,11264},
                navigation="Inventory: R1 R2 L1 L2 R1 L1 R2 L2, then Cross Triangle (Town Square 13).",
                gameplayRequired=new[]{"cold boot and normal Town Square reachability","native key visible and collected with native particles/sound",
                    "chest remains locked before key","native chest opens after key with correct animation, debris, particles and sound",
                    "all native reward gems collect and treasure total reaches expected 210","death/reload and repeat entry preserve gating",
                    "nearby native enemies, camera, collision and exit continue to work"},
                runtimeProven=false,normalCreateBinEnabled=false,appIntegrated=false,exceptionRollbackPublication=true,crashAtomicPublication=false
            },new JsonSerializerOptions{WriteIndented=true});
    }

    private sealed class PhysicalPlan
    {
        internal required byte[] Wad {get;init;}
        internal required byte[] RootDirectory {get;init;}
        internal required NativeWadGrowthSectorPreparation Preparation {get;init;}
        internal required byte WadBoundaryFlags {get;init;}
        internal required byte ExeBoundaryFlags {get;init;}
        internal static PhysicalPlan Create(FileStream source,NativeTownSquareLockedChestClosure closure)
        {
            var layout=closure.Layout;byte[] header=closure.OriginalWadHeader.ToArray(),wad=new byte[0x6929800];
            List<(int Id,int Offset,int Length)> entries=[];
            for(int i=0;i<256;i++)
            {
                uint offset=U32(header,i*8),length=U32(header,i*8+4);
                if(offset==0&&length==0)continue;
                if(offset<2048||length==0||offset%2048!=0||length%2048!=0||offset+(long)length>0x6927000)
                    throw new InvalidDataException("Native WAD has an unsupported occupied directory entry.");
                entries.Add((i,(int)offset,(int)length));
            }
            Dictionary<int,long> remaps=[];int oldCursor=2048,newCursor=2048;
            foreach(var entry in entries.OrderBy(e=>e.Offset))
            {
                if(entry.Offset!=oldCursor)throw new InvalidDataException("Native WAD entries are not fully packed.");
                byte[] bytes=entry.Id==15?closure.Overlay:entry.Id==16?closure.Data:DiscImage.ReadFileBytes(source,layout,37,entry.Offset,entry.Length);
                remaps.Add(entry.Id,newCursor);Put(header,entry.Id*8,(uint)newCursor);Put(header,entry.Id*8+4,(uint)bytes.Length);
                bytes.CopyTo(wad,newCursor);oldCursor+=entry.Length;newCursor+=bytes.Length;
            }
            if(oldCursor!=0x6927000||newCursor!=wad.Length)throw new InvalidDataException("Native WAD allocation does not balance.");
            header.CopyTo(wad,0);
            NativeSkyWadRelocationPlan allocation=new(true,37,0x6927000,0x6929800,0x2800,53875,53880,0x66000,60000,
                (60000-(53875+0x66000/2048))*2048,new Dictionary<int,int>{{15,0x1000},{16,0x1800}},remaps,[]);
            NativeWadGrowthSectorPreparation preparation=NativeWadGrowthSectorPreparation.Create(source,layout,allocation);
            byte[] root=DiscImage.ReadFileBytes(source,layout,layout.RootExtent,0,layout.RootLength);
            var files=NativeWadGrowthSectorPreparation.ReadFlatRootFiles(source,layout);
            foreach(var item in new[]{("WAD.WAD",37,0x6929800),("SCUS_942.28",53880,0x66000)})
            {
                var file=files.Single(f=>f.Name.Equals(item.Item1,StringComparison.OrdinalIgnoreCase));
                Both(root,file.RecordOffset+2,(uint)item.Item2);Both(root,file.RecordOffset+10,(uint)item.Item3);
            }
            byte Flags(int lba){source.Position=(long)lba*2352+18;return (byte)(source.ReadByte()&0x81);}
            return new(){Wad=wad,RootDirectory=root,Preparation=preparation,WadBoundaryFlags=Flags(53874),ExeBoundaryFlags=Flags(54078)};
        }
        internal (string Hash,int Changed) Verify(FileStream source,string outputPath,NativeTownSquareLockedChestClosure closure)
        {
            using FileStream output=File.OpenRead(outputPath);
            return NativeTownSquareClosurePhysicalReadback.Verify(source,output,661547040,
                NativeTownSquareLockedChestClosureCompiler.SourceSha256,(lba,before)=>
            {
                byte[] expected=before.ToArray();bool touched=false;
                if(lba>=37&&lba<53880)
                {Wad.AsSpan((lba-37)*2048,2048).CopyTo(expected.AsSpan(24,2048));touched=true;}
                else if(lba>=53880&&lba<54084)
                {
                    if(lba>=54079)expected=RawMode2Form1SectorIntegrity.PrepareOwnedZeroPaddingSector(before,closure.Layout,lba);
                    closure.Executable.AsSpan((lba-53880)*2048,2048).CopyTo(expected.AsSpan(24,2048));touched=true;
                }
                if(lba>=closure.Layout.RootExtent&&lba<closure.Layout.RootExtent+(RootDirectory.Length+2047)/2048)
                {int o=(lba-closure.Layout.RootExtent)*2048;RootDirectory.AsSpan(o,Math.Min(2048,RootDirectory.Length-o)).CopyTo(expected.AsSpan(24));touched=true;}
                if(lba is 53874 or 54078 or 53879 or 54083)
                {byte flags=lba==53879?WadBoundaryFlags:lba==54083?ExeBoundaryFlags:(byte)0;expected[18]=expected[22]=(byte)((expected[18]&~0x81)|flags);touched=true;}
                if(touched&&!expected.SequenceEqual(before))expected=RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(expected,lba);
                return expected;
            });
        }
    }
    private static void WriteNew(string path,byte[] bytes){using FileStream f=new(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);f.Write(bytes);f.Flush(true);}
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static uint U32(byte[] bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset,4));
    private static void Put(byte[] bytes,int offset,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset,4),value);
    private static void Both(byte[] bytes,int offset,uint value){Put(bytes,offset,value);BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset+4,4),value);}
}

/// <summary>Pure streaming comparator; destination/source factories, never authored data, supply expected raw bytes.</summary>
internal static class NativeTownSquareClosurePhysicalReadback
{
    internal static (string Hash,int Changed) Verify(Stream source,Stream output,long expectedLength,string expectedSourceSha256,
        Func<int,byte[],byte[]> expectedSector)
    {
        if(!source.CanRead||!source.CanSeek||!output.CanRead||!output.CanSeek||expectedLength<=0||expectedLength%2352!=0||
            expectedLength/2352>int.MaxValue||source.Length!=expectedLength||output.Length!=expectedLength)
            throw new InvalidDataException("Native closure image length or readable stream changed.");
        using IncrementalHash sourceHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256),outputHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] before=new byte[2352],after=new byte[2352];int changed=0;source.Position=output.Position=0;
        for(int lba=0;lba<expectedLength/2352;lba++)
        {
            source.ReadExactly(before);output.ReadExactly(after);sourceHash.AppendData(before);outputHash.AppendData(after);
            byte[] expected=expectedSector(lba,before.ToArray());
            if(expected==null||expected.Length!=2352)throw new InvalidDataException("Native closure expected raw-sector length changed.");
            if(!expected.SequenceEqual(after))throw new InvalidDataException($"Whole native closure physical readback differs at raw sector {lba}.");
            if(!before.SequenceEqual(after))changed++;
        }
        if(source.Length!=expectedLength||output.Length!=expectedLength||source.Position!=expectedLength||output.Position!=expectedLength)
            throw new InvalidDataException("Native closure image length or position changed during physical readback.");
        if(Convert.ToHexStringLower(sourceHash.GetHashAndReset())!=expectedSourceSha256)
            throw new InvalidDataException("Native closure source image identity changed during physical readback.");
        return(Convert.ToHexStringLower(outputHash.GetHashAndReset()),changed);
    }
    internal static void RequireCompanions(ReadOnlySpan<byte> actualCue,ReadOnlySpan<byte> expectedCue,
        ReadOnlySpan<byte> actualReceipt,ReadOnlySpan<byte> expectedReceipt)
    {
        if(!actualCue.SequenceEqual(expectedCue))throw new InvalidDataException("Native closure CUE failed exact companion readback.");
        if(!actualReceipt.SequenceEqual(expectedReceipt))throw new InvalidDataException("Native closure receipt failed exact companion readback.");
    }
}

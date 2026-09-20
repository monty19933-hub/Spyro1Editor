using System.Buffers.Binary;

namespace Spyro.Editor.Core.Exporting;

internal sealed partial class NativeLockedChestFamilySource
{
    // The existing complete-owner decoder remains the only byte decoder. This
    // named acquisition supplies fixed Original owners, never caller domains,
    // readers or source hashes. It grants no placement/runtime/export approval.
    internal static async Task<NativeLockedChestFamilySource> CaptureOriginalAsync(string image,
        NativeWizardFamilyAssetPlan actualWizard,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(actualWizard);
        ArgumentException.ThrowIfNullOrWhiteSpace(image);string path=Path.GetFullPath(image);
        var executable=RequireWizardOriginalDomain(actualWizard);
        FileStream? held=null;NativeLockedChestFamilySource? result=null;List<Exception> failures=[];
        try
        {
            held=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(held,token);
            var layout=DiscImage.DetectLayout(held);
            var wad=DiscImage.FindRootFileRecord(held,layout,n=>n.Equals("WAD.WAD",StringComparison.OrdinalIgnoreCase));
            var exe=DiscImage.FindRootFileRecord(held,layout,n=>n.Equals("SCUS_942.28",StringComparison.OrdinalIgnoreCase));
            if(layout!=new DiscLayout(2352,24,22,2048)||wad.Lba!=37||wad.Size!=0x6927000||
                exe.Lba!=53875||exe.Size!=0x66000)
                throw new InvalidDataException("Locked-chest capture requires the complete Original raw ISO/WAD/executable owners.");
            byte[] directory=Read(wad.Lba,0,0x800);
            if(Hash(directory)!="15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd")
                throw new InvalidDataException("Complete locked-chest Original WAD directory changed.");
            (int Id,int Offset,int Length)[] entries=[(22,0x1890800,0x29E800),(21,0x187E000,0x12800),
                (16,0x119E000,0x2E2000),(15,0x118E800,0xF800)];
            // Retain historical labels for existing graph/plan identities. The
            // label is not the authority: each domain is paired with a complete
            // authenticated owner and the EXE is the actual Wizard's instance.
            var domains=entries.Select(e=>new NativeAddressSpace(NativeAddressKind.LevelData,
                "sound-smoke-original-wad-"+e.Id,NativeAddressRevision.Original,e.Length)).Append(executable).ToArray();
            Dictionary<NativeAddressSpace,byte[]> owners=[];
            for(int i=0;i<entries.Length;i++)
            {
                var entry=entries[i];
                if(BinaryPrimitives.ReadInt32LittleEndian(directory.AsSpan(entry.Id*8))!=entry.Offset||
                    BinaryPrimitives.ReadInt32LittleEndian(directory.AsSpan(entry.Id*8+4))!=entry.Length)
                    throw new InvalidDataException("Locked-chest capture lost an exact Original source WAD entry.");
                owners.Add(domains[i],Read(wad.Lba,entry.Offset,entry.Length));
            }
            byte[] originalExe=Read(exe.Lba,0,exe.Size);
            if(!originalExe.AsSpan().SequenceEqual(actualWizard.ReadPrivateSource(new(executable,0,executable.ByteLength))))
                throw new InvalidDataException("Locked-chest capture and the actual Wizard have different complete Original executable bytes.");
            owners.Add(executable,originalExe);
            result=DecodeOriginal(domains[0],domains[1],domains[2],domains[3],domains[4],extent=>
            {
                token.ThrowIfCancellationRequested();
                if(!owners.TryGetValue(extent.Space,out var bytes))
                    throw new InvalidDataException("Locked-chest acquisition read escaped its actual complete Original owners.");
                byte[] copy=bytes.AsSpan(checked((int)extent.Offset),checked((int)extent.ByteLength)).ToArray();
                token.ThrowIfCancellationRequested();return copy;
            });
            result.RequireWizardOriginalOwner(actualWizard);token.ThrowIfCancellationRequested();

            byte[] Read(int lba,long offset,int length)
            {
                token.ThrowIfCancellationRequested();
                byte[] bytes=DiscImage.ReadFileBytes(held,layout,lba,offset,length);
                if(bytes.Length!=length)throw new InvalidDataException("Locked-chest Original source read is incomplete.");
                token.ThrowIfCancellationRequested();return bytes;
            }
        }
        catch(Exception error){failures.Add(error);}
        finally
        {
            // Match the existing concrete family-source finalization: attempt
            // every acquired and named observation independently, using None,
            // retaining primary/readback/close failures in their original order.
            if(held is not null)await Observe(()=>NativeId65FreshDiscSource.RequireWholeOriginalAsync(held,CancellationToken.None));
            FileStream? current=null;
            try
            {
                current=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
                await NativeId65FreshDiscSource.RequireWholeOriginalAsync(current,CancellationToken.None);
            }
            catch(Exception error){failures.Add(error);}
            finally{if(current is not null)Attempt(current.Dispose);}
            Attempt(()=>RequireWizardOriginalDomain(actualWizard));
            if(result is not null)Attempt(()=>result.RequireWizardOriginalOwner(actualWizard));
            if(held is not null)Attempt(held.Dispose);
        }
        NativeFreshResearchSourceOwners.ThrowFailures(failures);token.ThrowIfCancellationRequested();
        return result??throw new InvalidDataException("Locked-chest Original capture returned no result.");
        async Task Observe(Func<Task> action){try{await action();}catch(Exception error){failures.Add(error);}}
        void Attempt(Action action){try{action();}catch(Exception error){failures.Add(error);}}
    }

    internal void RequireWizardOriginalOwner(NativeWizardFamilyAssetPlan actualWizard)
    {
        ArgumentNullException.ThrowIfNull(actualWizard);
        var executable=RequireWizardOriginalDomain(actualWizard);
        if(!ReferenceEquals(_domains[4],executable))
            throw new InvalidDataException("Locked-chest source requires the actual Wizard Original executable domain, not an equal foreign owner.");
        RequireRetainedOwners();
        var extent=new NativeByteExtent(executable,0,executable.ByteLength);
        if(!ReadSource(extent).AsSpan().SequenceEqual(actualWizard.ReadPrivateSource(extent)))
            throw new InvalidDataException("Locked-chest and Wizard complete retained Original executable bytes disagree.");
    }

    private static NativeAddressSpace RequireWizardOriginalDomain(NativeWizardFamilyAssetPlan actualWizard)
    {
        ArgumentNullException.ThrowIfNull(actualWizard);
        if(actualWizard.SourceImageSha256!=NativeId65FreshDiscSource.OriginalSha256||
            actualWizard.DonorLevelId!=33||actualWizard.DonorTrueIndex!=6)
            throw new InvalidDataException("Locked-chest capture requires the actual original Wizard33/T6 source.");
        _=actualWizard.CapturePrivateStorageRows();
        var executable=actualWizard.OriginalExecutableDomain;
        if(executable.Kind!=NativeAddressKind.ExecutableFile||executable.Revision!=NativeAddressRevision.Original||
            executable.ByteLength!=OwnerLengths[4])
            throw new InvalidDataException("Actual Wizard Original executable domain changed.");
        byte[] bytes=actualWizard.ReadPrivateSource(new(executable,0,executable.ByteLength));
        if(bytes.Length!=OwnerLengths[4]||Hash(bytes)!=OwnerHashes[4])
            throw new InvalidDataException("Complete actual Wizard Original executable identity changed.");
        return executable;
    }
}

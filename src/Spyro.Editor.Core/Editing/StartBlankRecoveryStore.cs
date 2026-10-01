using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Editing;

public sealed record StartBlankRecoveryAsset(string Path, bool ProjectRelative, string SnapshotName, string Sha256);
public sealed record StartBlankRecoveryFile(string Name, string Sha256);
public sealed record StartBlankRecovery(int SchemaVersion, string LevelKey, string SnapshotId,
    string? BeforeSceneSha256, string ClearedSceneId, bool Active, IReadOnlyList<StartBlankRecoveryFile> PreservedFiles, IReadOnlyList<StartBlankRecoveryAsset>? Assets = null);

/// <summary>Durable per-level recovery. Original edit owners are kept in place; only the
/// authored replacement document is switched. Snapshots contain verified saved drafts.</summary>
public static class StartBlankRecoveryStore
{
    private static string Key(string key)
    {
        _ = SceneDocumentStore.PathForTarget(".", key);
        return key;
    }
    public static string PointerPath(string root,string key) => Path.Combine(root,Key(key)+"-start-blank-recovery.json");
    private static string SnapshotPath(string root,string key,string id)
    {
        if(!Guid.TryParseExact(id,"N",out _)) throw new InvalidDataException("Invalid recovery snapshot identity.");
        return Path.Combine(root,"level-recovery",Key(key),id);
    }
    public static StartBlankRecovery? Read(string root,string key)
    {
        string path=PointerPath(root,key);
        if(!File.Exists(path))return null;
        var value=JsonSerializer.Deserialize<StartBlankRecovery>(File.ReadAllBytes(path))??throw new InvalidDataException("Empty recovery record.");
        if(value.SchemaVersion!=1||value.LevelKey!=key||!Guid.TryParseExact(value.ClearedSceneId,"N",out _))
            throw new InvalidDataException("Recovery record belongs to another level.");
        _=SnapshotPath(root,key,value.SnapshotId);return value;
    }
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static IEnumerable<string> RelatedFiles(string root,string key)=>Directory.EnumerateFiles(root,key+"-*.json")
        .Where(p=>p!=PointerPath(root,key)&&p!=SceneDocumentStore.PathForTarget(root,key)&&p!=UniversalNativeLayoutStore.PathFor(root,key)&&!p.EndsWith("-cleared-lifetime-ledger.json",StringComparison.Ordinal));

    public static async Task<StartBlankRecovery> ClearAsync(string root,string key,SceneDocument blank,
        Action? requireCurrent=null,Action<string>? snapshotFaultForTesting=null)
    {
        blank.Validate();
        if((blank.TargetBinding?.LevelKey??"authored65")!=key||blank.Objects.Count!=0)
            throw new InvalidDataException("Start Blank requires a matching neutral document with no objects.");
        string scenePath=SceneDocumentStore.PathForTarget(root,key),id=Guid.NewGuid().ToString("N");
        string snapshot=SnapshotPath(root,key,id);RequireRecoveryDirectories(root,key);Directory.CreateDirectory(snapshot);
        byte[]? before=File.Exists(scenePath)?await File.ReadAllBytesAsync(scenePath):null;
        if(before!=null)
        {
            _=SceneDocument.Read(scenePath);
            await File.WriteAllBytesAsync(Path.Combine(snapshot,"before.scene.json"),before);
            if(!File.ReadAllBytes(Path.Combine(snapshot,"before.scene.json")).AsSpan().SequenceEqual(before))throw new IOException("Recovery scene verification failed.");
        }
        List<StartBlankRecoveryFile> files=[];
        foreach(string path in RelatedFiles(root,key))
        {
            byte[] bytes=await File.ReadAllBytesAsync(path);string name=Path.GetFileName(path);
            await File.WriteAllBytesAsync(Path.Combine(snapshot,name),bytes);
            if(!File.ReadAllBytes(Path.Combine(snapshot,name)).AsSpan().SequenceEqual(bytes)||Hash(File.ReadAllBytes(path))!=Hash(bytes))
                throw new IOException("Recovery saved edit verification failed: "+name);
            files.Add(new(name,Hash(bytes)));
        }
        List<StartBlankRecoveryAsset> assets=[];
        foreach(string path in ReferencedImages(root,key))
        {
            byte[] bytes=await File.ReadAllBytesAsync(path);string snapshotName="asset-"+Guid.NewGuid().ToString("N")+Path.GetExtension(path);
            await File.WriteAllBytesAsync(Path.Combine(snapshot,snapshotName),bytes);
            bool relative=path.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.Ordinal);
            assets.Add(new(relative?Path.GetRelativePath(root,path):path,relative,snapshotName,Hash(bytes)));
        }
        var record=new StartBlankRecovery(1,key,id,before==null?null:Hash(before),blank.Id,true,files,assets);
        byte[] recordBytes=JsonSerializer.SerializeToUtf8Bytes(record,new JsonSerializerOptions{WriteIndented=true});
        await File.WriteAllBytesAsync(Path.Combine(snapshot,"snapshot.json"),recordBytes);
        snapshotFaultForTesting?.Invoke(snapshot);
        RequireSnapshot(root,record);
        if(!FileBytesEqual(scenePath,before))throw new InvalidDataException("The saved scene changed while the recovery snapshot was made.");
        requireCurrent?.Invoke();
        RequireSavedOwners(root,record);
        string stagedScene=Path.Combine(snapshot,"new.scene.json"),stagedRecord=Path.Combine(snapshot,"new-record.json");
        await File.WriteAllBytesAsync(stagedScene,blank.Serialize());await File.WriteAllBytesAsync(stagedRecord,recordBytes);
        requireCurrent?.Invoke();RequireSavedOwners(root,record);
        if(!FileBytesEqual(scenePath,before))throw new InvalidDataException("The saved scene changed before clear could commit.");
        _=ExportArtifactTransaction.Publish(root,[new(scenePath,stagedScene),new(PointerPath(root,key),stagedRecord)]);
        return record;
    }
    public static void RequireSnapshot(string root,StartBlankRecovery record)
    {
        string snapshot=SnapshotPath(root,record.LevelKey,record.SnapshotId);
        var issued=JsonSerializer.Deserialize<StartBlankRecovery>(File.ReadAllBytes(Path.Combine(snapshot,"snapshot.json")));
        if(issued==null||issued.LevelKey!=record.LevelKey||issued.SnapshotId!=record.SnapshotId||issued.ClearedSceneId!=record.ClearedSceneId||
            issued.BeforeSceneSha256!=record.BeforeSceneSha256||!issued.PreservedFiles.SequenceEqual(record.PreservedFiles)||!(issued.Assets??[]).SequenceEqual(record.Assets??[]))
            throw new InvalidDataException("The recovery manifest does not match its verified snapshot.");
        if(record.BeforeSceneSha256 is {} hash && Hash(File.ReadAllBytes(Path.Combine(snapshot,"before.scene.json")))!=hash)
            throw new InvalidDataException("The previous scene recovery bytes changed.");
        foreach(var asset in record.Assets??[])
            if(Path.GetFileName(asset.SnapshotName)!=asset.SnapshotName||!asset.SnapshotName.StartsWith("asset-",StringComparison.Ordinal)||
                Hash(File.ReadAllBytes(Path.Combine(snapshot,asset.SnapshotName)))!=asset.Sha256)
                throw new InvalidDataException("A recovery texture image changed.");
        foreach(var file in record.PreservedFiles)
        {
            if(Path.GetFileName(file.Name)!=file.Name||!file.Name.StartsWith(record.LevelKey+"-",StringComparison.Ordinal)||
                Hash(File.ReadAllBytes(Path.Combine(snapshot,file.Name)))!=file.Sha256)
                throw new InvalidDataException("A recovery saved edit changed: "+file.Name);
        }
    }
    public static async Task RestoreAsync(string root,string key,Action? requireCurrent=null)
    {
        var record=Read(root,key)??throw new InvalidDataException("No before-clear snapshot is available for this level.");
        if(!record.Active)throw new InvalidDataException("The before-clear snapshot has already been restored.");
        RequireSnapshot(root,record);
        RequireSavedOwners(root,record);
        string path=SceneDocumentStore.PathForTarget(root,key),snapshot=SnapshotPath(root,key,record.SnapshotId);
        var current=SceneDocument.Read(path);
        if(current.Id!=record.ClearedSceneId)throw new InvalidDataException("A different scene now occupies this level; restore stopped.");
        UniversalNativeLayoutStore.Validate(root,key,current.TargetBinding?.NativePrefixRows??0);
        SaveLifetime(root,key,current.Ledger,current.TargetBinding?.NativePrefixRows??0);
        string after=Path.Combine(snapshot,"after-"+Guid.NewGuid().ToString("N")+".scene.json");
        byte[] currentBytes=File.ReadAllBytes(path);await File.WriteAllBytesAsync(after,currentBytes);
        if(!File.ReadAllBytes(after).AsSpan().SequenceEqual(currentBytes))throw new IOException("Current scene recovery verification failed.");
        requireCurrent?.Invoke();
        if(!FileBytesEqual(path,currentBytes))throw new InvalidDataException("The scene changed during recovery.");
        RequireSavedOwners(root,record);
        string stagedRecord=Path.Combine(snapshot,"restored-record-"+Guid.NewGuid().ToString("N")+".json");
        await File.WriteAllBytesAsync(stagedRecord,JsonSerializer.SerializeToUtf8Bytes(record with{Active=false}));
        if(record.BeforeSceneSha256!=null)
        {
            string stagedScene=Path.Combine(snapshot,"restore-"+Guid.NewGuid().ToString("N")+".scene.json");
            var before=SceneDocument.Read(Path.Combine(snapshot,"before.scene.json"));
            before=before with{Ledger=ReconcileLifetime(root,key,before.Ledger,before.TargetBinding?.NativePrefixRows??0)};
            await File.WriteAllBytesAsync(stagedScene,before.Serialize());
            RequireCommit();
            _=ExportArtifactTransaction.Publish(root,[new(path,stagedScene),new(PointerPath(root,key),stagedRecord)]);
        }
        else
        {
            // The saved after-scene stays in recovery; paired publication removes only the active override.
            RequireCommit();
            _=ExportArtifactTransaction.Publish(root,[new(path,null),new(PointerPath(root,key),stagedRecord)]);
        }
        void RequireCommit()
        {
            requireCurrent?.Invoke();RequireSavedOwners(root,record);
            if(!FileBytesEqual(path,currentBytes))throw new InvalidDataException("The scene changed before recovery could commit.");
            UniversalNativeLayoutStore.Validate(root,key,current.TargetBinding?.NativePrefixRows??0);
        }
    }
    private static void RequireSavedOwners(string root,StartBlankRecovery record)
    {
        var actual=RelatedFiles(root,record.LevelKey).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        if(!actual.SequenceEqual(record.PreservedFiles.Select(f=>f.Name).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("The level's saved edit files changed while recovery was prepared.");
        foreach(var file in record.PreservedFiles)
            if(Hash(File.ReadAllBytes(Path.Combine(root,file.Name)))!=file.Sha256)
                throw new InvalidDataException("Original saved edits changed; recovery stopped to preserve them: "+file.Name);
        var paths=ReferencedImages(root,record.LevelKey).ToHashSet(StringComparer.Ordinal);
        if(!paths.SetEquals((record.Assets??[]).Select(a=>AssetPath(root,a))))
            throw new InvalidDataException("The level's referenced texture images changed during recovery.");
        foreach(var asset in record.Assets??[])
            if(Hash(File.ReadAllBytes(AssetPath(root,asset)))!=asset.Sha256)
                throw new InvalidDataException("A saved terrain texture image changed; recovery stopped to preserve it.");
    }
    private static string AssetPath(string root,StartBlankRecoveryAsset asset)
    {
        if(!asset.ProjectRelative)return Path.GetFullPath(asset.Path);
        string path=Path.GetFullPath(Path.Combine(root,asset.Path));
        if(!path.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.Ordinal))
            throw new InvalidDataException("A recovery texture path escaped the project.");return path;
    }
    private static IEnumerable<string> ReferencedImages(string root,string key)
    {
        HashSet<string> paths=new(StringComparer.Ordinal);
        foreach(string manifest in new[]{CustomTerrainTextureStore.ManifestPath(root,key),NativeTerrainTextureRelocationEditStore.ManifestPath(root,key)})
        {
            if(!File.Exists(manifest))continue;
            using var doc=JsonDocument.Parse(File.ReadAllBytes(manifest));Visit(doc.RootElement);
        }
        return paths;
        void Visit(JsonElement node)
        {
            if(node.ValueKind==JsonValueKind.Array){foreach(var child in node.EnumerateArray())Visit(child);}
            else if(node.ValueKind==JsonValueKind.Object)foreach(var property in node.EnumerateObject())
            {
                if(property.Name is "sourceImagePath" or "previewImagePath" && property.Value.ValueKind==JsonValueKind.String)
                {
                    string value=property.Value.GetString()??"";if(string.IsNullOrWhiteSpace(value))continue;
                    string path=Path.GetFullPath(Path.IsPathFullyQualified(value)?value:Path.Combine(root,value));
                    if(new[]{".png",".jpg",".jpeg",".bmp",".webp"}.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase)&&File.Exists(path))paths.Add(path);
                }
                else Visit(property.Value);
            }
        }
    }
    private sealed record Lifetime(int SchemaVersion,string LevelKey,int NativePrefixRows,UniversalPlacementLedger Ledger);
    private static string LifetimePath(string root,string key)=>Path.Combine(root,Key(key)+"-cleared-lifetime-ledger.json");
    public static UniversalPlacementLedger ReconcileLifetime(string root,string key,UniversalPlacementLedger current,int nativePrefixRows)
    {
        current.Validate();
        var saved=File.Exists(LifetimePath(root,key))?JsonSerializer.Deserialize<Lifetime>(File.ReadAllBytes(LifetimePath(root,key))):null;
        if(saved!=null&&(saved.SchemaVersion!=1||saved.LevelKey!=key||saved.NativePrefixRows!=nativePrefixRows))
            throw new InvalidDataException("The cleared level lifetime identities belong to another native prefix.");
        var prior=saved?.Ledger??UniversalPlacementLedger.Empty;prior.Validate();
        for(int i=0;i<Math.Min(prior.Entries.Count,current.Entries.Count);i++)
            if(prior.Entries[i]!=current.Entries[i])throw new InvalidDataException("The before-clear collectible identities diverged; no slots were reused.");
        var merged=prior.Entries.Count>current.Entries.Count?prior:current;
        if(nativePrefixRows+merged.Entries.Count>256)throw new InvalidDataException("The native collectible identity rows are full.");
        return merged;
    }
    public static UniversalPlacementLedger SaveLifetime(string root,string key,UniversalPlacementLedger current,int nativePrefixRows)
    {
        var ledger=ReconcileLifetime(root,key,current,nativePrefixRows);
        string path=LifetimePath(root,key),temporary=path+".pending-"+Guid.NewGuid().ToString("N");
        try{File.WriteAllBytes(temporary,JsonSerializer.SerializeToUtf8Bytes(new Lifetime(1,key,nativePrefixRows,ledger)));File.Move(temporary,path,true);}
        finally{if(File.Exists(temporary))File.Delete(temporary);}return ledger;
    }
    public static UniversalPlacementLedger ReconcileNativeLedger(string nativeEditPath,UniversalPlacementLedger current)
    {
        const string suffix="-native-edits.json";string filename=Path.GetFileName(nativeEditPath);
        if(!filename.EndsWith(suffix,StringComparison.Ordinal))return current;
        string root=Path.GetDirectoryName(Path.GetFullPath(nativeEditPath))!,key=filename[..^suffix.Length];
        string lifetimePath=LifetimePath(root,key);
        if(!File.Exists(lifetimePath))return current;
        var saved=JsonSerializer.Deserialize<Lifetime>(File.ReadAllBytes(lifetimePath))??throw new InvalidDataException("Empty cleared lifetime identities.");
        return ReconcileLifetime(root,key,current,saved.NativePrefixRows);
    }
    private static void RequireRecoveryDirectories(string root,string key)
    {
        foreach(string path in new[]{root,Path.Combine(root,"level-recovery"),Path.Combine(root,"level-recovery",key)})
            if(Directory.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)
                throw new InvalidDataException("Recovery storage cannot use linked directories.");
    }
    private static bool FileBytesEqual(string path,byte[]? bytes)=>bytes==null?!File.Exists(path):File.Exists(path)&&File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes);
}

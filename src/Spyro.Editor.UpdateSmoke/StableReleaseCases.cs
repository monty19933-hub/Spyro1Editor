using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Updates;

internal static class StableReleaseCases
{
    internal static async Task RunAsync()
    {
        int checks=0;void Check(bool ok,string text){checks++;if(!ok)throw new InvalidOperationException("Stable updater: "+text);}
        foreach(string v in new[]{"1.0","1.1","1.0.1","12.10.7"})Check(EditorStableReleaseVersion.TryParse(v,out _),"valid version "+v);
        foreach(string v in new[]{"1","1.0.0","01.0","1.00","1.0-beta.1"," 1.0","1.0 ","0.1","1.-1","1.0+abc"})Check(!EditorStableReleaseVersion.TryParse(v,out _),"noncanonical version "+v);
        Check(!EditorStableReleaseVersion.TryParseReleaseTag("beta-v6",out _),"beta tag refused");
        var current=new EditorStableReleaseVersion(1,0);var next=new EditorStableReleaseVersion(1,1);
        Check(next.CompareTo(current)>0,"stable ordering");Check(!EditorBetaReleaseVersion.TryParseDisplayName(next.DisplayName,out _),"old beta discovery cannot see stable name");
        byte[] stableDll=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spyro.Editor.App.StableFixture.dll"));
        using(var input=new MemoryStream(stableDll)){var identity=EditorStableAssemblyReleaseIdentityReader.Read(input);Check(identity.PublicVersion==next&&identity.ReleaseManifestSchema==3,"exact stable PE metadata");}
        using(var input=new MemoryStream(stableDll)){try{EditorAssemblyReleaseIdentityReader.Read(input);throw new Exception("accepted stable DLL as beta");}catch(InvalidDataException){checks++;}}
        if (typeof(Spyro.Editor.App.App).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false).Cast<System.Reflection.AssemblyMetadataAttribute>().Any(a=>a.Key=="SpyroEditorReleaseChannel" && a.Value=="stable"))
        {
            using var activeInput=new MemoryStream(File.ReadAllBytes(typeof(Spyro.Editor.App.App).Assembly.Location));
            var activeIdentity=EditorStableAssemblyReleaseIdentityReader.Read(activeInput);
            Check(activeIdentity.ReleaseManifestSchema==3,"active source App has valid stable PE metadata");
        }
        string platform=OperatingSystem.IsMacOS()?"osx-arm64":"win-x64";
        string appPath=OperatingSystem.IsMacOS()?"Spyro Editor.app/Contents/MacOS/Spyro.Editor.App.dll":"support/app/Spyro.Editor.App.dll";
        string notes="# Spyro Editor 1.1\n\nStable channel test.";
        byte[] Package(int schema=3,string channel="stable",byte[]? dll=null,string internalVersion="1.1.0",string? rogue=null)
        {
            using var buffer=new MemoryStream();using(var zip=new ZipArchive(buffer,ZipArchiveMode.Create,true))
            {void Add(string name,byte[] bytes){var entry=zip.CreateEntry(next.PackageStem+"-"+platform+"/"+name);using var output=entry.Open();output.Write(bytes);}
            Add("README.txt",Encoding.UTF8.GetBytes(next.DisplayName+"\n"));Add("CHANGELOG.md",Encoding.UTF8.GetBytes(notes));
            Add("release-manifest.json",JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=schema,channel,publicVersion=next.CanonicalVersion,displayName=next.DisplayName,internalVersion,platform}));Add(appPath,dll??stableDll);if(rogue!=null)Add(rogue,[1]);}
            return buffer.ToArray();
        }
        byte[] valid=Package();string digest=Convert.ToHexStringLower(SHA256.HashData(valid));string asset=next.PackageStem+"-"+platform+".zip";
        object Release(string name="Spyro Editor 1.1",string tag="v1.1",bool prerelease=false,string? hash=null)=>new{name,tag_name=tag,prerelease,draft=false,body=notes,html_url="https://github.com/monty19933-hub/Spyro1Editor/releases/tag/"+tag,assets=new[]{new{name=asset,size=valid.Length,digest="sha256:"+(hash??digest),browser_download_url="https://github.com/monty19933-hub/Spyro1Editor/releases/download/"+tag+"/"+asset}}};
        using var handler=new Handler(JsonSerializer.Serialize(new[]{Release("Spyro Editor Beta V99","beta-v99",true),Release(prerelease:true),Release(tag:"v1.2"),Release(hash:"bad"),Release()}),valid);
        using var http=new HttpClient(handler);var client=new GitHubStableReleaseUpdateClient(http);var update=await client.CheckAsync(current);
        Check(update?.Channel=="stable"&&update.BetaVersion==0&&update.PublicVersion=="1.1","discovery filters beta/prerelease/mismatch/digest");
        Check(await client.CheckAsync(next)==null,"current stable version is not an update");
        string root=Path.Combine(Path.GetTempPath(),"spyro-stable-update-"+Guid.NewGuid().ToString("N"));
        try
        {
            await client.DownloadVerifiedAsync(update!,root);await client.DownloadVerifiedAsync(update!,root);Check(handler.Downloads==1,"verified cache reused");
            string changelog=await GitHubStableReleaseUpdateClient.WriteChangelogAsync(update!,root);Check(File.ReadAllText(changelog).StartsWith(next.DisplayName),"stable changelog identity");
            async Task Reject(byte[] bytes,string name)
            {using var h=new Handler("[]",bytes);using var c=new HttpClient(h);var u=update! with{Asset=update.Asset with{Size=bytes.Length,Sha256=Convert.ToHexStringLower(SHA256.HashData(bytes))}};string dest=Path.Combine(root,name);try{await new GitHubStableReleaseUpdateClient(c).DownloadVerifiedAsync(u,dest);throw new Exception("accepted "+name);}catch(InvalidDataException){Check(!Directory.Exists(dest)||!Directory.EnumerateFiles(dest).Any(),"rejection leaves no package: "+name);}}
            await Reject(Package(schema:2),"schema2");await Reject(Package(channel:"beta"),"betaManifest");await Reject(Package(internalVersion:"1.1.0-beta.1"),"betaInternal");await Reject(Package(dll:File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Spyro.Editor.App.BetaFixture.dll"))),"betaDll");await Reject(Package(rogue:"../outside"),"traversal");
            try{await client.DownloadVerifiedAsync(update! with{Channel="beta"},Path.Combine(root,"wrongChannel"));throw new Exception("accepted beta update");}catch(InvalidDataException){checks++;}
            try{GitHubReleaseUpdateClient.RequireUpdateVersion(update!);throw new Exception("beta updater accepted stable update");}catch(InvalidDataException){checks++;}
            using var badHandler=new Handler("[]",valid);using var badHttp=new HttpClient(badHandler);try{await new GitHubStableReleaseUpdateClient(badHttp).DownloadVerifiedAsync(update! with{Asset=update.Asset with{Sha256=new string('0',64)}},Path.Combine(root,"digest"));throw new Exception("accepted bad digest");}catch(InvalidDataException){checks++;}
            Console.WriteLine("STABLE_RELEASE_UPDATE_OK checks="+checks+" network=false separateChannels=true");
        }
        finally{Directory.Delete(root,true);}
    }
    private sealed class Handler(string releases,byte[] bytes):HttpMessageHandler
    {public int Downloads;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken){bool listing=request.RequestUri!.Host=="api.github.com";if(!listing)Downloads++;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=listing?new StringContent(releases):new ByteArrayContent(bytes)});}}
}

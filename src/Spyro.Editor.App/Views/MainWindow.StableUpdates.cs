using System.Text.Json;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Updates;
using Spyro.Editor.App;

namespace Spyro.Editor.App.Views;
public sealed partial class MainWindow
{
    private static string StableStatePath(ReleaseProjectContext context)=>Path.Combine(context.UserData.SettingsPath,"update-check-stable.json");
    private sealed record StableUpdateState(int SchemaVersion,string Channel,DateTimeOffset LastCheckedUtc,string LastNotified="",string LastDownloaded="");
    private static StableUpdateState? ReadStableState(string path)
    {try{var s=File.Exists(path)?JsonSerializer.Deserialize<StableUpdateState>(File.ReadAllText(path)):null;return s?.SchemaVersion==1&&s.Channel=="stable"?s:null;}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException){return null;}}
    private static async Task WriteStableState(string path,StableUpdateState state)
    {Directory.CreateDirectory(Path.GetDirectoryName(path)!);string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{await File.WriteAllTextAsync(temporary,JsonSerializer.Serialize(state));File.Move(temporary,path,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
    private async Task CheckStableUpdatesAsync(bool quiet)
    {
        var context=ReleaseProjectBootstrap.Current;if(!_releaseMode||context==null)return;
        var version=AppReleaseIdentity.StableVersion!;var path=StableStatePath(context);var saved=ReadStableState(path);
        if(quiet&&saved!=null&&EditorUpdateNotificationPolicy.IsRecentCheck(DateTimeOffset.UtcNow,saved.LastCheckedUtc,TimeSpan.FromHours(24)))return;
        try
        {
            var update=await new GitHubStableReleaseUpdateClient(UpdateHttpClient).CheckAsync(version);
            var next=new StableUpdateState(1,"stable",DateTimeOffset.UtcNow,saved?.LastNotified??"",saved?.LastDownloaded??"");await WriteStableState(path,next);
            if(update==null){if(!quiet)await ShowUpdateResultDialogAsync(null,$"You already have the newest stable release: {version.DisplayName}.");return;}
            if(quiet)
            {
                var available=GitHubStableReleaseUpdateClient.RequireUpdateVersion(update);
                if(EditorStableReleaseVersion.TryParse(next.LastNotified,out var notified)&&notified.CompareTo(available)>=0)return;
                for(int attempt=0;attempt<300&&(!IsVisible||_loadingLevel);attempt++)await Task.Delay(100);
                if(!IsVisible||_updateNotificationBanner==null)return;
                ShowUpdateNotification(update);await WriteStableState(path,next with{LastNotified=available.CanonicalVersion});
            }
            else {await WriteStableState(path,next with{LastNotified=update.PublicVersion});await ShowUpdateResultDialogAsync(update,"");}
        }
        catch(Exception e){EditorDiagnostics.RecordWarning("Stable update check failed",e.Message);if(!quiet)await ShowUpdateResultDialogAsync(null,$"Could not check stable releases: {e.Message}");}
    }
    private static Task RememberStableDownloadAsync(ReleaseProjectContext context,EditorUpdateInfo update)
    {var version=GitHubStableReleaseUpdateClient.RequireUpdateVersion(update);var path=StableStatePath(context);var saved=ReadStableState(path);return WriteStableState(path,new(1,"stable",saved?.LastCheckedUtc??DateTimeOffset.UtcNow,version.CanonicalVersion,version.CanonicalVersion));}
}

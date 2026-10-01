using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

internal static class SourceAliasCases
{
    internal static async Task Run(Action<bool,string> check)
    {
        string root=Path.Combine(Path.GetTempPath(),"spyro-capture-alias-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string original=Path.Combine(root,"source.bin"),alias=Path.Combine(root,"alias.bin");
            byte[] sentinel=[1,3,7,9,11];File.WriteAllBytes(original,sentinel);
            File.CreateSymbolicLink(alias,original);
            foreach(string output in new[]{original,alias})
            foreach(bool cleared in new[]{false,true})
            {
                bool exactRefusal=false;
                try
                {
                    if(cleared)await NativeClearedLevelCompilation.CaptureAsync(original,SceneDocument.New(),new LevelCatalog([]),output);
                    else await NativeFreshMainCompilation.CaptureAsync(original,SceneDocument.New(),output);
                }
                catch(InvalidOperationException e) when(e.Message.Contains("selected source BIN",StringComparison.Ordinal)) { exactRefusal=true; }
                // The tiny file cannot parse as a disc; require the explicit path
                // guard first, not an unrelated later parse/target error.
                check(exactRefusal&&File.ReadAllBytes(original).AsSpan().SequenceEqual(sentinel)&&File.ReadAllBytes(alias).AsSpan().SequenceEqual(sentinel),
                    (cleared?"cleared":"fresh")+" capture refuses "+(output==alias?"symlink":"same-path")+" before source parsing and preserves source");
            }
        }
        finally { Directory.Delete(root,true); }
    }
}

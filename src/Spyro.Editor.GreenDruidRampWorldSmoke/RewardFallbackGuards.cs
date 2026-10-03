using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Universal;

internal static class RewardFallbackGuards
{
    internal static int Run(string dir,UniversalSourceLibrary lib,NativeClearedLevelCompilation compiled,UniversalComposeResult composed,IEnumerable<(long Offset,int Length)> protectedRanges)
    {
        int checks=0;void Check(bool v,string why){if(!v)throw new Exception(why);checks++;}
        void Refuse(byte[] data,string why){try{compiled.RewardFallbacks.RequireInstalled(data);}catch(InvalidDataException){checks++;return;}throw new Exception("Accepted reward fallback mutation: "+why);}
        byte[] host=compiled.CopyLevelData();var body=new UniversalLevelData(host);var final=new UniversalLevelData(composed.LevelData);
        compiled.RewardFallbacks.RequireInstalled(host);compiled.RewardFallbacks.RequireInstalled(composed.LevelData);checks+=2;
        var bank=body.Subfiles()[2];var gpu=body.Subfiles()[0];var town=lib.Get("townsquare").Data;
        foreach(var spec in NativeId65FreshWizardResearchFallbackResourceSource.PackageSpecs)
        {
            var r=body.Registrations().Single(r=>r.ClassId==spec.Class);var f=final.Registrations().Single(r=>r.ClassId==spec.Class);
            Check((r.Root&7)==(spec.At&7),"original fallback root alignment");
            Check(body.Bytes.AsSpan(r.Root,spec.Length).SequenceEqual(town.Bytes.AsSpan(spec.At,spec.Length)),"complete original fallback package");
            Check(final.Bytes.AsSpan(f.Root,spec.Length).SequenceEqual(town.Bytes.AsSpan(spec.At,spec.Length)),"fallback after actual source model, texture, sound and world imports");
            foreach(int field in new[]{0x50+4*r.Slot,0x150+2*r.Slot}){var bad=host.ToArray();bad[field]^=1;Refuse(bad,"registration root/identity");}
            // Every byte of each complete source package must remain authenticated.
            for(int at=0;at<spec.Length;at++){var bad=host.ToArray();bad[r.Root+at]^=1;Refuse(bad,$"class{spec.Class:X} package byte{at:X}");}
        }
        var e=body.Registrations().Single(r=>r.ClassId==0xE);var f0=body.Registrations().Single(r=>r.ClassId==0xF);var ten=body.Registrations().Single(r=>r.ClassId==0x10);
        Check(e.Root+0x7EC==f0.Root&&body.Bytes.AsSpan(e.Root+0x7EC,8).SequenceEqual(Convert.FromHexString("01000000FFFFFFFF")),"original simple renderer next-eight-byte owner");
        Check(bank.Offset+bank.Length>=ten.Root+0x888,"complete10 plus conservative runtime extent remains reserved");
        foreach(int delta in new[]{-4,4,8}){var bad=host.ToArray();BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(0x50+4*f0.Slot),f0.Root+delta);Refuse(bad,"renderer next-owner displacement");}
        var ranges=protectedRanges.ToArray();
        foreach(var region in new (int At,int Width,int Rows)[]{(0x48320,16,32),(0x582B0,16,32),(0x6C3A0,2,4),(0x722E0,32,1),(0x7C2C0,32,1),(0x7E2C0,32,1)})
        for(int row=0;row<region.Rows;row++)for(int x=0;x<region.Width;x++)
        {
            int at=region.At+row*1024+x;Check(ranges.Any(r=>r.Offset<=at&&r.Offset+r.Length>at),"fallback texel/CLUT including zero bytes is protected in actual Composer scan");
            Check(host[gpu.Offset+at]==town.Bytes[town.Subfiles()[0].Offset+at],"source fallback texture byte");
            var bad=host.ToArray();bad[gpu.Offset+at]^=1;Refuse(bad,"texel/palette/retained alias");
        }
        Check(body.Scene().RowCount==0,"fallback resources add no placed actors");
        Check(UniversalMobyComposer.RuntimeEnd(body,new UniversalOverlay(compiled.CopyOverlay(),UniversalExeBindings.Read(compiled.CopyExecutable(),20)).CopyBuffer)<UniversalMobyComposer.NormalBufferFloor-UniversalMobyComposer.RamSafetyMargin,"actual shared kernel including fallback growth fits native capacity");
        File.WriteAllText(Path.Combine(dir,"reward-fallback-guards.json"),JsonSerializer.Serialize(new{Marker="FRESH_REWARD_FALLBACK_RESOURCE_GUARDS_OK",Checks=checks,Classes=new[]{14,15,16},SourcePackageBytes=4776,ProtectedTextureBytes=1128,Gameplay=false}));
        Console.WriteLine("FRESH_REWARD_FALLBACK_RESOURCE_GUARDS_OK "+checks);return checks;
    }
}

using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Levels;

static class ParticleSourceCases
{
    public static void Run(string image,string catalogRoot,string output,Action<bool,string> check)
    {
        if(File.Exists(output))throw new IOException("Existing private source receipt preserved.");
        var catalog=LevelCatalog.Load(catalogRoot);var lib=new UniversalSourceLibrary(image,catalog);
        UniversalHighValueGems.RequireNativeOwner(lib.Disc.Exe);List<object> rows=[];
        foreach(var level in catalog.Levels.Where(l=>l.SourceWadEntry>0))
        {
            var (_,data,ov)=lib.Get(level.Key);UniversalHighValueGems.RequireDestinationParticleOwner(ov);
            check(true,"native destination type12 owners "+level.Key);
            check(UniversalParticleDependencies.Records(data).ContainsKey(12),"destination-native type12 texture exists "+level.Key);
            uint spawn=ov.Bindings[UniversalExeBindings.ParticleSpawnGlobal].Value,update=ov.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value;
            uint Table(uint function)
            {
                for(uint pc=function;pc<function+0x100;pc+=4)
                {
                    uint w=ov.Word(pc);if(UniversalMips.Op(w)!=35||UniversalMips.Rs(w)!=1||UniversalMips.Rt(w)!=2)continue;
                    for(uint p=pc-4;p>=function;p-=4)
                    {uint upper=ov.Word(p);if(UniversalMips.Op(upper)==15&&UniversalMips.Rt(upper)==1)return unchecked(((uint)UniversalMips.Imm(upper)<<16)+(uint)UniversalMips.SImm(w));}
                }
                throw new InvalidDataException("Native particle type12 table producer is absent.");
            }
            uint st=Table(spawn),ut=Table(update);int sn=(int)(ov.FunctionEnd(spawn)-spawn),un=(int)(ov.FunctionEnd(update)-update);
            byte[] sb=ov.Bytes.Slice((int)(spawn-UniversalOverlay.Base),sn).ToArray(),ub=ov.Bytes.Slice((int)(update-UniversalOverlay.Base),un).ToArray();
            foreach(var (pc,length) in new[]{(spawn,sn),(update,un),(st+48,4),(ut+48,4)})
                foreach(int delta in new[]{0,length/2,length-1}.Distinct())
                {
                    byte[] bad=ov.Bytes.ToArray();bad[(int)(pc-UniversalOverlay.Base)+delta]^=1;bool refuses=false;
                    try{UniversalHighValueGems.RequireDestinationParticleOwner(new(bad,ov.Bindings));}catch(InvalidDataException){refuses=true;}
                    check(refuses,"edited actual particle owner/table refused "+level.Key);
                }
            rows.Add(new{Level=level.Key,Spawn=spawn,SpawnLength=sn,SpawnSha256=Convert.ToHexStringLower(SHA256.HashData(sb)),SpawnTable=st,SpawnCase=ov.Word(st+48),
                Update=update,UpdateLength=un,UpdateSha256=Convert.ToHexStringLower(SHA256.HashData(ub)),UpdateTable=ut,UpdateCase=ov.Word(ut+48),TextureRecord12=true,OverlayBytes=Convert.ToHexString(ov.Bytes)});
        }
        check(rows.Count==35,"all 35 native particle families certified");
        File.WriteAllText(output,JsonSerializer.Serialize(rows));
    }
}

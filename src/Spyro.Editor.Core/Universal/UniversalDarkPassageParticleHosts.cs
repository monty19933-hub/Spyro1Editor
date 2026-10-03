using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;
internal sealed record UniversalDarkProjectileHost(string Key,int LevelId,bool HasEquivalentNativeImpact,uint SpawnTable,string SpawnTableSha,uint UpdateTable,string UpdateTableSha,uint SpawnLui,uint SpawnLo,uint UpdateLui,uint UpdateLo,int InitializerLength,string InitializerSha);
public static partial class UniversalHighValueGems
{
 internal static (int Index,uint Spawn,uint Update) DarkProjectileParticleOwner(UniversalOverlay o)
 {
  // Reuse only the already issued complete original-USA particle function owners;
  // this grants no particle12 caller or gameplay authority.
  RequireDestinationParticleOwner(o);uint spawn=o.Bindings[UniversalExeBindings.ParticleSpawnGlobal].Value,update=o.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value;
  int i=Array.FindIndex(ParticleOwners,p=>p.Spawn==spawn&&p.Update==update);return(i,spawn,update);
 }
}
/// <summary>Exact retained native1 and native/no-op13 owners for the original35
/// dispatchers. Known no-op13 hosts require the separately issued source13 wrapper.</summary>
internal static class UniversalDarkPassageParticleHosts
{
 private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
 internal static UniversalDarkProjectileHost Resolve(UniversalOverlay overlay)
 {
  var owner=UniversalHighValueGems.DarkProjectileParticleOwner(overlay);var p=Profiles[owner.Index];
  foreach(var (pc,sha) in new[]{(p.SpawnTable,p.SpawnTableSha),(p.UpdateTable,p.UpdateTableSha)})
   if(!overlay.Contains(pc)||(ulong)pc+324>overlay.End||Hash(overlay.Bytes.Slice((int)(pc-UniversalOverlay.Base),324))!=sha)throw new InvalidDataException("Dark projectile host lost its exact native particle table owner.");
  return p;
 }
 internal static UniversalDarkProjectileHost Require(string key,UniversalOverlay overlay,byte[]? exe)
 {
  var p=Resolve(overlay);
  if(key=="authored65")
  {
   if(p.Key!="townsquare")throw new InvalidDataException("Fresh Dark projectile host needs its exact native Town engine.");
   if(exe==null)
   {
    var spawn=overlay.Bindings[UniversalExeBindings.ParticleSpawnGlobal];var update=overlay.Bindings[UniversalExeBindings.ParticleUpdateGlobal];
    bool nativeMetadata=spawn==new UniversalExeBinding(spawn.Global,p.SpawnLui,p.SpawnLo,0x80088F68)&&update==new UniversalExeBinding(update.Global,p.UpdateLui,p.UpdateLo,0x80087E20);
    bool compact=spawn==new UniversalExeBinding(spawn.Global,0,0,0x80088F68)&&update==new UniversalExeBinding(update.Global,0,0,0x80087E20);
    if(!nativeMetadata&&!compact)throw new InvalidDataException("Fresh Dark projectile callback metadata lost its native or canonical compact owner.");
   }
   if(exe!=null)Spyro.Editor.Core.Exporting.NativePortalRouteFreshBinding.RequireNativeParticleInitializer(exe);
   foreach(uint global in new[]{UniversalExeBindings.ParticleSpawnGlobal,UniversalExeBindings.ParticleUpdateGlobal})
    if(exe!=null&&UniversalExeBindings.Read(exe,65)[global]!=overlay.Bindings[global])throw new InvalidDataException("Fresh Dark projectile callback identity changed.");
   return p;
  }
  var target=Profiles.SingleOrDefault(p=>p.Key==key)??throw new InvalidDataException("Unowned Dark projectile host identity.");
  bool cleared=p.Key=="townsquare"&&target.InitializerLength==152;
  if(p.Key!=target.Key&&!cleared)throw new InvalidDataException("Dark projectile host profile differs from its destination.");
  var native=UniversalHighValueGems.DarkProjectileParticleOwner(overlay);
  var expected=new[]{new UniversalExeBinding(UniversalExeBindings.ParticleSpawnGlobal,target.SpawnLui,target.SpawnLo,native.Spawn),new UniversalExeBinding(UniversalExeBindings.ParticleUpdateGlobal,target.UpdateLui,target.UpdateLo,native.Update)};
  foreach(var b in expected)if(!overlay.Bindings.TryGetValue(b.Global,out var actual)||actual!=b)throw new InvalidDataException("Dark projectile host callback/initializer identity changed.");
  if(exe!=null)
  {
   if(exe.Length!=0x66000)throw new InvalidDataException("Dark projectile host needs the complete native executable.");
   var read=UniversalExeBindings.Read(exe,target.LevelId);foreach(var b in expected)if(!read.TryGetValue(b.Global,out var actual)||actual!=b)throw new InvalidDataException("Dark projectile host executable callback identity changed.");
   uint entry=target.SpawnLui-48;
   if(UniversalExeBindings.Word(exe,UniversalExeBindings.LevelInitJumpTable+(uint)target.LevelId*4)!=entry||Hash(exe.AsSpan((int)(entry-UniversalExeBindings.ExeBase),p.InitializerLength))!=p.InitializerSha)throw new InvalidDataException("Dark projectile host lost its complete original/cleared initializer.");
  }
  return p;
 }
 internal static readonly UniversalDarkProjectileHost[] Profiles=
 [
 new("artisans",10,false,0x8007AD18,"0b6fbb3e73e0fec0b6c041e769934482a851b900a8ed713db1cdc4f471ece894",0x8007ABD4,"3cd12c5a5baae02e9c878bcddc09499766533b96ce653f4176e81e131adb8505",0x8005A748,0x8005A74C,0x8005A758,0x8005A75C,152,"fe806d69534bcb66ae79423ff1c325fe7785c5c3857b4d92898df8b565494dfa"),
 new("stonehill",11,false,0x8007ADC8,"a1c6e26add9e8a932c634c27c5b7842958dfa2d09671820ed63bf8e7332641ca",0x8007AC84,"dfd4964ad37e5a3ca1b3514b58e6fd76f6a0c54bbec191a2c01fef041b9ba9d3",0x8005A7E0,0x8005A7E4,0x8005A7F0,0x8005A7F4,152,"7e394297f054d99c8bdc85d4289be3bc6cea35d10f2e101647a572749bcb613f"),
 new("darkhollow",12,false,0x8007ACB8,"3d7229fa46f8f21d4d3dd07f28fb18cbfa0872c445aa24ae571eb1975243c4c6",0x8007AB74,"138aafd4977dd3327e9283992d2082469c5d064b08b373848f2101d53cce3997",0x8005A878,0x8005A87C,0x8005A888,0x8005A88C,80,"bb0bcedcf3f8862724eada056a33adb9b2aeed82756bd99bedcce30f62ae7e53"),
 new("townsquare",13,false,0x8007ADC4,"cfbfca35aad07e26113533f2444ec9d148510523e93e37ddf506aa5203132532",0x8007AC80,"2e8c2cfb1539eab094c0c0d8cb771df1aaac08c092947b20869c715e4fc93bc8",0x8005A8C8,0x8005A8CC,0x8005A8D8,0x8005A8DC,152,"fe4669229aedbbc8ff698d57a237327af36025aaeda0ff5cc2e1c806fd23e50b"),
 new("toasty",14,false,0x8007AE0C,"9079e477857c49395f804600d1e3ecfcac716c57f00cf1948545a98741c90cbf",0x8007ACC8,"2abdf00c812cf2411b8fdbfa7bd16c60404d743ea8c977f586ddf7ef696449dc",0x8005A960,0x8005A964,0x8005A970,0x8005A974,80,"014761bc746d756b6bb3a6451167ce1b2135dd0975a509da4dd3961ec72b9d44"),
 new("sunnyflight",15,true,0x8007ACA8,"d51df0f05c199e12e635224f3c6ac1f4b32e13e57373442e6d36fb6d42f34550",0x8007AB68,"015b1f6b6a70ae6d3ce540e4a55020872776a9514b8e44b900c72b23a179fc1d",0x8005A9B0,0x8005A9B4,0x8005A9C0,0x8005A9C4,80,"90d12d9980086f2a4cd1baef91fbfcba89f2be5ff1f35f88eb2c0e8441ed07ec"),
 new("peacekeepers",20,true,0x8007B710,"84c0178e89a2789a34e7e04487eaa2483d130caaafb20ceebbdd0a11af07579f",0x8007B5CC,"8d43637b3dcf652cb8ce111ee7a360792f803122ddca530618a033e076a6320f",0x8005AA58,0x8005AA5C,0x8005AA68,0x8005AA6C,152,"229c1235ff1e6860db55fb922e66eb526356d88f669e9ed41b609ea9f8af1187"),
 new("drycanyon",21,true,0x8007B5B0,"d51bbb8cc00a616b8a977355eb6a42df8e238eb57a2a63399bb872738010d98f",0x8007B46C,"23585bf1d1a064a40b6afa0d38663ce34f5a586e4bd98c3261daec074528024c",0x8005AAF0,0x8005AAF4,0x8005AB00,0x8005AB04,152,"ed27c554747f4f2b106b47e4feb49c63f8835064e865745c4d3a8b2fbb72a0e1"),
 new("clifftown",22,false,0x8007B590,"decfa21da5cb35bdb708a57c73b169038598a9dcdf5452b4afe5d092948a8884",0x8007B44C,"f99969a05011f76a0ef5c476dca4af4680a8cb618673cc757a3d96461b7d28ef",0x8005AB88,0x8005AB8C,0x8005AB98,0x8005AB9C,152,"e8f5b16d48b0c0d43010af84041e0dcb7b777b24234ab67aa5efec2983256543"),
 new("icecavern",23,false,0x8007B360,"b13ffe084113883b4fe924ddbc2eed112590973fdb5818b7e5a829f269b9e5e0",0x8007B21C,"e4458f5af21314163597ee6b2a73615934f0d1dd42639a21e3f91a67d5281851",0x8005AC20,0x8005AC24,0x8005AC30,0x8005AC34,80,"12ec03903edf87214354599be89a3893ff5fe6cb70e2f11fffb24940c376368e"),
 new("doctorshemp",24,false,0x8007AD30,"7a1e9c0127ba8b7ed16a1917890a223fb8147913bbdf011083c56dc3c48acf02",0x8007ABEC,"344e702a5398db4ad1ced6c91bcf46b166a29c73ba1e35710763ccbd150acb81",0x8005AC70,0x8005AC74,0x8005AC80,0x8005AC84,80,"6e31163449dc424125fb23892a256bc1a738ad68f7955da909492dbf5f3ca860"),
 new("nightflight",25,false,0x8007ACA8,"33aa1b5a04b4ef5978be485c99f48809c262276f68c8ab63993e836a1914ca69",0x8007AB68,"783ee4e57fc4137d5e2c815a4d3b46142c074706b88e158749c71ed48ccb414b",0x8005ACC0,0x8005ACC4,0x8005ACD0,0x8005ACD4,80,"e410526fa395f9ca2ea862217342c731743d180009f7edd147db3755ab0ebfcd"),
 new("magiccrafters",30,false,0x8007B708,"6264f4d626ee4d4f5734855e5e91aea9613c52ef522d99b7a62caeb388e47fbc",0x8007B5C4,"729993d6b685b1b7b97120a321916ce0b438db60e923cc8d055e3fedfcfe7bda",0x8005AD68,0x8005AD6C,0x8005AD78,0x8005AD7C,152,"e85c2944f6dc345fb991b9f63703b1abb3a794e00f6b1abd834797559398480e"),
 new("alpineridge",31,false,0x8007B998,"f852256d7ac869b23e070f01e36119d9717c3e52b8aeb63d45c8d9631c7fd121",0x8007B854,"a4b2c7b2122f0a6fd91eb7582fd0cc743bcf45316c527d3142bf3aa9b6dfaef4",0x8005AE00,0x8005AE04,0x8005AE10,0x8005AE14,80,"9bfa1ce565b0e389cfd56be5741b6f2deefadd362bee9c53219a104bddcb77b4"),
 new("highcaves",32,false,0x8007B4A4,"85fceb1df312428bc004bedcfa00e22ac4cab44aaab5109a2c807d5031ceb55b",0x8007B360,"07079fe1a9f4a763aeabd7e6c3d975990225322c3700f1e93cd0c70791cab80e",0x8005AE50,0x8005AE54,0x8005AE60,0x8005AE64,80,"4e77642b189c5a58079a44089e52fe2a651cd29265372fd7a4afd50136283f10"),
 new("wizardpeak",33,false,0x8007B620,"f602c4989ba73932779ec594c114662026116c18e24b06e98d7a62a18a29a32e",0x8007B4DC,"adbffe6141473ba4e3744cf32174467317b08b6d31dafb8be85fc55bda42ae2d",0x8005AEA0,0x8005AEA4,0x8005AEB0,0x8005AEB4,80,"fbe4550ffc95bbe9d7920f3fa027f8f0ba9c96cdccd4d4dbcb880fa46d362a98"),
 new("blowhard",34,false,0x8007ADA0,"62f50455799a302b7b10a548dc8ed754b8005bb9c816869a6cb9c613e7d587c6",0x8007AC5C,"89e4d68e32cbbceb84d82b5c3c9e58ec1421a5f2a7ef4e81f8e5bd530b7601f6",0x8005AEF0,0x8005AEF4,0x8005AF00,0x8005AF04,80,"7bb6e9a74abf80ae9381ac15a57b87ca7d7603c17c45dabf9f7ce9a10f9cddce"),
 new("crystalflight",35,false,0x8007ACA8,"59fc99eedb64f2b8bb75d4e20a6e9bb3dfab2ed5dbd5eadc63452d78c12ed96f",0x8007AB68,"3bbec5956608b8701443eec58a97091b4ec731f3c470fbbc2e890fe29649efcc",0x8005AF40,0x8005AF44,0x8005AF50,0x8005AF54,80,"637ac72fbe7c94a412e3b77313ea5a5a0ce8632e4adf81680da76970b3c2c83b"),
 new("beastmakers",40,false,0x8007B4DC,"836c3ffe905768bb9497aaf0eccf0660327379e75096cfd6a08bfee987378ff1",0x8007B398,"a32242a1781ca87cd222b714bca2fa3e499a6767025fae7a9adc781a6c053f11",0x8005AFE8,0x8005AFEC,0x8005AFF8,0x8005AFFC,152,"9ce5ab3727bad54feee738151533bc8759880f0306e197f714b22510e9313c73"),
 new("terracevillage",41,false,0x8007B454,"a7a61c2f15cc8ac22acf4bc467cf347a53c925bd2c6a8abc7533dd118242c903",0x8007B310,"b2b40f117207b98eec550797892d9e9b29f54ba74270af63c4fad8593bdbd287",0x8005B080,0x8005B084,0x8005B090,0x8005B094,80,"73f9ae2f73461b3171e395c194df124cc451fbe7e182f075947fb9ba02fb1c03"),
 new("mistybog",42,false,0x8007AE54,"d68c9aefa649442bd68b4c174370a1a839f98f7e29b770aece2a11fea8479f22",0x8007AD10,"e86ef5d1e65258c68c009247592d382247d03ca5a1899e13356ca30174d610c0",0x8005B0D0,0x8005B0D4,0x8005B0E0,0x8005B0E4,80,"2ffaba32bd9e5b48d7a7d9f3500bfb4529d187e341091afff1ee96ae968208ed"),
 new("treetops",43,false,0x8007B510,"9ebe722df7cd674259f480540aa03ca6542ca328f49712e878620a5bbdee8695",0x8007B3CC,"f20bfebfa814a14c05dd62609b47a067c555881ef7a870757cf8c2c33d1d401e",0x8005B120,0x8005B124,0x8005B130,0x8005B134,80,"f9674890560904e63e7083c0826c0dd5a3507ab5b926a841521077fb79915f6a"),
 new("metalhead",44,false,0x8007B5E8,"178bf094b49ff35479e55adb3d13bcaad93aed9efb3154e0d73824790d2a0a94",0x8007B4A4,"7dd329e107b813722b25859c0aaf2df0d3bd35f5d32f64256f6ebf8d41cbcb0b",0x8005B170,0x8005B174,0x8005B180,0x8005B184,80,"936abcaac5ff8456d744fb16b29979425db0c2be7ef003fd128dfd879ff58733"),
 new("wildflight",45,false,0x8007ACA8,"05b1d171d873b83bdee36e1eea9ab6018432c2d3c92848206901c5b0b527eb7f",0x8007AB68,"12dbbe64ed0e4738b28cea4c87655309885da558f8e75d97de465bc4a1f2200b",0x8005B1C0,0x8005B1C4,0x8005B1D0,0x8005B1D4,80,"6f36ddf8fa78e9de3dc60ee565cacaf70b15f415ab1b8fa79a41c120f8d1e3e4"),
 new("dreamweavers",50,false,0x8007B710,"0850662fffde6453551c266b58e909320ddeda08ef20d5e2af77880fb52b2298",0x8007B5CC,"74813f59112e72233c386cbfcd433b8236913c78a9612cabfc416b246e46cc94",0x8005B268,0x8005B26C,0x8005B278,0x8005B27C,152,"a0ad8c23a7bc28ced832c9a19ac840123465ec25c401497e2ca213f079a455ac"),
 new("darkpassage",51,true,0x8007B370,"5fb0e2d534be293fc1b6eb0c6c37fbd3073c459954548b4dc3fc7cc762ce7390",0x8007B22C,"df43b46222d4772bfb8281acb61ed015ccd3a7dc92d753cd6b94dc1d5fc05400",0x8005B300,0x8005B304,0x8005B310,0x8005B314,80,"70310213dc973221204e782af2fc3477d3351d4e42a692adf3682c7a89821bea"),
 new("loftycastle",52,false,0x8007B354,"65a1e055f8ea285a415cbf5fa29e11df775a49ec56aff42c5b7e83d0b9251cbf",0x8007B210,"34e3a812a31cc438c02ae35b220b4a406e7f78baf1203a196f9a26d48013aab8",0x8005B350,0x8005B354,0x8005B360,0x8005B364,80,"e2abb7e0152333b7afa3f8069bbf21ff5962bf988d83f615008ba17fa5696e43"),
 new("hauntedtowers",53,true,0x8007B388,"fae77a0b13757946e4f8473a4d16ca12b9385f2937d39a7d82134eb00c76b2c5",0x8007B244,"518a53c1100b06bdb3f7dca90635dcf41e66fc8114fef8f1cfa6cfe0cfcd4e54",0x8005B3A0,0x8005B3A4,0x8005B3B0,0x8005B3B4,80,"611380d7a6ed8462c2b1fdb3b397c953359cbb94ec18b6f2e53b1bbca6ce9cc0"),
 new("jacques",54,false,0x8007ADC8,"dd411cbefed32d019418acd223b5139424f95b4a34285a0fd7cc839b89d53b59",0x8007AC84,"99e14fbbf08ca652d478e91521085767d3270ab65137f7a5387481be627710bd",0x8005B3F0,0x8005B3F4,0x8005B400,0x8005B404,80,"4b232cfd8a02e086207c3079ca61c9d95dc26ff11aeb6babb21860fa704bffb7"),
 new("icyflight",55,true,0x8007ACA8,"e5fa7eb5d8d9d03e89e433faeeccb4aafc8d75597799a0e34fd375c6bb2e45dd",0x8007AB68,"a57607887030d6c8a19d717c7e0d32dc13e261dc69042b20598a18e7b87f3056",0x8005B440,0x8005B444,0x8005B450,0x8005B454,80,"58c44980ed3223bc6f6b8526b6233e82845cb959ab4c4b5e93946c7dc873fef0"),
 new("gnastysworld",60,false,0x8007ACA8,"e875cffbaeb31a46df6399485586d52c476e6ea95d1c59720de9d3b40e12fd77",0x8007AB64,"212e3d86a092bc929241ba0ced14296cb3ff2ffaff0e398265211bd7360308a8",0x8005B4E8,0x8005B4EC,0x8005B4F8,0x8005B4FC,152,"4d07e1626bf3f3a56a3a9d5f146fd94ecac817cca4cce57eac4ca27116e595c0"),
 new("gnorccove",61,false,0x8007B3A0,"e945951d8ba2dc05ff0e4808d838047a7a3a88f9509369eb182a1828a72cb360",0x8007B25C,"83826841b4763adae41323df311a54b262063d79dfe299fec89aa8384e38153d",0x8005B580,0x8005B584,0x8005B590,0x8005B594,80,"f37414db64f34a9f80d69377491658034a7a5194a2bdf70c83d990e8d4e9f928"),
 new("twilightharbor",62,true,0x8007ACD4,"84d3f56e5a2a6cc0d917df88f133f3d29d4d0a12c2c4b512f8d1218e1c0b442b",0x8007AB90,"9703ae00e6009e930ceb21b3858eb12b7e2a37547c9e66328b44fdfcc7a3e433",0x8005B5D0,0x8005B5D4,0x8005B5E0,0x8005B5E4,80,"96ac3dab2baa73131c58d9d0cc36c732b5fd2bf893a158dd1a1dcb4348ff9be2"),
 new("gnastygnorc",63,false,0x8007AC04,"9d7c53c01579ec7a9be568607173803cfe77aa057cac68922cdd7827937ef825",0x8007AAC4,"7f397b46da454e022a548510b4108463c80a8dadc9865d91ec5198a4d4dab3a1",0x8005B620,0x8005B624,0x8005B630,0x8005B634,80,"cb1c13f93b060b51afcf79a21f639e085923873d0e8e64de8e1e32066b530759"),
 new("gnastysloot",64,false,0x8007ABEC,"06639a437d27bf12d0eb4bcdea1122531caf4d8188795168eafda51d3f86d606",0x8007AAAC,"3c8eb090c0ada29840f451f3840f703d583a51328ba64e1b6a699e0ecff11908",0x8005B670,0x8005B674,0x8005B680,0x8005B684,80,"8933201e94621ef2ef6bac9a0bac8e7f5aa7ebc5a87858a9a75eaf44acec2c89"),
 ];
}

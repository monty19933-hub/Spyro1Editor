using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeMobySaveMapOverlay(byte[] Overlay, uint UpdatePointer, uint CopyBuffer);
public sealed record NativeMobySaveMapWindow(uint Address, byte[] Code, string SourceSha256);

/// <summary>Physical placement compaction with unchanged native 36x256 saved bits.
/// Code occupies only rescheduled original collected-write/reload owners.</summary>
public static class NativeMobySaveMap
{
    public const uint EmptyReloadBranchAddress = 0x800142B4;
    public const uint ReloadCopyAddress = 0x800143DC;
    public const string EmptyReloadBranchSourceSha256 = "295aa92625eb8be9692062d90c652709bb59a0b31b771d05b49d0583cdb2a3b0";
    public const uint PreInitAddress = 0x8001417C;
    public const uint CheckpointQueryAddress = 0x80038494;
    public const string PreInitSourceSha256 = "1db8c2edc34e915fbab076199a90800439ddb0ab475329256e90faa1014cde05";
    public const string CheckpointQuerySourceSha256 = "48d9e4f53b14cce415a2cbadffcc41e49f9bfa18f17df5e0b2ed2558da20afcf";
    public const uint MapRowAddress = 0x8003B9A4;
    public const uint DeleteAddress = 0x8003B7C0;
    public const string DeleteSourceSha256 = "a8378b5d829c8a0661affbb6e50eb64b606c3114e116a38ec1b7137ba8a30ec4";
    public const uint MarkAddress = 0x8003B854;
    public const uint ReloadAddress = 0x800142C8;
    public const uint Tag = 0x50414D53; // SMAP, full word
    public const string MarkSourceSha256 = "333f5d194c140bcf1be0a97c556a1d39ad2efb56cf70871c6da5bba23ed6b426";
    public const string ReloadSourceSha256 = "3a37e43ef8fcef2f4533c6f8f2ca34e4277ec66bfe1ab8f0f3120837668dd683";
    private sealed class Asm
    {
        public UniversalAsmPart Part { get; } = new();
        public void I(int op,int rs,int rt,int imm)=>Part.Emit(UniversalMips.EncodeI(op,rs,rt,imm));
        public void R(int rs,int rt,int rd,int sh,int fn)=>Part.Emit((uint)(rs<<21|rt<<16|rd<<11|sh<<6|fn));
        public void B(int op,int rs,int rt,string label)=>Part.Emit(UniversalMips.EncodeI(op,rs,rt,0),new(UniversalFixKind.BranchLabel,Label:label));
        public void J(uint address)=>Part.Emit(UniversalMips.EncodeJ(2,address));
        public void J(string label)=>Part.Emit(UniversalMips.EncodeJ(2,0),new(UniversalFixKind.JumpLabel,Label:label));
        public void Jal(uint address)=>Part.Emit(UniversalMips.EncodeJ(3,address));
        public void L(string label)=>Part.Label(label);
        public void N()=>Part.Emit(0);
        public void Li(int r,int n)=>I(9,0,r,n);
        public void La(int r,uint address){I(15,0,r,UniversalMips.Hi(address));I(9,r,r,UniversalMips.Lo(address));}
        public byte[] Bytes(uint address)=>UniversalBehaviorBundleBuilder.Assemble([Part],address).Code;
    }

    public static NativeMobySaveMapOverlay Append(UniversalOverlay destinationOverlay, byte[] finalOverlayBytes,
        uint finalUpdatePointer,uint finalCopyBuffer,IReadOnlyList<int> physicalToLogicalSaveRows)
    {
        ArgumentNullException.ThrowIfNull(destinationOverlay);
        if(physicalToLogicalSaveRows.Count>256||physicalToLogicalSaveRows.Any(n=>n is < -1 or >255))
            throw new InvalidDataException("Native saved object identities require at most 256 physical and logical rows.");
        var active=physicalToLogicalSaveRows.Where(n=>n>=0).ToArray();
        if(active.Distinct().Count()!=active.Length)throw new InvalidDataException("Native logical saved row identities must be unique.");
        if(physicalToLogicalSaveRows.Select((n,i)=>n==i).All(b=>b))return new(finalOverlayBytes,finalUpdatePointer,finalCopyBuffer);
        if(finalUpdatePointer<UniversalOverlay.Base||finalUpdatePointer>=finalCopyBuffer||
            finalCopyBuffer<UniversalOverlay.Base||(long)finalCopyBuffer-UniversalOverlay.Base>finalOverlayBytes.Length)
            throw new InvalidDataException("Save mapping must append to the actual bound resident overlay.");
        uint prefix=(finalCopyBuffer+7)&~7u, start=prefix+12, leaf=start+16, table=leaf+48;
        int oldUpdateOffset=checked((int)(finalUpdatePointer-UniversalOverlay.Base));
        bool hasSigns=oldUpdateOffset>=12 && BinaryPrimitives.ReadUInt32LittleEndian(finalOverlayBytes.AsSpan(oldUpdateOffset-12))==UniversalSpecialBehaviors.SignHookMarker;
        if(hasSigns)
        {
            uint jump=BinaryPrimitives.ReadUInt32LittleEndian(finalOverlayBytes.AsSpan(oldUpdateOffset-8));
            uint delay=BinaryPrimitives.ReadUInt32LittleEndian(finalOverlayBytes.AsSpan(oldUpdateOffset-4));
            uint target=((finalUpdatePointer-4)&0xF0000000)|((jump&0x03FFFFFF)<<2);
            if((jump>>26)!=2 || delay!=0 || target<UniversalOverlay.Base || target>=finalCopyBuffer)
                throw new InvalidDataException("Save mapping requires the verified resident native sign jump prefix.");
        }
        var a=new Asm();a.J(finalUpdatePointer);a.N();a.Part.Emit(Tag);a.Part.Emit(leaf);
        a.I(11,6,2,physicalToLogicalSaveRows.Count);a.B(4,2,0,"invalid");a.R(0,6,2,1,0);
        a.La(3,table);a.R(2,3,2,0,0x21);a.I(33,2,6,0);a.R(31,0,0,0,8);a.N();
        a.L("invalid");a.R(31,0,0,0,8);a.Li(6,-1);a.N();
        byte[] code=a.Bytes(start);
        if(code.Length!=64)throw new InvalidDataException("Save map metadata/leaf layout changed.");
        uint copy=(uint)((table+physicalToLogicalSaveRows.Count*2+15)&~15u);
        byte[] bytes=new byte[UniversalLevelData.Align(checked((int)(copy-UniversalOverlay.Base)),0x800)];
        finalOverlayBytes.AsSpan(0,checked((int)(finalCopyBuffer-UniversalOverlay.Base))).CopyTo(bytes);
        if(hasSigns)finalOverlayBytes.AsSpan(oldUpdateOffset-12,12).CopyTo(bytes.AsSpan(checked((int)(prefix-UniversalOverlay.Base)),12));
        code.CopyTo(bytes,checked((int)(start-UniversalOverlay.Base)));
        for(int i=0;i<physicalToLogicalSaveRows.Count;i++)BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(checked((int)(table-UniversalOverlay.Base))+i*2),(short)physicalToLogicalSaveRows[i]);
        return new(bytes,start,copy);
    }

    public static IReadOnlyList<NativeMobySaveMapWindow> Windows()=>
        [new(MarkAddress,MarkRoutine(),MarkSourceSha256),new(ReloadAddress,ReloadRoutine(),ReloadSourceSha256),new(DeleteAddress,DeleteRoutine(),DeleteSourceSha256),new(PreInitAddress,PreInitRoutine(),PreInitSourceSha256),new(CheckpointQueryAddress,CheckpointQueryRoutine(),CheckpointQuerySourceSha256),new(EmptyReloadBranchAddress,BitConverter.GetBytes(0x10620049u),EmptyReloadBranchSourceSha256)];

    // Original USA bytes of every window, so native consumer proofs can look past an installed save map.
    private static readonly (uint Address, string Original)[] OriginalWindows =
    [
        (0x8003B854, "e0ffbd271000b0af218080001400b1af2188a000160000121800bfaf1400228e00000000000342280300401400000000f355010c000000000780043c6459848c0780023c2074422480200400212082000780023c6058428c0000838c21105000211870000780013c605822ac000083ac0780033c2858638c000000002b10230221004014000000000780023c9058428c000000002b1022021b0040102320230240190400231864004019030021186400c010030021186200c013030023104300801002002110440023100200c3300200032202002128c0001f00c6300780023cb87842248020040021208200010003240418c3000000828c2130a0002510430058ee0008000082ac5600269200000000432106001f00c6300780033c08796324802004000780023c6459428c21282002401102002110430021208200010002240000838c0410c20025186200000083ac0780023cd461428c21380000000044909e56010c100006241800bf8f1400b18f1000b08f2000bd270800e00300000000"),
        (0x800142C8, "433111001f0025320780033c087963240780023c6459428c8030060040110200211043002110c2000000428c0418b400241043002c004010000000000780013c210826009878228c000000002410430008004014212004023a0082907d0003247f0042301e00431000000000ec500008530093a00780023c2858428c00000000211002023a004290000000007f0045302000a228040040100410b40024104202de5000082b1002007e00a2380100422c07004010ff0003240780023c2858428c0000000021100202ec500008530043a00780043c2858848c00000000212090005a49010c000000000780043c2858848c58001026211004024800429000000000bfff531401003126218800000780023cb878422421184000e0ff64240000828c0400842401003126000062ac0800222afaff40140400632409001124"),
        (0x8003B7C0, "0780033c2858638c000000002b1083001e004014000000000780023c9058428c000000002b108200180040102320830040110400231044004011020021104400c018020021104300c01b020023186200801803002118640023180300c3280300031a03001f00a5300780023cb87842248018030021186200010002240000648c0410a20025208200000064ac0800e00300000000"),
        (0x8001417C, "212004023600828400000000801002000780013c210822007863228c000000000500410400000000aa49010c000000006f50000800000000734a010c000000000780053c2858a58c1000a4272128b000c05d000c0c00a5241000a4271800a28f0100053c000442247b35010c1800a2af0780043c2858848cf44c010c212090000780023c2858428c00000000211002023a004790000000007f00e2302000422c17004010433111001f002532802006000780013c210824009878228c0428b300241045000d0040100410f3000780023c6459428c0780033c087963244011020021104300211082000000428c0000000024104500020040140410f30025904202580010260780043c2858848c000000002110040248004390ff000224b8ff621401003126"),
        (0x80038494, "212880000780043c2858848c000000002320a40040110400231044004011020021104400c018020021104300c01b020023186200801803002118640023180300c3200300031a03001f00843080180300010002240780013c210823009878238c041082002418620005006010213000005300a29000000000ff0042380100462c0800e0032110c000"),
        (0x800142B4, "46006210")
    ];

    /// <summary>A strict view for native consumer proofs while the save map is installed. A window holding exactly
    /// its canonical emitted code is shown as the original routine; any other change is left for the proof to refuse.</summary>
    public static byte[] OriginalView(byte[] exe)
    {
        byte[]? view=null;
        foreach(var w in Windows())
        {
            int at=checked((int)(w.Address-UniversalExeBindings.ExeBase));
            if(at<0||at+w.Code.Length>exe.Length||!exe.AsSpan(at,w.Code.Length).SequenceEqual(w.Code))continue;
            byte[] original=Convert.FromHexString(OriginalWindows.Single(o=>o.Address==w.Address).Original);
            if(original.Length!=w.Code.Length||Convert.ToHexStringLower(SHA256.HashData(original))!=w.SourceSha256)
                throw new InvalidDataException($"Native save mapping original {w.Address:X8} does not match its source owner.");
            view??=exe.ToArray();
            original.CopyTo(view,at);
        }
        return view??exe;
    }

    public static void PatchExe(byte[] exe)
    {
        var windows=Windows();
        foreach(var w in windows)
        {
            int offset=checked((int)(w.Address-UniversalExeBindings.ExeBase));
            var current=exe.AsSpan(offset,w.Code.Length);
            if(current.SequenceEqual(w.Code))continue;
            if(Convert.ToHexStringLower(SHA256.HashData(current))!=w.SourceSha256)
                throw new InvalidDataException($"Native save mapping owner {w.Address:X8} is neither original nor its canonical emitted image.");
        }
        foreach(var w in windows)w.Code.CopyTo(exe.AsSpan(checked((int)(w.Address-UniversalExeBindings.ExeBase)),w.Code.Length));
    }

    public static byte[] GatewayRoutine()
    {
        var a=new Asm();a.I(15,0,2,0x8007);a.I(35,2,2,0x5734);a.I(15,0,1,UniversalMips.Hi(Tag));a.I(35,2,3,8);
        a.I(35,2,2,12);a.I(9,1,1,UniversalMips.Lo(Tag));a.B(5,3,1,"identity");a.N();
        a.R(2,0,0,0,8);a.N();a.L("identity");a.R(31,0,0,0,8);a.N();
        byte[] code=a.Bytes(MapRowAddress);if(code.Length!=48)throw new InvalidDataException("Save mapper gateway exceeded its source owner.");return code;
    }

    public static byte[] MarkRoutine()
    {
        var a=new Asm();
        a.I(9,29,29,-32);a.I(43,29,16,16);a.R(4,0,16,0,0x21);a.I(43,29,17,20);a.R(5,0,17,0,0x21);
        a.B(4,16,0,"placed");a.I(43,29,31,24);
        a.I(35,17,2,20);a.N();a.I(10,2,2,768);a.B(5,2,0,"gems");a.N();a.Jal(0x800557CC);a.N();
        a.L("gems");a.I(15,0,8,0x8007);a.I(35,8,4,0x5964);a.I(35,8,2,0x5860);a.R(0,4,4,2,0);
        a.I(9,8,3,0x7420);a.R(4,3,4,0,0x21);a.I(35,4,3,0);a.R(2,16,2,0,0x21);a.R(3,16,3,0,0x21);
        a.I(43,8,2,0x5860);a.I(43,4,3,0);
        a.L("placed");a.I(15,0,3,0x8007);a.I(35,3,2,0x5828);a.I(35,3,3,0x5890);a.R(17,2,4,0,0x2B);
        a.B(5,4,0,"dynamic");a.R(17,2,6,0,0x23);a.R(17,3,4,0,0x2B);a.B(4,4,0,"dynamic");a.Li(2,88);
        a.R(6,2,0,0,0x1B);a.R(0,0,6,0,0x12);a.Jal(MapRowAddress);a.N();a.B(1,6,0,"sound");a.N();
        a.R(0,6,4,5,3);a.R(6,0,5,0,0x21);a.I(12,6,6,31);a.I(15,0,2,0x8007);a.I(9,2,2,0x78B8);
        a.R(0,4,4,2,0);a.R(4,2,4,0,0x21);a.Li(3,1);a.R(6,3,3,0,4);a.I(35,4,2,0);
        a.R(5,0,6,0,0x21);a.R(2,3,2,0,0x25);a.J("durable");a.I(43,4,2,0);
        a.L("dynamic");a.Jal(MapRowAddress);a.I(36,17,6,0x56);a.B(1,6,0,"sound");a.N();
        a.L("durable");a.R(0,6,4,5,3);a.I(12,6,6,31);a.I(15,0,3,0x8007);a.I(35,3,2,0x5964);a.I(9,3,3,0x7908);
        a.R(0,4,4,2,0);a.R(0,2,2,5,0);a.R(2,3,2,0,0x21);a.R(4,2,4,0,0x21);a.Li(2,1);
        a.I(35,4,3,0);a.R(6,2,2,0,4);a.R(3,2,3,0,0x25);a.I(43,4,3,0);
        a.L("sound");a.R(17,0,5,0,0x21);a.I(15,0,2,0x8007);a.I(35,2,2,0x61D4);a.R(0,0,7,0,0x21);a.I(36,2,4,0);a.Jal(0x80055A78);a.Li(6,16);
        a.I(35,29,31,24);a.I(35,29,17,20);a.I(35,29,16,16);a.R(31,0,0,0,8);a.I(9,29,29,32);
        byte[] body=a.Bytes(MarkAddress);int owner=(int)(MapRowAddress-MarkAddress);
        if(body.Length>owner)throw new InvalidDataException($"Native mark routine requires {body.Length}, owner has {owner} bytes.");
        byte[] result=new byte[0x180];body.CopyTo(result,0);GatewayRoutine().CopyTo(result,owner);return result;
    }

    public static byte[] PreInitRoutine()
    {
        var a=new Asm();a.L("loop");a.Jal(MapRowAddress);a.R(17,0,6,0,0x21);a.B(1,6,0,"next");a.R(16,4,4,0,0x21);
        a.I(33,4,2,0x36);a.I(15,0,1,0x8007);a.R(0,2,2,2,0);a.R(1,2,1,0,0x21);a.I(35,1,2,0x6378);a.N();
        a.B(1,2,1,"resident");a.N();a.Jal(0x800526A8);a.N();a.J("position");a.N();a.L("resident");a.Jal(0x800529CC);a.N();
        a.L("position");a.I(15,0,5,0x8007);a.I(35,5,5,0x5828);a.I(9,29,4,16);a.R(5,16,5,0,0x21);a.Jal(0x80017700);a.I(9,5,5,12);
        a.I(9,29,4,16);a.I(35,29,2,24);a.I(15,0,5,1);a.I(9,2,2,1024);a.Jal(0x8004D5EC);a.I(43,29,2,24);
        a.I(15,0,4,0x8007);a.I(35,4,4,0x5828);a.Jal(0x800533D0);a.R(4,16,4,0,0x21);
        a.Jal(MapRowAddress);a.R(17,0,6,0,0x21);a.I(15,0,4,0x8007);a.I(35,4,4,0x5828);a.I(12,6,5,31);a.R(16,4,2,0,0x21);
        a.I(36,2,7,0x3A);a.R(0,6,6,5,3);a.I(12,7,2,0x60);a.B(5,2,0,"next");a.R(0,6,4,2,0);
        a.I(15,0,1,0x8007);a.R(1,4,1,0,0x21);a.I(35,1,2,0x7898);a.R(5,19,5,0,4);a.R(2,5,2,0,0x24);a.B(4,2,0,"include");a.R(7,19,2,0,4);
        a.I(15,0,3,0x8007);a.I(35,3,2,0x5964);a.I(9,3,3,0x7908);a.R(0,2,2,5,0);a.R(2,3,2,0,0x21);a.R(2,4,2,0,0x21);
        a.I(35,2,2,0);a.N();a.R(2,5,2,0,0x24);a.B(5,2,0,"next");a.R(7,19,2,0,4);
        a.L("include");a.R(18,2,18,0,0x25);
        a.L("next");a.I(9,16,16,88);a.I(15,0,4,0x8007);a.I(35,4,4,0x5828);a.N();a.R(16,4,2,0,0x21);a.I(36,2,3,0x48);a.Li(2,255);
        a.B(5,3,2,"loop");a.I(9,17,17,1);
        byte[] code=a.Bytes(PreInitAddress);if(code.Length>0x124)throw new InvalidDataException($"Native constructor pass requires {code.Length}, owner has292 bytes.");
        byte[] result=new byte[0x124];code.CopyTo(result,0);return result;
    }

    public static byte[] CheckpointQueryRoutine()
    {
        var a=new Asm();a.I(9,29,29,-16);a.I(43,29,31,12);a.R(4,0,5,0,0x21);a.I(15,0,2,0x8007);a.I(35,2,2,0x5828);a.Li(3,88);
        a.R(5,2,6,0,0x23);a.R(6,3,0,0,0x1B);a.R(0,0,6,0,0x12);a.Jal(MapRowAddress);a.N();
        a.B(1,6,0,"zero");a.N();a.R(0,6,3,5,3);a.I(12,6,4,31);a.R(0,3,3,2,0);a.I(15,0,1,0x8007);a.R(1,3,1,0,0x21);
        a.I(35,1,3,0x7898);a.Li(2,1);a.R(4,2,2,0,4);a.R(3,2,3,0,0x24);a.B(4,3,0,"done");a.Li(6,0);
        a.I(36,5,2,0x53);a.N();a.I(14,2,2,255);a.J("done");a.I(11,2,6,1);
        a.L("zero");a.Li(6,0);a.L("done");a.I(35,29,31,12);a.R(6,0,2,0,0x21);a.R(31,0,0,0,8);a.I(9,29,29,16);
        byte[] code=a.Bytes(CheckpointQueryAddress);if(code.Length>0x88)throw new InvalidDataException($"Native checkpoint query requires {code.Length}, owner has136 bytes.");
        byte[] result=new byte[0x88];code.CopyTo(result,0);return result;
    }

    public static byte[] DeleteRoutine()
    {
        var a=new Asm();a.I(9,29,29,-16);a.I(43,29,31,12);a.I(43,29,6,8);
        a.I(15,0,3,0x8007);a.I(35,3,2,0x5828);a.I(35,3,3,0x5890);a.R(4,2,2,0,0x2B);
        a.B(5,2,0,"done");a.N();
        a.I(15,0,2,0x8007);a.I(35,2,2,0x5828);a.N();a.R(4,2,6,0,0x23);
        a.R(4,3,2,0,0x2B);a.B(4,2,0,"done");a.Li(2,88);
        a.R(6,2,0,0,0x1B);a.R(0,0,6,0,0x12);a.Jal(MapRowAddress);a.N();
        a.B(1,6,0,"done");a.I(12,6,5,31);a.R(0,6,3,5,3);a.R(0,3,3,2,0);
        a.La(2,0x800778B8);a.R(3,2,3,0,0x21);a.I(35,3,4,0);a.Li(2,1);
        a.R(5,2,2,0,4);a.R(4,2,4,0,0x25);a.I(43,3,4,0);
        a.L("done");a.I(35,29,31,12);a.I(35,29,6,8);a.I(9,29,29,16);a.R(31,0,0,0,8);a.N();
        byte[] code=a.Bytes(DeleteAddress);if(code.Length>0x94)throw new InvalidDataException($"Native deletion marker requires {code.Length}, owner has148 bytes.");
        byte[] result=new byte[0x94];code.CopyTo(result,0);return result;
    }

    public static byte[] ReloadRoutine()
    {
        var a=new Asm();a.Jal(MapRowAddress);a.R(17,0,6,0,0x21);a.B(1,6,0,"free");a.N();
        a.I(12,6,5,31);a.R(0,6,6,5,3);a.I(15,0,3,0x8007);a.I(35,3,2,0x5964);a.I(9,3,3,0x7908);
        a.R(0,6,6,2,0);a.R(0,2,2,5,0);a.R(2,3,2,0,0x21);a.R(6,2,2,0,0x21);a.I(35,2,2,0);
        a.R(5,20,3,0,4);a.R(2,3,2,0,0x24);a.B(4,2,0,"next");a.N();
        a.I(15,0,1,0x8007);a.R(1,6,1,0,0x21);a.I(35,1,2,0x7898);a.N();a.R(2,3,2,0,0x24);
        a.B(5,2,0,"checkpoint");a.R(16,4,4,0,0x21);a.I(36,4,2,0x3A);a.Li(3,0x7D);a.I(12,2,2,127);
        a.B(4,2,3,"unlink");a.N();a.J("next");a.I(40,4,19,0x53);
        a.L("checkpoint");a.I(15,0,2,0x8007);a.I(35,2,2,0x5828);a.N();a.R(16,2,2,0,0x21);a.I(36,2,2,0x3A);a.N();
        a.I(12,2,5,127);a.I(10,5,2,32);a.B(4,2,0,"special");a.R(5,20,2,0,4);a.R(18,2,2,0,0x24);
        a.J("kind");a.R(0,2,2,0,0x2B);a.L("special");a.I(14,5,2,0x7E);a.I(11,2,2,1);
        a.L("kind");a.B(4,2,0,"free");a.Li(3,255);a.I(15,0,2,0x8007);a.I(35,2,2,0x5828);a.N();a.R(16,2,2,0,0x21);
        a.J("next");a.I(40,2,3,0x53);
        a.L("free");a.I(15,0,4,0x8007);a.I(35,4,4,0x5828);a.N();a.R(4,16,4,0,0x21);
        a.L("unlink");a.Jal(0x80052568);a.N();
        a.L("next");a.I(15,0,4,0x8007);a.I(35,4,4,0x5828);a.I(9,16,16,88);a.R(16,4,2,0,0x21);a.I(36,2,2,0x48);a.N();
        a.B(5,2,19,"loop");a.I(9,17,17,1);
        a.L("copy");
        if(a.Part.Items.Count(i=>i.Word!=null)*4 != ReloadCopyAddress-ReloadAddress)throw new InvalidDataException("Empty reload branch must target the exact emitted checkpoint copy entry.");
        a.La(4,0x800778B8);a.I(9,4,5,-32);a.Li(6,32);a.Jal(0x80016958);a.Li(17,8);
        a.J(0x80014404);a.Li(17,9);
        // The physical loop re-enters this relocated owner, preserving s1/s0.
        a.Part.Items.Insert(0,new(null,default,null,"loop"));
        byte[] code=a.Bytes(ReloadAddress);if(code.Length>0x13C)throw new InvalidDataException($"Native reload requires {code.Length}, owner has316 bytes.");
        byte[] result=new byte[0x13C];code.CopyTo(result,0);return result;
    }
}

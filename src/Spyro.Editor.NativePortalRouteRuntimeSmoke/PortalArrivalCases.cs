using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Universal;

internal static class PortalArrivalCases
{
    internal static void Run(byte[] original,byte[] fresh,Action<bool,string> check,Action<Action,string> refuse)
    {
        byte[] routed=fresh.ToArray();NativePortalRouteFreshBinding.PatchOrdinaryPortalArrival(original,routed);
        check(BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(0x5F640))==-1024&&BinaryPrimitives.ReadInt32LittleEndian(routed.AsSpan(0x5F640))==8192,"source flight65 height alone becomes normal Town arrival height");
        check(Enumerable.Range(0,routed.Length).Where(i=>routed[i]!=fresh[i]).All(i=>i>=0x5F640&&i<0x5F644),"arrival changes only owned65 four-byte height");
        byte[] repeated=routed.ToArray();NativePortalRouteFreshBinding.PatchOrdinaryPortalArrival(original,repeated);
        check(repeated.SequenceEqual(routed),"arrival patch is idempotent");
        foreach(int id in Enumerable.Range(10,56).Where(i=>i%10<=5))
        {
            var index=new ScalarMips();index.Segment(UniversalExeBindings.ExeBase,routed);index.Memory[0x8007596C]=(uint)id;index.Memory[0x80075964]=0;
            index.Run(0x800155FC,0x80015664);
            int expected=(id/10-1)*6+id%10;
            check(index.Memory[0x80075964]==expected&&index.Memory[0x8007595C]==id/10-1,"actual native continuous index for arrival "+id);
            foreach(int heading in Enumerable.Range(0,8).Select(h=>h*32))foreach(uint entering in new uint[]{0,1})
            {
                ScalarMips Machine(byte[] exe)
                {
                    var m=new ScalarMips();m.Segment(UniversalExeBindings.ExeBase,exe);
                    m.Memory[0x800756D0]=entering;m.Memory[0x80075964]=(uint)expected;
                    m.Memory[0x800778D8]=100000;m.Memory[0x800778DC]=200000;m.Memory[0x800778E0]=9216;m.Memory[0x800778E4]=(uint)heading;
                    return m;
                }
                var before=Machine(fresh);var after=Machine(routed);before.Run(0x8001399C,0x80013A44);after.Run(0x8001399C,0x80013A44);
                bool changed=id==65&&entering==1;
                check(before.Memory.Keys.All(a=>after.Memory[a]==(changed&&a==0x800778E0?before.Memory[a]+9216:before.Memory[a])),"actual incoming cache effects preserve retail and NewGame "+id+":"+heading+":"+entering);
                check(before.R.SequenceEqual(after.R)&&before.Writes.Select(w=>w.Address).SequenceEqual(after.Writes.Select(w=>w.Address))&&before.PendingLoad==after.PendingLoad,"arrival uses unchanged registers/store ownership "+id+":"+heading+":"+entering);
                if(changed)check(before.Memory[0x800778E0]==8192&&after.Memory[0x800778E0]==17408,"actual65 portal cache starts above fixture floor8704");
            }
        }
        foreach(int offset in new[]{0x419C,0x423C,0x5DFC,0x5E60,0x5F5B4,0x5F63C,0x5F640,0x40AC,0x40C0,0x1CA8})
        {
            byte[] bad=fresh.ToArray();bad[offset]^=1;byte[] before=bad.ToArray();
            refuse(()=>NativePortalRouteFreshBinding.PatchOrdinaryPortalArrival(original,bad),"changed arrival owner refuses "+offset.ToString("X"));
            check(bad.SequenceEqual(before),"arrival refusal is atomic "+offset.ToString("X"));
        }
        refuse(()=>NativePortalRouteFreshBinding.PatchOrdinaryPortalArrival(original,original.ToArray()),"arrival refuses missing fresh bootstrap");
        check(routed.AsSpan(0x40AC,4).SequenceEqual(original.AsSpan(0x40AC,4)),"state12 branch slot unchanged");
        Console.WriteLine("NATIVE_PORTAL_ARRIVAL_OK retailLevels=35 headings=8 entryStates=2 runtime=not-tested");
    }
}

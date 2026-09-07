using System.Text;
using Spyro.Editor.Core.Exporting;

internal static class FreeCases
{
    internal static async Task Run()
    {
        int checks=CueStreamCases.Run(),callbacks=0;
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        foreach(string? output in new string?[]{null,""," ","\0","must-not-touch"})
        foreach(var token in new[]{CancellationToken.None,cancelled.Token})
        {
            foreach(var fault in new[]{NativeId65FreshWizardResearchFault.AfterFirstSector,NativeId65FreshWizardResearchFault.AfterImageWrite,
                NativeId65FreshWizardResearchFault.BeforePublication,(NativeId65FreshWizardResearchFault)(-1),(NativeId65FreshWizardResearchFault)999})
                await NullTask(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishForSmokeAsync(null!,output!,fault,(_,_)=>callbacks++,token));
            Task? task=null;bool threw=false;
            try{task=NativeId65FreshWizardResearchCandidatePublisher.PublishForSmokeAsync(null!,output!,NativeId65FreshWizardResearchFault.None,null,token);}
            catch(InvalidDataException error)when(error.Message=="A Wizard staging exercise must force a precommit fault."){checks++;threw=true;}
            if(!threw){_=task?.Exception;throw new Exception("Forced None must throw synchronously before returning any value.");}
        }
        await NullTask(()=>NativeId65FreshWizardResearchCandidatePublisher.PublishPostcommitForSmokeAsync(null!,"must-not-touch",(_,_)=>callbacks++));
        await NullTask(()=>NativeId65FreshResearchPublicationEngine.PublishWizardPostcommitForSmokeAsync(null!,"must-not-touch",(_,_)=>callbacks++,default));
        Check(callbacks==0,"Refused entries never execute callbacks.");
        foreach(byte[] bytes in new[]{Array.Empty<byte>(),new byte[65537],new byte[]{0xFF},
            Encoding.UTF8.GetBytes("FILE bad.bin BINARY\nTRACK 01 MODE2/2352\nINDEX 01 00:00:00\n"),
            Encoding.UTF8.GetBytes("FILE \"x\" BINARY\nTRACK 02 MODE2/2352\nINDEX 01 00:00:00\n"),
            Encoding.UTF8.GetBytes("FILE \"x\" BINARY\nTRACK 01 MODE1/2048\nINDEX 01 00:00:00\n"),
            Encoding.UTF8.GetBytes("FILE \"x\" BINARY\nTRACK 01 MODE2/2352\nINDEX 01 00:00:01\n")})
            Reject(()=>NativeId65FreshWizardResearchCandidatePlan.ValidateSourceCue(bytes,"must-not-open.cue","must-not-open.bin"));
        byte[] owner="exact companion\n"u8.ToArray();
        using(var input=new MemoryStream(owner,false))
        {NativeId65FreshResearchPublicationEngine.RequireCompanion(input,owner,default);Check(input.Position==owner.Length,"Complete companion accepted.");}
        foreach(var fault in new[]{0,1,2})
        {using var input=new CompanionEnd(owner,fault);Reject(()=>NativeId65FreshResearchPublicationEngine.RequireCompanion(input,owner,default));}
        using(var input=new MemoryStream(owner,false))
        {input.Position=1;Reject(()=>NativeId65FreshResearchPublicationEngine.RequireCompanion(input,owner,default));Check(input.Position==1,"Companion initial cursor is not normalized.");}
        using(var input=new MemoryStream(owner,false))
        {byte[] bad=owner.ToArray();bad[0]^=1;Reject(()=>NativeId65FreshResearchPublicationEngine.RequireCompanion(input,bad,default));}
        Console.WriteLine($"FRESH_WIZARD_RESEARCH_FREE_OK checks={checks} outputsWritten=0");
        void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
        void Reject(Action action){try{action();}catch(InvalidDataException){checks++;return;}throw new Exception("Expected exact bounded InvalidData refusal.");}
        async Task NullTask(Func<Task<NativeId65FreshDiscResearchPublication>> invoke)
        {
            Task<NativeId65FreshDiscResearchPublication> task;
            try{task=invoke();}catch(Exception error){throw new Exception("Null must be a faulted Task.",error);}
            Check(task.IsFaulted&&!task.IsCanceled,"Null precedence remains faulted, not canceled.");
            try{await task;}catch(ArgumentNullException error)when(error.ParamName=="plan"){checks++;return;}
            throw new Exception("Expected actual null-plan rejection.");
        }
    }
    private sealed class CompanionEnd(byte[] bytes,int fault):MemoryStream(bytes,false)
    {
        private bool end;
        public override long Length=>base.Length+(end&&fault==1?1:0);
        public override int ReadByte(){end=true;if(fault==2)Position--;return fault==0?0:-1;}
    }
}

internal static class SeparatedPublicationArgumentCases
{
    internal static async Task Run(Action<bool,string> check)
    {
        foreach(string mode in new[]{"--publish-separated-reviewed","--verify-separated-reviewed"})
        {
            string[] valid=[mode,"source.bin","source.cue","/explicit-parent/output"];
            var request=SeparatedPublicationCases.Parse(valid);
            check(request.Publish==(mode=="--publish-separated-reviewed")&&request.Output==valid[3],"Exactly four tokens select a reviewed separated mode.");
            for(int length=0;length<4;length++)Refuse(valid[..length]);
            Refuse([..valid,"extra"]);
            for(int field=1;field<4;field++)
            foreach(string? bad in new string?[]{null,""," \t\r\n","\0","a\0b","--unknown","--source","--publish-separated-reviewed","--verify-separated-reviewed"})
            {string[] changed=valid.ToArray();changed[field]=bad!;Refuse(changed);}
            foreach(string relative in new[]{"output","./output","../output","~/output"})Refuse([mode,"bin","cue",relative]);
        }
        foreach(string bad in new[]{"--publish-reviewed","--verify-reviewed","--source","--publish-separated-reviewed=x","--Publish-separated-reviewed","--verify-separated-reviewed "})
        {check(!SeparatedPublicationCases.IsReviewedMode([bad]),"Other modes cannot enter reviewed separated dispatch.");Refuse([bad,"bin","cue","/parent/output"]);}
        Refuse(null!);check(!SeparatedPublicationCases.IsReviewedMode([]),"Default cannot dispatch publication.");
        // Neutral finalization mechanics only; no native result or commit is manufactured.
        var primary=new InvalidDataException("simulated primary");var first=new IOException("simulated source final");var last=new OperationCanceledException("simulated close");
        var exact=await Failure(()=>SeparatedPublicationCases.FinishAsync(()=>Task.FromException<string>(primary),[]));
        check(ReferenceEquals(exact,primary),"One failure preserves exact original instance/type.");
        int finals=0;
        var combined=await Failure(()=>SeparatedPublicationCases.FinishAsync(()=>Task.FromException<string>(primary),[
            ()=>{finals++;throw first;},()=>{finals++;},()=>{finals++;throw last;},()=>{finals++;}]));
        check(finals==4&&combined is IOException{InnerException:AggregateException aggregate}&&aggregate.InnerExceptions.Count==3&&
            ReferenceEquals(aggregate.InnerExceptions[0],primary)&&ReferenceEquals(aggregate.InnerExceptions[1],first)&&ReferenceEquals(aggregate.InnerExceptions[2],last),
            "Every final/close attempt runs and multiple failures remain primary-first beneath catchable IOException.");
        var finalOnly=await Failure(()=>SeparatedPublicationCases.FinishAsync(()=>Task.FromResult("must-not-report-success"),[()=>throw first]));
        check(ReferenceEquals(finalOnly,first),"A final error prevents returning the prospective success report.");
        check(await SeparatedPublicationCases.FinishAsync(()=>Task.FromResult("closed"),[()=>finals++])=="closed"&&finals==5,
            "Report is returned only after every successful final action.");
        check(SeparatedPublicationCases.FailureOutcome(combined,false,false)=="verification-failed"&&
            SeparatedPublicationCases.FailureOutcome(combined,false,true)=="not-confirmed", "Ordinary simulated failures never imply a commit.");
        Console.WriteLine("FRESH_WIZARD_SEPARATED_ARGUMENTS_OK sourceIo=0 outputWrites=0 finalizationControls=neutral-simulation");
        void Refuse(string[] args)
        {try{_=SeparatedPublicationCases.Parse(args);}catch(ArgumentException){check(true,"Malformed separated CLI refused before I/O.");return;}throw new InvalidDataException("Malformed separated CLI accepted.");}
    }
    private static async Task<Exception> Failure(Func<Task> action)
    {try{await action();}catch(Exception e){return e;}throw new InvalidDataException("Expected simulated failure.");}
}

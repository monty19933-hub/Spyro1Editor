// Parser controls only: no source/image/file access and no compiler invocation.
internal static class AuthoringSourceArgumentCases
{
    internal static async Task Run(Action<bool,string> check)
    {
        check(AuthoringSourceArguments.Parse([]) == null, "Default selects no source work.");
        Refuse(null!);
        foreach (string mode in new[] { "--source", "--source-issued-matrix" })
        {
            string[] valid = [mode, "not-opened original.bin", "not-opened original.cue"];
            var request = AuthoringSourceArguments.Parse(valid)!;
            check(request.IssuedMatrix == (mode == "--source-issued-matrix") &&
                request.Image == Path.GetFullPath(valid[1]) && request.Cue == Path.GetFullPath(valid[2]),
                "Exact mode selects only its source arm and resolves both paths without opening them.");
            for (int length = 1; length < 3; length++) Refuse(valid[..length]);
            Refuse([..valid, "extra"]);
            Refuse([mode, mode, valid[1], valid[2]]);
            Refuse([mode, "--source", "--source-issued-matrix"]);
            for (int field = 1; field < 3; field++)
            foreach (string? bad in new string?[] { null, "", " \t\r\n", "\0", "a\0b", "--source", "--source-issued-matrix", "--unknown" })
            {
                string[] changed = valid.ToArray(); changed[field] = bad!; Refuse(changed);
            }
            Refuse([mode, valid[1], valid[1]]);
            Refuse([mode, valid[1], Path.GetFullPath(valid[1])]);
        }
        foreach (string mode in new[] { "--Source", "--source-issued-matrix=x", "--source-issued-matrix ", "--unknown",
            "--publish-separated-reviewed", "--verify-separated-reviewed" })
            Refuse([mode, "not-opened.bin", "not-opened.cue"]);

        // Exercise the same neutral fold used by the new matrix arm. These are
        // deliberately synthetic errors, not an executed matrix/native result.
        var primary = new InvalidDataException("matrix action control");
        var binFinal = new IOException("BIN final control");
        var cueFinal = new IOException("CUE final control");
        int actions = 0, finals = 0;
        Exception? caught = null;
        try
        {
            await SeparatedPublicationCases.FinishAsync(() => { actions++; return Task.FromException<string>(primary); },
                [() => { finals++; throw binFinal; }, () => { finals++; throw cueFinal; }]);
        }
        catch (Exception error) { caught = error; }
        check(actions == 1 && finals == 2, "Matrix fold attempts action and both independent final checks exactly once.");
        check(caught is IOException { InnerException: AggregateException aggregate } &&
            aggregate.InnerExceptions.Count == 3 && ReferenceEquals(aggregate.InnerExceptions[0], primary) &&
            ReferenceEquals(aggregate.InnerExceptions[1], binFinal) && ReferenceEquals(aggregate.InnerExceptions[2], cueFinal),
            "Matrix fold preserves actual control exception identities in primary, BIN-final, CUE-final order.");
        Console.WriteLine("FRESH_WIZARD_AUTHORING_ARGUMENTS_OK sourceIo=0 outputWrites=0");

        void Refuse(string[] args)
        {
            try { _ = AuthoringSourceArguments.Parse(args); }
            catch (ArgumentException) { check(true, "Malformed source arguments refused before I/O."); return; }
            throw new InvalidDataException("Malformed source arguments accepted.");
        }
    }
}

if (args.Length != 0) throw new ArgumentException("API-only probe accepts no source or output arguments.");
int checks = 0;
ApiCases.Free((ok, text) => { if (!ok) throw new InvalidDataException(text); checks++; });
Console.WriteLine($"NATIVE_ID65_TEXTURED_DISC_API_OK checks={checks} sourceIo=0 outputWrites=0");

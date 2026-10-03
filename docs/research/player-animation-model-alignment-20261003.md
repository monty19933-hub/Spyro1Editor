# Player animation insertion preserves native model alignment

Importing an original animation whose record size is four modulo eight previously shifted every later native model root by four. Complete model bytes were retained, but this broke the original Town0E/0F/10 replacement models’ required alignment and blocked cleared Haunted exports.

The authenticated shared producer now retains every original record and exact negative-one terminator, issuing only zero or four zero bytes after that terminator. Net insertion growth preserves all later roots modulo eight. A second odd-size insertion consumes the previously issued gap. Public stream decoding remains strict: extra, nonzero, truncated and mutated suffixes refuse.

The complete original native parser80013230..800133E0 is authenticated. Root tests in work/universal-finish-20261002/root-player-alignment-r1 pass763 source controls across35 levels/181 records,10925 cleared Haunted composition checks across33 cases, and22019 native-parser checks executing4288 original instructions. Parser hydration, tables and step count are identical with and without the gap; no instruction reads or writes the gap. Complete fallback models, E-to-F read-ahead and all retained model roots are verified.

This changes ordinary export identity where alignment is needed, including the original Dark Hollow missing hurt8 record (15900 original bytes plus4 alignment bytes). Previous native evidence remains attached to its old candidate; rebuilt candidates require new gameplay checks. No fallback guard was weakened, animation record shortened, model payload dropped, live RAM written or save state used. Root cleared/treasure integration and rebuilt fairy gameplay are checked separately.

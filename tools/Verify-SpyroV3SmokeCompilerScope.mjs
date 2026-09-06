// Checkpoint-specific exact-source regression; reads Git and source only.
// No .NET compilation, fixture execution, or writes. See the compiler report.
import fs from 'node:fs';
import crypto from 'node:crypto';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const repo = fileURLToPath(new URL('../', import.meta.url));
const relative = 'src/Spyro.Editor.Id65V3RuntimeEvidenceContractSmoke/Program.cs';
const revision = 'b50c9a83f03175c77979af339595e1e48ecf0f23';
const baselineSha256 = '626370c366864367576592167e619b0ffe54841f87e4dc085aae686ed6c36ebc';
const hash = s => crypto.createHash('sha256').update(s).digest('hex');
const source = execFileSync('git', ['show', `${revision}:${relative}`],
  {cwd: repo, encoding: 'utf8', maxBuffer: 1024 * 1024});
assert.equal(hash(source), baselineSha256);
const currentSource = fs.readFileSync(`${repo}/${relative}`, 'utf8');

const markers = [
  'string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));',
  'static JsonObject CaptureEvent(JsonObject root, string eventType, int occurrence = 0) =>',
  'void RejectCopiedVisualBytesAcrossSessions(',
  'static Id65V3RuntimeEvidencePackage ReplaceArtifactMetadata(',
  'void RejectContract(string name, string json)',
  'static Id65V3RuntimeEvidenceSession SyntheticSession(',
  'string WatcherLog(IEnumerable<string> samples, string promptSeparator) =>',
  'static string Sample(',
  'internal sealed record CompleteFixture('
];
function once(text, marker) {
  const at = text.indexOf(marker);
  assert(at >= 0 && text.indexOf(marker, at + marker.length) < 0, `Missing/duplicate ${marker}`);
  return at;
}
function partition(text) {
  const positions = markers.map(m => once(text, m));
  assert(positions.every((v, i) => !i || v > positions[i - 1]));
  return [text.slice(0, positions[0]), ...positions.map((v, i) => text.slice(v, positions[i + 1]))];
}
const [preamble, mainWithConstants, helperA, copiedReject, helperB,
  tailRejects, helperC, watcher, helperD, globalTypes] = partition(source);
const constantStart = once(mainWithConstants, 'const string Warning =');
const constantEnd = once(mainWithConstants, 'string[] stableLines =');
const constants = mainWithConstants.slice(constantStart, constantEnd);
assert.deepEqual([...constants.matchAll(/^const string (\w+)/gm)].map(m => m[1]),
  ['Warning', 'ReadOnly', 'Connected', 'Prompt', 'Watching', 'Stopped']);
const main = mainWithConstants.slice(0, constantStart) + mainWithConstants.slice(constantEnd);
assert.equal((source.match(/^static /gm) || []).length, 44);
assert(!/\b(?:await|async)\b/.test(source), 'Sync-only entry assumption changed');
assert(!/GetCurrentMethod|StackTrace|CallerMemberName|CallerLineNumber|nameof\(/.test(source));
const builder = 'CompleteFixture BuildCompleteFixture(';
once(helperB, builder);
const open = 'internal static class Program\n{\n';
const openMain = 'private static void Main(string[] args)\n{\n';
const close = '}\n\n';
const memberBlocks = [helperA, helperB.replace(builder, 'static ' + builder), helperC,
  'static ' + watcher, helperD];
const expected = preamble + open + constants + openMain + main + copiedReject + tailRejects + close +
  memberBlocks.join('') + close + globalTypes;

function verify(actual) {
  // Exact reconstructed source equality, not an approximate regex rewrite, is
  // authoritative. No instruction, string, body, optional default or order of
  // executable statements may change. Only whole declarations are relocated.
  assert.equal(hash(actual), hash(expected), 'Change exceeds exact source-derived scope permutation');
  assert.equal(actual, expected);
  assert.equal(actual.slice(0, preamble.length), preamble);
  assert(actual.endsWith(close + globalTypes), 'Global types became nested');
  assert.equal((actual.match(/^static /gm) || []).length, 46);
  assert.equal((actual.match(/private static void Main\(string\[\] args\)/g) || []).length, 1);
  assert.equal((actual.match(/\[DllImport\(/g) || []).length, 1);
  const recoveredMain = main.slice(0, constantStart) + constants + main.slice(constantStart);
  const recovered = preamble + recoveredMain + helperA + copiedReject + helperB + tailRejects +
    helperC + watcher + helperD + globalTypes;
  assert.equal(recovered, source, 'Inverse permutation did not recover every original byte');
  assert.equal(hash(recovered), baselineSha256);
  const originalLocalRejects = (copiedReject + tailRejects).match(/^void /gm) || [];
  assert.equal(originalLocalRejects.length, 5);
  assert.equal((memberBlocks.join('').match(/^static /gm) || []).length, 46);
  assert(!memberBlocks.join('').includes('void RejectContract('));
  assert.equal((actual.match(/ID65_V3_RUNTIME_EVIDENCE_CONTRACT_SMOKE_OK/g) || []).length, 1);
  return {baselineSha256, proposedSha256: hash(actual), originalBytes: Buffer.byteLength(source),
    proposedBytes: Buffer.byteLength(actual), existingStaticHelpers: 44, proposedStaticMembers: 46,
    relocatedCapturingHelpers: 5, watcherConstants: 6,
    unchangedGlobalTypesSha256: hash(globalTypes), unchangedMainWithCapturesSha256: hash(main),
    copiedRejectSha256: hash(copiedReject), tailRejectsSha256: hash(tailRejects),
    originalBuilderBlockSha256: hash(helperB),
    note: 'This source-only gate does not establish C# name binding, compiled metadata, compilation or runtime acceptance.'};
}
const result = verify(expected);
let negativeFixtures = 0;
function reject(before, after) {
  assert(expected.includes(before), 'Negative fixture misses original token');
  assert.throws(() => verify(expected.replace(before, after)));
  negativeFixtures++;
}
reject('private static void Main', 'private static async Task Main');
reject('static CompleteFixture BuildCompleteFixture(', 'CompleteFixture BuildCompleteFixture(');
reject('static CompleteFixture BuildCompleteFixture(', 'public static CompleteFixture BuildCompleteFixture(');
reject('static string WatcherLog(', 'string WatcherLog(');
reject('primaryRecordingStartMilliseconds = 3_500', 'primaryRecordingStartMilliseconds = 3_501');
reject('fullPassTamperRejects == 121', 'fullPassTamperRejects == 120');
reject('captureJournalTamperRejects++;', 'captureJournalTamperRejects += 2;');
reject('ID65_V3_RUNTIME_EVIDENCE_CONTRACT_SMOKE_OK ', 'ID65_V3_RUNTIME_EVIDENCE_CONTRACT_WRONG ');
reject('EntryPoint = "link"', 'EntryPoint = "unlink"');
reject('const string Warning =', 'static string Warning =');
reject('if (!OperatingSystem.IsMacOS())', 'if (false)');
reject(close + helperA, helperA);
reject(close + globalTypes, globalTypes);
reject('Directory.Delete(fixtureRoot, recursive: true);', 'Directory.Delete(repositoryRoot, recursive: true);');
assert.equal(process.argv.length, 2, 'This checkpoint gate takes no arguments');
verify(currentSource);
console.log(JSON.stringify({marker: 'V3_COMPILER_SCOPE_SOURCE_EQUIVALENCE_OK', ...result,
  negativeFixtures, trackedSourceMatchesScopedVariant: true, buildsRun: false, fixturesExecuted: false}, null, 2));

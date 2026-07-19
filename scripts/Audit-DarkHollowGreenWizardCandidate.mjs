#!/usr/bin/env node

import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";

const EXPECTED_IMAGE_SHA256 = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
const EXPECTED_OVERLAY_SHA256 = "9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32";
const EXPECTED_DATA_SHA256 = "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97";
const EXPECTED_SCENE_SHA256 = "c48c344d3809f1ea5761e968ce6f18b02d5c7bbb82fcec4a7fd453343dbb84f8";
const EXPECTED_EXE_SHA256 = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";

const WAD_LBA = 37;
const DARK_OVERLAY_ENTRY = 13;
const DARK_DATA_ENTRY = 14;
const WIZARD_OVERLAY_ENTRY = 39;
const WIZARD_DATA_ENTRY = 40;
const OVERLAY_LOAD_ADDRESS = 0x8007aa38;
const LOWER_POLYGON_BUFFER_ADDRESS = 0x80187bb0;
const DARK_LOAD_LEVEL_DATA_CONSUMED_BYTES = 0x38750;
const RECORD_BYTES = 0x58;
const SOURCE_TABLE_DATA_OFFSET = 0x19cc38;
const SOURCE_RECORD_COUNT = 128;
const TARGET_TRUE_INDEX = 48;
const PROPERTIES_SCRATCH_SCENE_OFFSET = 0xb838;
const PROPERTIES_BYTES = 0x50;
const POINTER_FIXUP_LIST_SCENE_OFFSET = 0x10ef0;
const POINTER_FIXUP_COUNT = 147;

const BUNDLE = {
  id: "spyro1.green-wizard.runtime-bundle.v11",
  provenRecipeId:
    "toasty.wizardpeak.greenWizard.compactDependencyBundle.propsFixup.impactBurstQuarantine.genericHurtDamage.textureDependencies.lightningTrailParticleTexture.v11",
  provenOutputSha256: "e2ec0b112c9f7782ec85e66118cf157550938e9c49146339df6d1000ffbff5e7",
  compactOverlayBytes: 0xd108,
  particleBundleBytes: 0x380,
  shimBytes: 0xa0,
  wizardPackageOffset: 0x19b518,
  wizardPackageBytes: 0x35ac,
  wizardPackageSha256: "24df9ae279f32ba10a8e3bd25880d6989991b9225acf45755e6aaf985d66f529",
  lightningPackageOffset: 0x1cac88,
  lightningPackageBytes: 0x564,
  lightningPackageSha256: "9ea5c72ba675e9c6a81433f7408695399e19bc188b4b2a923069d71de601789a",
  compactOverlaySha256: "6cc3fc694ebefc487e0609c7ef86155ad880910b2e56c6873707c6c44f4d7a25",
  particleBundleSha256: "eaab7adf4f1d5657c9d1ed66a5e944b007a560b5cebb3516705ee6cda62b38fc",
  textureAggregateSha256: "9448477af6f60a253f43c49e0642f8ce406027ff811dd37b41a4b1d1f4efbfc7",
  trailDescriptorOffset: 0x173518,
  trailDescriptorBytes: 0x0c,
  trailDescriptorSha256: "4baed93eb98ac71d27f59e3ddf8b5382dda7e74319de188749dce3831397f7b3",
};

const TEXTURES = [
  {
    id: "wizard-accent",
    label: "Wizard accent",
    targetPixelOffset: 0x50900,
    donorPixelOffset: 0x50900,
    rows: 32,
    rowBytes: 0x10,
    stride: 0x400,
    targetClutOffset: 0x7a8e0,
    donorClutOffset: 0x7a8e0,
    clutBytes: 0x20,
    donorPixelsSha256: "8ff165f1d889594f9221913fdfa69f471eb33c57ea0780be4b1ddf8f22317c12",
    donorClutSha256: "60a0eb6d64d068d96e76de01db8bf4b942d0771fefbef6baed7d2964940522af",
  },
  {
    id: "wizard-special",
    label: "Wizard special",
    targetPixelOffset: 0x58910,
    donorPixelOffset: 0x58910,
    rows: 32,
    rowBytes: 0x10,
    stride: 0x400,
    targetClutOffset: 0x708a0,
    donorClutOffset: 0x708a0,
    clutBytes: 0x20,
    donorPixelsSha256: "2229d62ddadd04752fcc5790b019b55d166b774007ab646e46f1d7a440069dea",
    donorClutSha256: "5cb82309aae7685152d17f22df8e9e8a55d5202b449adf08f580a254038798a1",
  },
  {
    id: "wizard-main",
    label: "Wizard main",
    targetPixelOffset: 0x58960,
    donorPixelOffset: 0x58960,
    rows: 32,
    rowBytes: 0x10,
    stride: 0x400,
    targetClutOffset: 0x72980,
    donorClutOffset: 0x72980,
    clutBytes: 0x20,
    donorPixelsSha256: "6832e7f686f47f7224b1e7b1bb136885d2326650bde4eaf99cffce3c74dba445",
    donorClutSha256: "98c5ea6b2eaeeb703c65f10e6ba11e9f2d5b4272a4bbc261d00ef178cfd397ba",
  },
  {
    id: "lightning",
    label: "Lightning",
    targetPixelOffset: 0x70850,
    donorPixelOffset: 0x70850,
    rows: 4,
    rowBytes: 0x02,
    stride: 0x400,
    targetClutOffset: 0x78840,
    donorClutOffset: 0x78840,
    clutBytes: 0x20,
    donorPixelsSha256: "e6f48a0036f29213687545ad901eb55949d15e150213f2db8b32f248d55ec411",
    donorClutSha256: "ab654734be1754fd47a82b2974389dac5b3bcca0e0015604e6724b56d1492d00",
  },
  {
    id: "lightning-trail-particle-07",
    label: "Lightning trail particle 0x07",
    targetPixelOffset: 0x78ab0,
    donorPixelOffset: 0x78810,
    rows: 32,
    rowBytes: 0x10,
    stride: 0x400,
    targetClutOffset: 0x34c60,
    donorClutOffset: 0x759a0,
    clutBytes: 0x20,
    donorPixelsSha256: "31f14b4cd1994f468e2bd1d9d1937ca4e7bccd276cd32efa37ecfba4ecb92249",
    donorClutSha256: "c8c516666d7b96b4039b451cffcf69e8c610a93a112532859932befbd2ea9384",
  },
];

function parseArguments(argv) {
  const result = {};
  for (let index = 0; index < argv.length; index += 2) {
    const key = argv[index];
    const value = argv[index + 1];
    if (!key?.startsWith("--") || value == null) usage();
    result[key.slice(2)] = value;
  }
  if (!result.image || !result.analysis || !result.output) usage();
  return result;
}

function usage() {
  console.error(
    "Usage: node scripts/Audit-DarkHollowGreenWizardCandidate.mjs " +
      "--image <clean Spyro BIN> --analysis <spyro-wad-analysis.json> --output <report.json>",
  );
  process.exit(2);
}

function invariant(condition, message) {
  if (!condition) throw new Error(message);
}

function hex(value, width = 0) {
  return `0x${value.toString(16).toUpperCase().padStart(width, "0")}`;
}

function sha256(bytes) {
  return crypto.createHash("sha256").update(bytes).digest("hex");
}

async function sha256File(filePath) {
  const hash = crypto.createHash("sha256");
  await new Promise((resolve, reject) => {
    const stream = fs.createReadStream(filePath);
    stream.on("data", (chunk) => hash.update(chunk));
    stream.on("error", reject);
    stream.on("end", resolve);
  });
  return hash.digest("hex");
}

function detectDiscLayout(fd) {
  for (const [sectorBytes, userOffset] of [
    [2048, 0],
    [2352, 24],
    [2336, 8],
  ]) {
    const pvd = Buffer.alloc(2048);
    fs.readSync(fd, pvd, 0, pvd.length, 16 * sectorBytes + userOffset);
    if (pvd[0] === 1 && pvd.subarray(1, 6).toString("ascii") === "CD001") {
      return {
        sectorBytes,
        userOffset,
        rootExtent: pvd.readUInt32LE(158),
        rootLength: pvd.readUInt32LE(166),
      };
    }
  }
  throw new Error("ISO9660 primary volume descriptor was not found.");
}

function readLogical(fd, layout, fileLba, fileOffset, length) {
  const result = Buffer.alloc(length);
  let written = 0;
  while (written < length) {
    const absolute = fileOffset + written;
    const sectorOffset = absolute % 2048;
    const sector = fileLba + Math.floor(absolute / 2048);
    const count = Math.min(2048 - sectorOffset, length - written);
    const read = fs.readSync(
      fd,
      result,
      written,
      count,
      sector * layout.sectorBytes + layout.userOffset + sectorOffset,
    );
    invariant(read === count, "Unexpected end of disc image.");
    written += count;
  }
  return result;
}

function readRootFiles(fd, layout) {
  const directory = readLogical(fd, layout, layout.rootExtent, 0, layout.rootLength);
  const result = [];
  for (let offset = 0; offset < directory.length; ) {
    const recordBytes = directory[offset];
    if (recordBytes === 0) {
      offset = (Math.floor(offset / 2048) + 1) * 2048;
      continue;
    }
    if (recordBytes < 34 || offset + recordBytes > directory.length) break;
    const nameBytes = directory[offset + 32];
    const name = directory
      .subarray(offset + 33, offset + 33 + nameBytes)
      .toString("ascii")
      .replace(";1", "");
    if (name !== "\0" && name !== "\u0001") {
      result.push({
        name,
        lba: directory.readUInt32LE(offset + 2),
        bytes: directory.readUInt32LE(offset + 10),
      });
    }
    offset += recordBytes;
  }
  return result;
}

function zeroRuns(bytes, minimumBytes = 1) {
  const result = [];
  for (let start = 0; start < bytes.length; ) {
    if (bytes[start] !== 0) {
      start++;
      continue;
    }
    let end = start + 1;
    while (end < bytes.length && bytes[end] === 0) end++;
    if (end - start >= minimumBytes) result.push({ start, end, bytes: end - start });
    start = end;
  }
  return result;
}

function stridedRows(bytes, offset, rows, rowBytes, stride) {
  const result = Buffer.alloc(rows * rowBytes);
  for (let row = 0; row < rows; row++) {
    bytes.copy(result, row * rowBytes, offset + row * stride, offset + row * stride + rowBytes);
  }
  return result;
}

function countNonZero(bytes) {
  let result = 0;
  for (const value of bytes) if (value !== 0) result++;
  return result;
}

function align(value, alignment) {
  return Math.ceil(value / alignment) * alignment;
}

function decodeBranchTarget(instructionAddress, word) {
  const displacement = (word << 16) >> 16;
  return instructionAddress + 4 + displacement * 4;
}

function findAddressPairs(bytes, address) {
  const expectedHigh = address >>> 16;
  const expectedLow = address & 0xffff;
  const result = [];
  for (let offset = 0; offset <= bytes.length - 8; offset += 4) {
    const highWord = bytes.readUInt32LE(offset);
    const lowWord = bytes.readUInt32LE(offset + 4);
    const highOpcode = highWord >>> 26;
    const lowOpcode = lowWord >>> 26;
    const highRegister = (highWord >>> 16) & 31;
    const lowBase = (lowWord >>> 21) & 31;
    const lowRegister = (lowWord >>> 16) & 31;
    if (
      highOpcode === 15 &&
      (highWord & 0xffff) === expectedHigh &&
      [8, 9, 13].includes(lowOpcode) &&
      (lowWord & 0xffff) === expectedLow &&
      highRegister === lowBase &&
      highRegister === lowRegister
    ) {
      result.push({ offset, highWord, lowWord });
    }
  }
  return result;
}

const args = parseArguments(process.argv.slice(2));
const imagePath = path.resolve(args.image);
const analysisPath = path.resolve(args.analysis);
const outputPath = path.resolve(args.output);
const analysis = JSON.parse(fs.readFileSync(analysisPath, "utf8").replace(/^\uFEFF/, ""));
const imageSha256 = await sha256File(imagePath);
invariant(
  imageSha256 === EXPECTED_IMAGE_SHA256,
  `Clean image SHA-256 changed: expected ${EXPECTED_IMAGE_SHA256}, found ${imageSha256}.`,
);

const fd = fs.openSync(imagePath, "r");
try {
  const layout = detectDiscLayout(fd);
  invariant(layout.sectorBytes === 2352 && layout.userOffset === 24, "Expected a MODE2/2352 BIN.");
  const rootFiles = readRootFiles(fd, layout);
  const wadFile = rootFiles.find((file) => file.name.toUpperCase() === "WAD.WAD");
  const executableFile = rootFiles.find((file) => /^(SCUS|SCES|SLES)_/i.test(file.name));
  invariant(wadFile?.lba === WAD_LBA, "WAD.WAD must begin at LBA 37.");
  invariant(executableFile, "PlayStation executable was not found in the ISO root.");
  invariant(analysis.wad.lba === wadFile.lba && analysis.wad.size === wadFile.bytes, "WAD analysis root extent changed.");

  const entries = new Map(analysis.entries.map((entry) => [entry.index, entry]));
  const darkOverlayEntry = entries.get(DARK_OVERLAY_ENTRY);
  const darkDataEntry = entries.get(DARK_DATA_ENTRY);
  const wizardOverlayEntry = entries.get(WIZARD_OVERLAY_ENTRY);
  const wizardDataEntry = entries.get(WIZARD_DATA_ENTRY);
  invariant(darkOverlayEntry?.offset === 0xef6000 && darkOverlayEntry.size === 0xb000, "Dark Hollow overlay entry changed.");
  invariant(darkDataEntry?.offset === 0xf01000 && darkDataEntry.size === 0x28d800, "Dark Hollow data entry changed.");
  invariant(wizardOverlayEntry?.offset === 0x2f5b000 && wizardOverlayEntry.size === 0x10000, "Wizard Peak overlay entry changed.");
  invariant(wizardDataEntry?.offset === 0x2f6b000 && wizardDataEntry.size === 0x28a000, "Wizard Peak data entry changed.");

  const darkOverlay = readLogical(fd, layout, WAD_LBA, darkOverlayEntry.offset, darkOverlayEntry.size);
  const darkData = readLogical(fd, layout, WAD_LBA, darkDataEntry.offset, darkDataEntry.size);
  const wizardOverlay = readLogical(fd, layout, WAD_LBA, wizardOverlayEntry.offset, wizardOverlayEntry.size);
  const wizardData = readLogical(fd, layout, WAD_LBA, wizardDataEntry.offset, wizardDataEntry.size);
  const executable = readLogical(fd, layout, executableFile.lba, 0, executableFile.bytes);
  invariant(sha256(darkOverlay) === EXPECTED_OVERLAY_SHA256, "Dark Hollow overlay hash changed.");
  invariant(sha256(darkData) === EXPECTED_DATA_SHA256, "Dark Hollow data hash changed.");
  invariant(sha256(executable) === EXPECTED_EXE_SHA256, "Executable hash changed.");

  const dataSubfiles = [];
  for (let offset = 0; offset < 0x38; offset += 8) {
    dataSubfiles.push({
      index: offset / 8,
      offset: darkData.readUInt32LE(offset),
      bytes: darkData.readUInt32LE(offset + 4),
    });
  }
  const textureSubfile = dataSubfiles[0];
  const loadLevelDataSubfile = dataSubfiles[1];
  const modelSubfile = dataSubfiles[2];
  const sceneSubfile = dataSubfiles[3];
  invariant(
    textureSubfile.offset === 0x800 && textureSubfile.bytes === 0xf0800 &&
      loadLevelDataSubfile.offset === 0xf1000 && loadLevelDataSubfile.bytes === 0x38800 &&
      modelSubfile.offset === 0x129800 && modelSubfile.bytes === 0x6a800 &&
      sceneSubfile.offset === 0x194000 && sceneSubfile.bytes === 0x11800,
    "Dark Hollow nested data layout changed.",
  );
  const scene = darkData.subarray(sceneSubfile.offset, sceneSubfile.offset + sceneSubfile.bytes);
  invariant(sha256(scene) === EXPECTED_SCENE_SHA256, "Dark Hollow scene hash changed.");

  const dispatchHookOffset = 0x4ec;
  const dispatchHookAddress = OVERLAY_LOAD_ADDRESS + dispatchHookOffset;
  const dispatchWords = [0, 4, 8].map((delta) => darkOverlay.readUInt32LE(dispatchHookOffset + delta));
  invariant(
    dispatchWords[0] === 0x240200fb && dispatchWords[1] === 0x106218f4 && dispatchWords[2] === 0x286200fc,
    "Dark Hollow dispatch hook preimage changed.",
  );
  invariant(darkOverlay.readUInt32LE(0x4dc) === 0x86630036, "Dark Hollow current-Moby register evidence changed.");
  const updateTableOffset = 0x13c;
  const spawnTableOffset = 0x280;
  invariant(darkOverlay.readUInt32LE(0x158) === 0x80084450, "Particle update type 0x07 slot changed.");
  invariant(darkOverlay.readUInt32LE(0x240) === 0x80084450, "Particle update type 0x41 slot changed.");
  invariant(darkOverlay.readUInt32LE(0x29c) === 0x8008553c, "Particle spawn type 0x07 slot changed.");
  invariant(darkOverlay.readUInt32LE(0x384) === 0x8008553c, "Particle spawn type 0x41 slot changed.");

  const compactOverlay = wizardOverlay.subarray(0, BUNDLE.compactOverlayBytes);
  const wizardPackage = wizardData.subarray(
    BUNDLE.wizardPackageOffset,
    BUNDLE.wizardPackageOffset + BUNDLE.wizardPackageBytes,
  );
  const lightningPackage = wizardData.subarray(
    BUNDLE.lightningPackageOffset,
    BUNDLE.lightningPackageOffset + BUNDLE.lightningPackageBytes,
  );
  const trailDescriptor = wizardData.subarray(
    BUNDLE.trailDescriptorOffset,
    BUNDLE.trailDescriptorOffset + BUNDLE.trailDescriptorBytes,
  );
  invariant(sha256(compactOverlay) === BUNDLE.compactOverlaySha256, "Wizard compact overlay hash changed.");
  invariant(sha256(wizardPackage) === BUNDLE.wizardPackageSha256, "Wizard actor package hash changed.");
  invariant(sha256(lightningPackage) === BUNDLE.lightningPackageSha256, "Lightning actor package hash changed.");
  invariant(sha256(trailDescriptor) === BUNDLE.trailDescriptorSha256, "Trail descriptor hash changed.");

  const targetTextureAggregate = [];
  const donorTextureAggregate = [];
  const textureEvidence = TEXTURES.map((region) => {
    const targetPixels = stridedRows(
      darkData,
      region.targetPixelOffset,
      region.rows,
      region.rowBytes,
      region.stride,
    );
    const donorPixels = stridedRows(
      wizardData,
      region.donorPixelOffset,
      region.rows,
      region.rowBytes,
      region.stride,
    );
    const targetClut = darkData.subarray(region.targetClutOffset, region.targetClutOffset + region.clutBytes);
    const donorClut = wizardData.subarray(region.donorClutOffset, region.donorClutOffset + region.clutBytes);
    invariant(sha256(donorPixels) === region.donorPixelsSha256, `${region.label} donor pixels changed.`);
    invariant(sha256(donorClut) === region.donorClutSha256, `${region.label} donor CLUT changed.`);
    targetTextureAggregate.push(targetPixels, targetClut);
    donorTextureAggregate.push(donorPixels, donorClut);
    const targetPixelNonZeroBytes = countNonZero(targetPixels);
    const targetClutNonZeroBytes = countNonZero(targetClut);
    return {
      id: region.id,
      label: region.label,
      targetPixelOffset: hex(region.targetPixelOffset),
      donorPixelOffset: hex(region.donorPixelOffset),
      pixelRows: region.rows,
      pixelRowBytes: region.rowBytes,
      pixelStrideBytes: region.stride,
      targetPixelSha256: sha256(targetPixels),
      donorPixelSha256: sha256(donorPixels),
      targetPixelNonZeroBytes,
      targetClutOffset: hex(region.targetClutOffset),
      donorClutOffset: hex(region.donorClutOffset),
      clutBytes: region.clutBytes,
      targetClutSha256: sha256(targetClut),
      donorClutSha256: sha256(donorClut),
      targetClutNonZeroBytes,
      collidesAtProvenCoordinates: targetPixelNonZeroBytes > 0 || targetClutNonZeroBytes > 0,
    };
  });
  const donorTextureAggregateBytes = Buffer.concat(donorTextureAggregate);
  const targetTextureAggregateBytes = Buffer.concat(targetTextureAggregate);
  invariant(
    donorTextureAggregateBytes.length === 2216 && sha256(donorTextureAggregateBytes) === BUNDLE.textureAggregateSha256,
    "Wizard texture dependency aggregate changed.",
  );
  invariant(textureEvidence.every((region) => region.collidesAtProvenCoordinates), "Expected all five target texture regions to collide.");

  const modelBytes = darkData.subarray(modelSubfile.offset, modelSubfile.offset + modelSubfile.bytes);
  const modelZeroRuns = zeroRuns(modelBytes, 0x20).sort((left, right) => right.bytes - left.bytes);
  invariant(modelZeroRuns[0]?.start === 0x6a5ac && modelZeroRuns[0]?.bytes === 0x254, "Dark Hollow model tail changed.");

  const roots = [];
  for (let slotOffset = 0x54; slotOffset < 0xdc; slotOffset += 4) {
    roots.push({ slotOffset, rootOffset: darkData.readUInt32LE(slotOffset) });
  }
  invariant(roots.length === 34 && darkData.readUInt32LE(0xdc) === 0, "Dark Hollow actor-root table changed.");
  const actorIdTableOffset = 0x152;
  const actorCounts = new Map();
  const sourceTableSceneOffset = SOURCE_TABLE_DATA_OFFSET - sceneSubfile.offset;
  invariant(sourceTableSceneOffset === 0x8c38, "Dark Hollow source-table scene offset changed.");
  for (let index = 0; index < SOURCE_RECORD_COUNT; index++) {
    const recordOffset = sourceTableSceneOffset + index * RECORD_BYTES;
    const actorId = scene.readUInt16LE(recordOffset + 0x36);
    actorCounts.set(actorId, (actorCounts.get(actorId) ?? 0) + 1);
  }
  const actorRoots = roots.map((root, index) => {
    const actorId = darkData.readUInt16LE(actorIdTableOffset + index * 2);
    const endOffset = roots[index + 1]?.rootOffset ?? modelSubfile.offset + modelSubfile.bytes;
    return {
      actorId: hex(actorId, 4),
      rootSlotOffset: hex(root.slotOffset),
      rootOffset: hex(root.rootOffset),
      packageSpanBytes: endOffset - root.rootOffset,
      sourceInstanceCount: actorCounts.get(actorId) ?? 0,
    };
  });
  const reusableLargeRoots = actorRoots.filter(
    (root) => root.packageSpanBytes >= BUNDLE.wizardPackageBytes && root.sourceInstanceCount === 0,
  );
  invariant(reusableLargeRoots.length === 0, "A previously unknown unused large actor root is now available.");

  const targetRecordSceneOffset = sourceTableSceneOffset + TARGET_TRUE_INDEX * RECORD_BYTES;
  const targetRecord = scene.subarray(targetRecordSceneOffset, targetRecordSceneOffset + RECORD_BYTES);
  invariant(targetRecord.readUInt16LE(0x36) === 0x00a6, "T48 is no longer the Big Armor Gnorc candidate.");
  invariant(targetRecord[0x53] === 0x55, "T48 reward changed.");
  const propertiesScratch = scene.subarray(
    PROPERTIES_SCRATCH_SCENE_OFFSET,
    PROPERTIES_SCRATCH_SCENE_OFFSET + PROPERTIES_BYTES,
  );
  invariant(countNonZero(propertiesScratch) === 0, "Dark Hollow Wizard properties scratch changed.");
  const fixupBytes = scene.subarray(
    POINTER_FIXUP_LIST_SCENE_OFFSET,
    POINTER_FIXUP_LIST_SCENE_OFFSET + 4 + POINTER_FIXUP_COUNT * 4,
  );
  invariant(fixupBytes.readUInt32LE(0) === POINTER_FIXUP_COUNT, "Dark Hollow pointer-fixup count changed.");
  const fixups = [];
  for (let index = 0; index < POINTER_FIXUP_COUNT; index++) fixups.push(fixupBytes.readUInt32LE(4 + index * 4));
  invariant(fixups.includes(targetRecordSceneOffset), "T48 pointer field is absent from the fixup list.");
  invariant(scene.readUInt32LE(POINTER_FIXUP_LIST_SCENE_OFFSET + fixupBytes.length) === 0, "Pointer-fixup append word changed.");

  const copyBufferPairs = findAddressPairs(executable, 0x80085594);
  invariant(
    copyBufferPairs.length === 1 && copyBufferPairs[0].offset === 0x4b048,
    "Dark Hollow executable copy-buffer address pair changed.",
  );
  const nextRootFile = rootFiles
    .filter((file) => file.lba > executableFile.lba)
    .sort((left, right) => left.lba - right.lba)[0];
  const executableSectors = Math.ceil(executableFile.bytes / 2048);
  const availableIsoGrowthBytes = (nextRootFile.lba - executableFile.lba - executableSectors) * 2048;
  const expandedOverlayBytes = align(
    darkOverlay.length + BUNDLE.compactOverlayBytes + BUNDLE.particleBundleBytes + BUNDLE.shimBytes,
    2048,
  );
  const paddingBytes =
    expandedOverlayBytes -
    darkOverlay.length -
    BUNDLE.compactOverlayBytes -
    BUNDLE.particleBundleBytes -
    BUNDLE.shimBytes;
  const relocatedCopyBufferAddress = OVERLAY_LOAD_ADDRESS + expandedOverlayBytes;
  const postCopyBufferFootprintBytes =
    DARK_LOAD_LEVEL_DATA_CONSUMED_BYTES + modelSubfile.bytes + sceneSubfile.bytes;
  const relocatedSceneEndAddress = relocatedCopyBufferAddress + postCopyBufferFootprintBytes;
  const polygonBufferMarginBytes = LOWER_POLYGON_BUFFER_ADDRESS - relocatedSceneEndAddress;
  invariant(expandedOverlayBytes === 0x18800 && polygonBufferMarginBytes === 0x40228, "Dark Hollow RAM-capacity calculation changed.");

  const appendDelta = darkOverlay.length;
  const particleBundleAddress = OVERLAY_LOAD_ADDRESS + darkOverlay.length + BUNDLE.compactOverlayBytes;
  const dispatchShimAddress = particleBundleAddress + BUNDLE.particleBundleBytes;
  const requiredPackageBytes = BUNDLE.wizardPackageBytes + BUNDLE.lightningPackageBytes;
  const largestModelZeroRunBytes = modelZeroRuns[0].bytes;
  const report = {
    schemaVersion: 1,
    auditId: "darkhollow.green-wizard.full-transplant.static-audit.v1",
    levelKey: "darkhollow",
    bundleId: BUNDLE.id,
    compatibilityStatus: "NeedsTargetProfile",
    readiness: "Blocked",
    canStageInstance: false,
    normalCreateBinReady: false,
    candidateBinEmitted: false,
    reason:
      "Overlay ABI, RAM capacity, target row, properties scratch, and pointer fixups are statically viable, but actor-package allocation and descriptor-aware VRAM rebasing are not yet proven.",
    sourceGuards: {
      imagePath: path.basename(imagePath),
      imageSha256,
      wadAnalysisPath: path.relative(process.cwd(), analysisPath),
      discLayout: layout,
      wad: { lba: wadFile.lba, bytes: wadFile.bytes },
      executable: {
        name: executableFile.name,
        lba: executableFile.lba,
        bytes: executableFile.bytes,
        sha256: sha256(executable),
      },
    },
    donorBundle: {
      ...BUNDLE,
      compactOverlayBytesHex: hex(BUNDLE.compactOverlayBytes),
      wizardPackageOffsetHex: hex(BUNDLE.wizardPackageOffset),
      wizardPackageBytesHex: hex(BUNDLE.wizardPackageBytes),
      lightningPackageOffsetHex: hex(BUNDLE.lightningPackageOffset),
      lightningPackageBytesHex: hex(BUNDLE.lightningPackageBytes),
      trailDescriptorOffsetHex: hex(BUNDLE.trailDescriptorOffset),
    },
    targetEntries: {
      overlay: {
        entry: DARK_OVERLAY_ENTRY,
        wadOffset: hex(darkOverlayEntry.offset),
        bytes: darkOverlayEntry.size,
        bytesHex: hex(darkOverlayEntry.size),
        sha256: sha256(darkOverlay),
      },
      data: {
        entry: DARK_DATA_ENTRY,
        wadOffset: hex(darkDataEntry.offset),
        bytes: darkDataEntry.size,
        bytesHex: hex(darkDataEntry.size),
        sha256: sha256(darkData),
        subfiles: dataSubfiles.map((subfile) => ({
          ...subfile,
          offsetHex: hex(subfile.offset),
          bytesHex: hex(subfile.bytes),
        })),
      },
      scene: {
        dataOffset: hex(sceneSubfile.offset),
        bytes: sceneSubfile.bytes,
        bytesHex: hex(sceneSubfile.bytes),
        sha256: sha256(scene),
      },
    },
    overlayAndRam: {
      staticStatus: "Pass",
      overlayLoadAddress: hex(OVERLAY_LOAD_ADDRESS, 8),
      nativeOverlayBytes: darkOverlay.length,
      compactDonorBytes: BUNDLE.compactOverlayBytes,
      particleBundleBytes: BUNDLE.particleBundleBytes,
      shimBytes: BUNDLE.shimBytes,
      paddingBytes,
      requiredExpandedOverlayBytes: expandedOverlayBytes,
      requiredExpandedOverlayBytesHex: hex(expandedOverlayBytes),
      requiredWadGrowthBytes: expandedOverlayBytes - darkOverlay.length,
      requiredWadGrowthBytesHex: hex(expandedOverlayBytes - darkOverlay.length),
      availableIsoGrowthBytes,
      availableIsoGrowthBytesHex: hex(availableIsoGrowthBytes),
      originalExecutableLba: executableFile.lba,
      relocatedExecutableLba: executableFile.lba + (expandedOverlayBytes - darkOverlay.length) / 2048,
      nextIsoFile: nextRootFile,
      originalCopyBufferAddress: hex(0x80085594, 8),
      relocatedCopyBufferAddress: hex(relocatedCopyBufferAddress, 8),
      executableCopyBufferHiOffset: hex(copyBufferPairs[0].offset),
      executableCopyBufferLoOffset: hex(copyBufferPairs[0].offset + 4),
      executableCopyBufferHiWord: hex(copyBufferPairs[0].highWord, 8),
      executableCopyBufferLoWord: hex(copyBufferPairs[0].lowWord, 8),
      postCopyBufferFootprintBytes,
      postCopyBufferFootprintBytesHex: hex(postCopyBufferFootprintBytes),
      relocatedSceneEndAddress: hex(relocatedSceneEndAddress, 8),
      lowerPolygonBufferAddress: hex(LOWER_POLYGON_BUFFER_ADDRESS, 8),
      polygonBufferMarginBytes,
      polygonBufferMarginBytesHex: hex(polygonBufferMarginBytes),
      caveRunsAtLeast32Bytes: zeroRuns(darkOverlay, 0x20).map((run) => ({
        overlayStart: hex(run.start),
        overlayEnd: hex(run.end),
        bytes: run.bytes,
        bytesHex: hex(run.bytes),
      })),
    },
    hookAbi: {
      staticStatus: "Pass",
      currentMobyRegister: "s3",
      actorIdLoadAddress: hex(OVERLAY_LOAD_ADDRESS + 0x4dc, 8),
      actorIdLoadWord: "0x86630036",
      importedHandlerExpectedMobyRegister: "s7",
      requiredAdapter: "swap s3/s7 on imported-handler entry and restore s3/s7 before Dark Hollow next-Moby",
      dispatchHookAddress: hex(dispatchHookAddress, 8),
      dispatchHookOffset: hex(dispatchHookOffset),
      dispatchPreimageWords: dispatchWords.map((word) => hex(word, 8)),
      dispatchPreimageSha256: sha256(darkOverlay.subarray(dispatchHookOffset, dispatchHookOffset + 12)),
      originalActorFbBranchTarget: hex(decodeBranchTarget(dispatchHookAddress + 4, dispatchWords[1]), 8),
      dispatchContinueAddress: hex(dispatchHookAddress + 12, 8),
      nextMobyAddress: "0x80082914",
      appendRelocationDelta: hex(appendDelta),
      relocatedGreenWizardHandlerAddress: hex(0x80082818 + appendDelta, 8),
      relocatedLightningHandlerAddress: hex(0x8007ead0 + appendDelta, 8),
      relocatedLightningDamagePatchAddress: hex(0x8007eb88 + appendDelta, 8),
      relocatedLightningImpactBurstPatchAddress: hex(0x8007eb9c + appendDelta, 8),
      relocatedLightningImpactSafeExitAddress: hex(0x80086a50 + appendDelta, 8),
      relocatedWizardSpawnInitializerAddress: hex(0x80086dd8 + appendDelta, 8),
      relocatedMainExitAddress: hex(0x80086d8c + appendDelta, 8),
      particleBundleAddress: hex(particleBundleAddress, 8),
      dispatchShimAddress: hex(dispatchShimAddress, 8),
      returnShimAddress: hex(dispatchShimAddress + 0x80, 8),
      trailDescriptorAddress: hex(dispatchShimAddress + 0x94, 8),
    },
    particleAbi: {
      staticStatus: "Pass",
      targetCurrentParticleRegister: "s1",
      donorCurrentParticleRegister: "s2",
      requiredAdapter: "rewrite imported particle-current references from s2 to s1",
      updateTableAddress: hex(OVERLAY_LOAD_ADDRESS + updateTableOffset, 8),
      updateTableSha256: sha256(darkOverlay.subarray(updateTableOffset, updateTableOffset + 0x144)),
      spawnTableAddress: hex(OVERLAY_LOAD_ADDRESS + spawnTableOffset, 8),
      spawnTableSha256: sha256(darkOverlay.subarray(spawnTableOffset, spawnTableOffset + 0x144)),
      type07: {
        updateSlotAddress: hex(OVERLAY_LOAD_ADDRESS + 0x158, 8),
        updatePreimage: "0x80084450",
        spawnSlotAddress: hex(OVERLAY_LOAD_ADDRESS + 0x29c, 8),
        spawnPreimage: "0x8008553C",
      },
      type41: {
        updateSlotAddress: hex(OVERLAY_LOAD_ADDRESS + 0x240, 8),
        updatePreimage: "0x80084450",
        spawnSlotAddress: hex(OVERLAY_LOAD_ADDRESS + 0x384, 8),
        spawnPreimage: "0x8008553C",
      },
      targetSharedUpdateAddress: "0x8008442C",
      targetDeleteUpdateAddress: "0x80084448",
      targetContinueUpdateAddress: "0x80084450",
      targetContinueSpawnAddress: "0x8008553C",
    },
    actorPackages: {
      staticStatus: "Blocked",
      modelSubfileOffset: hex(modelSubfile.offset),
      modelSubfileBytes: modelSubfile.bytes,
      modelSubfileBytesHex: hex(modelSubfile.bytes),
      requiredWizardPackageBytes: BUNDLE.wizardPackageBytes,
      requiredLightningPackageBytes: BUNDLE.lightningPackageBytes,
      requiredCombinedPackageBytes: requiredPackageBytes,
      requiredCombinedPackageBytesHex: hex(requiredPackageBytes),
      largestUnallocatedZeroRun: {
        modelRelativeOffset: hex(modelZeroRuns[0].start),
        dataOffset: hex(modelSubfile.offset + modelZeroRuns[0].start),
        bytes: largestModelZeroRunBytes,
        bytesHex: hex(largestModelZeroRunBytes),
        shortfallBytes: requiredPackageBytes - largestModelZeroRunBytes,
        shortfallBytesHex: hex(requiredPackageBytes - largestModelZeroRunBytes),
      },
      nextFreeRootSlotOffset: "0xDC",
      nextFreeActorIdSlotOffset: "0x196",
      unusedRootSlotsAvailable: true,
      unusedPackageStorageAvailable: false,
      reusableLargeResidentRoots: reusableLargeRoots,
      actorRoots,
      requiredResolution:
        "Grow and relocate the model subfile, shift scene/later subfiles and header offsets, then register independent 0x011B and 0x0026 roots. No native active actor package may be overwritten.",
    },
    instanceCandidate: {
      staticStatus: "Pass after bundle installation",
      targetTrueIndex: TARGET_TRUE_INDEX,
      targetLabel: "Big Armor Gnorc",
      targetActorId: "0x00A6",
      targetActorClassInstanceCount: actorCounts.get(0x00a6),
      sourceTableDataOffset: hex(SOURCE_TABLE_DATA_OFFSET),
      sourceTableSceneOffset: hex(sourceTableSceneOffset),
      sourceRecordSceneOffset: hex(targetRecordSceneOffset),
      sourceRecordSha256: sha256(targetRecord),
      originalPropertiesPointer: hex(targetRecord.readUInt32LE(0)),
      targetPositionRaw: {
        x: targetRecord.readInt32LE(0x0c),
        y: targetRecord.readInt32LE(0x10),
        z: targetRecord.readInt32LE(0x14),
      },
      preservedYawByte: hex(targetRecord[0x46], 2),
      preservedRewardByte: hex(targetRecord[0x53], 2),
      propertiesScratchSceneOffset: hex(PROPERTIES_SCRATCH_SCENE_OFFSET),
      propertiesScratchBytes: PROPERTIES_BYTES,
      propertiesScratchPreimageSha256: sha256(propertiesScratch),
      propertiesInternalRoutePointerValue: hex(PROPERTIES_SCRATCH_SCENE_OFFSET + 0x28),
      pointerFixupListSceneOffset: hex(POINTER_FIXUP_LIST_SCENE_OFFSET),
      pointerFixupCountBefore: POINTER_FIXUP_COUNT,
      pointerFixupCountAfter: POINTER_FIXUP_COUNT + 1,
      pointerFixupPreimageSha256: sha256(fixupBytes),
      existingSourcePointerFixupIndex: fixups.indexOf(targetRecordSceneOffset),
      appendedPropertiesPointerFixup: hex(PROPERTIES_SCRATCH_SCENE_OFFSET),
      pointerFixupAppendSceneOffset: hex(POINTER_FIXUP_LIST_SCENE_OFFSET + fixupBytes.length),
      pointerFixupAppendPreimage: "00000000",
    },
    textureAllocation: {
      staticStatus: "Blocked",
      dependencyBytes: donorTextureAggregateBytes.length,
      dependencyPixelBytes: 2056,
      dependencyClutBytes: 160,
      donorAggregateSha256: sha256(donorTextureAggregateBytes),
      targetAtProvenCoordinatesAggregateSha256: sha256(targetTextureAggregateBytes),
      allProvenCoordinateRegionsCollide: true,
      regions: textureEvidence,
      descriptorBoundary:
        "The trail descriptor is independently rebuildable, but the copied Wizard and lightning actor packages still reference their native VRAM coordinates. No guarded Dark Hollow package-face descriptor rebase exists.",
      requiredResolution:
        "Allocate non-overlapping Dark Hollow VRAM/CLUT regions and add descriptor-aware rebasing for the Wizard and lightning actor packages before copying any texture bytes.",
    },
    checks: [
      { id: "clean-image-preimage", status: "Pass" },
      { id: "overlay-dispatch-abi", status: "Pass" },
      { id: "particle-07-41-abi", status: "Pass" },
      { id: "overlay-and-ram-capacity", status: "Pass" },
      { id: "target-row-reward-properties-fixups", status: "Pass" },
      { id: "independent-actor-package-allocation", status: "Blocked" },
      { id: "non-overlapping-vram-and-actor-descriptor-rebase", status: "Blocked" },
      { id: "guarded-candidate-bin", status: "Not emitted" },
      { id: "duckstation-runtime-proof", status: "Not run" },
    ],
    blockers: [
      {
        id: "darkhollow-model-package-capacity",
        severity: "hard",
        finding:
          `The model subfile needs ${hex(requiredPackageBytes)} new bytes for independent Wizard and lightning packages, but its only qualifying zero tail is ${hex(largestModelZeroRunBytes)}. Every resident root span large enough for the Wizard belongs to an active native actor class.`,
        unsafeShortcut:
          "Overwriting the 0x00A6 package for T48 would also break or silently retarget T49, and stealing another active package would regress native objects.",
      },
      {
        id: "darkhollow-vram-coordinate-collisions",
        severity: "hard",
        finding:
          "All five v11 target coordinate regions collide with Dark Hollow native data. The trail pixel tile is zero, but its CLUT slot is occupied; the other Wizard/lightning pixels and CLUTs are occupied as well.",
        unsafeShortcut:
          "Copying Toasty's v11 texture ranges verbatim would overwrite Dark Hollow textures and reproduce corrupt native rendering.",
      },
      {
        id: "darkhollow-actor-descriptor-rebase",
        severity: "hard",
        finding:
          "The v11 trail sprite descriptor can be rebuilt, but no static guard or writer currently rebases the Wizard/lightning model face descriptors to new Dark Hollow VRAM allocations.",
        unsafeShortcut:
          "Allocating different pixels without rebasing actor descriptors leaves the Wizard and projectile sampling the old occupied coordinates.",
      },
    ],
    nextSafeExperiment: [
      "Implement a guarded nested model-subfile grower and prove an otherwise byte-identical Dark Hollow boot after shifting the scene/later subfiles and data header offsets.",
      "Trace and implement actor-package face descriptor rebasing, allocate Dark Hollow-specific VRAM/CLUT regions, and verify every target preimage plus installed descriptor readback.",
      "Only then assemble the already-compatible overlay/particle shim and stage T48 while preserving reward 0x55.",
    ],
    catalogDecision: {
      addedToRuntimeBundleProfileCatalog: false,
      reason:
        "The current transplanted-profile schema requires complete Data/Texture/Guard targets and Evaluate() reports such profiles as CanStageInstance=true. Adding this incomplete target would make a false editor capability claim.",
      currentHonestResult: "NeedsTargetProfile with CanStageInstance=false",
    },
  };

  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  fs.writeFileSync(outputPath, `${JSON.stringify(report, null, 2)}\n`);
  console.log(outputPath);
} finally {
  fs.closeSync(fd);
}

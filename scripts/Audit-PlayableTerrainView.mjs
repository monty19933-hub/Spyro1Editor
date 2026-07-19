#!/usr/bin/env node

import fs from "node:fs";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const cacheRoot = path.join(repositoryRoot, "editor-cache");
const outputPath = process.argv[2]
  ? path.resolve(process.argv[2])
  : path.join(repositoryRoot, "docs", "research", "playable-terrain-view-audit.json");

const minimumGroupFaces = 64;
const minimumLevelFaceFraction = 0.04;
const minimumProjectedAreaFraction = 0.08;
const minimumBroadPlaneFaces = 256;
const minimumBroadPlaneAreaFraction = 0.25;
const minimumBroadPlanePerimeterSides = 2;
const flatZEpsilon = 0.001;
const objectZTolerance = 256;
const broadPlaneKeepExceptions = new Set([
  "stonehill:32@1024",
  "beastmakers:41@951"
]);
const screenshotConfirmedBroadPlanes = new Set([
  "artisans:27@192",
  "metalhead:67@128"
]);

function readJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, "utf8"));
}

function loadBuiltInMaterialOverrides() {
  const sourcePath = path.join(
    repositoryRoot,
    "src",
    "Spyro.Editor.Core",
    "Scene",
    "TerrainMaterialClassifier.cs");
  const source = fs.readFileSync(sourcePath, "utf8");
  const overrides = new Map();
  const levelPattern = /\["([^"]+)"\]\s*=\s*new Dictionary<int, string>\s*\{([\s\S]*?)\n\s*\}/g;
  for (const match of source.matchAll(levelPattern)) {
    const textureOverrides = new Map();
    const texturePattern = /\[(\d+)\]\s*=\s*"([^"]+)"/g;
    for (const textureMatch of match[2].matchAll(texturePattern)) {
      textureOverrides.set(Number(textureMatch[1]), normalizeSurface(textureMatch[2]));
    }
    overrides.set(match[1].toLowerCase(), textureOverrides);
  }
  return overrides;
}

function normalizeSurface(surface) {
  const normalized = String(surface ?? "").trim().toLowerCase();
  const aliases = {
    rock: "stone",
    brickwork: "brick",
    "red stone": "brick",
    castle: "brick",
    dirt: "ground",
    "dry dirt": "ground",
    "dry ground": "ground",
    beach: "sand",
    "sand/beach": "sand",
    wall: "cliff",
    icy: "ice",
    snow: "ice",
    goo: "ooze",
    acid: "ooze",
    swamp: "ooze"
  };
  return aliases[normalized] ?? (normalized || "unknown");
}

function inferSurface(color) {
  const maximum = Math.max(color.r, color.g, color.b);
  const minimum = Math.min(color.r, color.g, color.b);
  if (color.b >= color.r + 32 && color.b >= color.g + 24)
    return "water";
  if (color.r >= 172 && color.r >= color.g + 40 && color.g >= color.b + 20 && color.b <= 105)
    return "lava";
  if (color.g >= color.r + 16 && color.g >= color.b + 16)
    return "grass";
  if (color.r >= 168 && color.g >= 145 && color.b <= 126 && color.r >= color.b + 34)
    return "sand";
  if (maximum - minimum <= 24 || maximum < 115)
    return "stone";
  if (color.r >= color.g && color.g >= color.b && color.r >= color.b + 22)
    return "stone";
  return "ground";
}

function buildSurfaceByTexture(levelKey, faces, builtInOverrides) {
  const colorSums = new Map();
  for (const face of faces) {
    const sum = colorSums.get(face.textureId) ?? { count: 0, r: 0, g: 0, b: 0 };
    sum.count++;
    sum.r += face.faceColor.r;
    sum.g += face.faceColor.g;
    sum.b += face.faceColor.b;
    colorSums.set(face.textureId, sum);
  }

  const levelOverrides = builtInOverrides.get(levelKey) ?? new Map();
  const result = new Map();
  for (const [textureId, sum] of colorSums) {
    result.set(
      textureId,
      levelOverrides.get(textureId) ?? inferSurface({
        r: Math.trunc(sum.r / sum.count),
        g: Math.trunc(sum.g / sum.count),
        b: Math.trunc(sum.b / sum.count)
      }));
  }
  return result;
}

function polygonArea(points) {
  let sum = 0;
  for (let index = 0; index < points.length; index++) {
    const point = points[index];
    const next = points[(index + 1) % points.length];
    sum += point.x * next.y - next.x * point.y;
  }
  return Math.abs(sum) * 0.5;
}

function nativeFaceFanTriangleCount(face) {
  const uniqueVertexCount = new Set((face.vertexIndexes ?? []).slice(0, 4)).size;
  return uniqueVertexCount === 3 ? 1 : uniqueVertexCount >= 4 ? 2 : 0;
}

function emptyBounds() {
  return { minX: Infinity, maxX: -Infinity, minY: Infinity, maxY: -Infinity };
}

function includePoint(bounds, point) {
  bounds.minX = Math.min(bounds.minX, point.x);
  bounds.maxX = Math.max(bounds.maxX, point.x);
  bounds.minY = Math.min(bounds.minY, point.y);
  bounds.maxY = Math.max(bounds.maxY, point.y);
}

function includeFace(bounds, face) {
  for (const point of face.points)
    includePoint(bounds, point);
}

function boundsForFaces(faces) {
  const bounds = emptyBounds();
  for (const face of faces)
    includeFace(bounds, face);
  return bounds;
}

function serializeBounds(bounds) {
  if (!Number.isFinite(bounds.minX))
    return null;
  return {
    minX: bounds.minX,
    maxX: bounds.maxX,
    minY: bounds.minY,
    maxY: bounds.maxY,
    width: bounds.maxX - bounds.minX,
    height: bounds.maxY - bounds.minY
  };
}

function pointInPolygon(x, y, points) {
  let inside = false;
  for (let index = 0, previous = points.length - 1; index < points.length; previous = index++) {
    const a = points[index];
    const b = points[previous];
    const cross = (x - a.x) * (b.y - a.y) - (y - a.y) * (b.x - a.x);
    const dot = (x - a.x) * (x - b.x) + (y - a.y) * (y - b.y);
    if (Math.abs(cross) <= 0.00001 && dot <= 0)
      return true;
    if ((a.y > y) !== (b.y > y) && x < ((b.x - a.x) * (y - a.y)) / (b.y - a.y) + a.x)
      inside = !inside;
  }
  return inside;
}

function findMobyOverlaps(groupFaces, groupZ, mobys) {
  const overlaps = [];
  for (const moby of mobys) {
    if (Math.abs(moby.z - groupZ) > objectZTolerance)
      continue;
    if (!groupFaces.some(face => pointInPolygon(moby.x, moby.y, face.points)))
      continue;
    overlaps.push({
      trueIndex: moby.trueIndex,
      typeHex: moby.typeHex,
      x: moby.x,
      y: moby.y,
      z: moby.z
    });
  }
  return overlaps;
}

function semanticGroupKey(surface, z) {
  return `${surface}@${z}`;
}

function texturePlaneKey(textureId, z) {
  return `${textureId}@${z}`;
}

function perimeterSidesFor(bounds, fullBounds) {
  const result = [];
  if (Math.abs(bounds.minX - fullBounds.minX) <= flatZEpsilon) result.push("left");
  if (Math.abs(bounds.maxX - fullBounds.maxX) <= flatZEpsilon) result.push("right");
  if (Math.abs(bounds.minY - fullBounds.minY) <= flatZEpsilon) result.push("top");
  if (Math.abs(bounds.maxY - fullBounds.maxY) <= flatZEpsilon) result.push("bottom");
  return result;
}

function analyzeLevel(level, builtInOverrides) {
  const levelKey = level.Key.toLowerCase();
  const overlay = readJson(path.join(cacheRoot, `${levelKey}-runtime-scene-editor-overlay.json`));
  const mobyCache = readJson(path.join(cacheRoot, `${levelKey}-mobys.json`));
  const faces = overlay.candidates[0].polygons;
  const mobys = mobyCache.mobys;
  const surfaceByTexture = buildSurfaceByTexture(levelKey, faces, builtInOverrides);
  const fullBounds = boundsForFaces(faces);
  const totalProjectedArea = faces.reduce((sum, face) => sum + polygonArea(face.points), 0);
  const semanticGroups = new Map();
  const texturePlaneGroups = new Map();

  for (const face of faces) {
    if (Math.abs(face.maxZ - face.minZ) <= flatZEpsilon) {
      const z = Math.round(face.avgZ * 1000) / 1000;
      const key = texturePlaneKey(face.textureId, z);
      const plane = texturePlaneGroups.get(key) ?? {
        key,
        textureId: face.textureId,
        surface: normalizeSurface(surfaceByTexture.get(face.textureId)),
        z,
        faces: [],
        projectedArea: 0,
        bounds: emptyBounds(),
        nativeTexturedFaceCount: 0,
        nativeUntexturedFaceCount: 0,
        nativeSemitransparentFaceCount: 0
      };
      plane.faces.push(face);
      plane.projectedArea += polygonArea(face.points);
      includeFace(plane.bounds, face);
      if (face.nativeUntexturedSentinel)
        plane.nativeUntexturedFaceCount++;
      else
        plane.nativeTexturedFaceCount++;
      if (face.nativePrimitiveSemiTransparent)
        plane.nativeSemitransparentFaceCount++;
      texturePlaneGroups.set(key, plane);
    }

    const surface = normalizeSurface(surfaceByTexture.get(face.textureId));
    if (!["water", "lava", "ooze"].includes(surface))
      continue;
    if (Math.abs(face.maxZ - face.minZ) > flatZEpsilon)
      continue;

    const z = Math.round(face.avgZ * 1000) / 1000;
    const key = semanticGroupKey(surface, z);
    const group = semanticGroups.get(key) ?? {
      key,
      surface,
      z,
      faces: [],
      projectedArea: 0,
      bounds: emptyBounds(),
      textureIds: new Set(),
      texturePlanes: new Map(),
      nativeTexturedFaceCount: 0,
      nativeUntexturedFaceCount: 0,
      nativeSemitransparentFaceCount: 0
    };
    group.faces.push(face);
    group.projectedArea += polygonArea(face.points);
    includeFace(group.bounds, face);
    group.textureIds.add(face.textureId);
    const texturePlane = group.texturePlanes.get(face.textureId) ?? {
      textureId: face.textureId,
      faceCount: 0,
      projectedArea: 0,
      bounds: emptyBounds()
    };
    texturePlane.faceCount++;
    texturePlane.projectedArea += polygonArea(face.points);
    includeFace(texturePlane.bounds, face);
    group.texturePlanes.set(face.textureId, texturePlane);
    if (face.nativeUntexturedSentinel)
      group.nativeUntexturedFaceCount++;
    else
      group.nativeTexturedFaceCount++;
    if (face.nativePrimitiveSemiTransparent)
      group.nativeSemitransparentFaceCount++;
    semanticGroups.set(key, group);
  }

  const semanticCandidates = [...semanticGroups.values()]
    .filter(group =>
      group.faces.length >= minimumGroupFaces &&
      (group.faces.length / faces.length >= minimumLevelFaceFraction ||
        group.projectedArea / totalProjectedArea >= minimumProjectedAreaFraction))
    .sort((left, right) => right.faces.length - left.faces.length);
  const broadPlaneCandidates = [...texturePlaneGroups.values()]
    .map(group => ({
      ...group,
      perimeterSides: perimeterSidesFor(group.bounds, fullBounds)
    }))
    .filter(group =>
      group.faces.length >= minimumBroadPlaneFaces &&
      group.projectedArea / totalProjectedArea >= minimumBroadPlaneAreaFraction &&
      group.perimeterSides.length >= minimumBroadPlanePerimeterSides)
    .sort((left, right) => right.projectedArea - left.projectedArea);
  const suppressedBroadPlanes = broadPlaneCandidates.filter(group =>
    !broadPlaneKeepExceptions.has(`${levelKey}:${group.key}`));
  const candidateFaces = suppressedBroadPlanes.flatMap(group => group.faces);
  const candidateFaceSet = new Set(candidateFaces);
  const remainingFaces = faces.filter(face => !candidateFaceSet.has(face));
  const remainingBounds = boundsForFaces(remainingFaces);
  const mobysOutsideRemainingTerrainBounds = mobys
    .filter(moby =>
      moby.x < remainingBounds.minX || moby.x > remainingBounds.maxX ||
      moby.y < remainingBounds.minY || moby.y > remainingBounds.maxY)
    .map(moby => ({ trueIndex: moby.trueIndex, typeHex: moby.typeHex, x: moby.x, y: moby.y, z: moby.z }));

  const semanticGroupReports = semanticCandidates.map(group => {
    const overlaps = findMobyOverlaps(group.faces, group.z, mobys);
    const perimeterSides = perimeterSidesFor(group.bounds, fullBounds);
    return {
      key: group.key,
      surface: group.surface,
      z: group.z,
      faceCount: group.faces.length,
      levelFaceFraction: group.faces.length / faces.length,
      projectedArea: group.projectedArea,
      projectedAreaFraction: group.projectedArea / totalProjectedArea,
      textureIds: [...group.textureIds].sort((left, right) => left - right),
      texturePlanes: [...group.texturePlanes.values()]
        .sort((left, right) => right.faceCount - left.faceCount)
        .map(texturePlane => ({
          textureId: texturePlane.textureId,
          faceCount: texturePlane.faceCount,
          projectedArea: texturePlane.projectedArea,
          bounds: serializeBounds(texturePlane.bounds)
        })),
      bounds: serializeBounds(group.bounds),
      perimeterSides,
      nativeTexturedFaceCount: group.nativeTexturedFaceCount,
      nativeUntexturedFaceCount: group.nativeUntexturedFaceCount,
      nativeSemitransparentFaceCount: group.nativeSemitransparentFaceCount,
      overlappingMobys: overlaps
    };
  });
  const broadPlaneReports = broadPlaneCandidates.map(group => {
    const selector = `${levelKey}:${group.key}`;
    const disposition = broadPlaneKeepExceptions.has(selector) ? "keep-exception" : "suppress-in-playable-view";
    const nativeFanTriangleCount = group.faces.reduce(
      (sum, face) => sum + nativeFaceFanTriangleCount(face),
      0);
    const affineTextureTriangleDrawCount = group.faces.reduce(
      (sum, face) => sum + (face.nativeUntexturedSentinel || face.nativePrimitiveSemiTransparent
        ? 0
        : nativeFaceFanTriangleCount(face)),
      0);
    const minimumMapGouraudDrawCount = group.faces.reduce(
      (sum, face) => sum + (face.nativePrimitiveSemiTransparent
        ? 0
        : nativeFaceFanTriangleCount(face) * 4),
      0);
    const ordinaryMapEdgeDrawCount = group.faces.filter(face => !face.nativePrimitiveSemiTransparent).length;
    const guardedFallbackDrawCount = group.nativeSemitransparentFaceCount;
    return {
      selector,
      key: group.key,
      textureId: group.textureId,
      textureTotalFaceCount: faces.filter(face => face.textureId === group.textureId).length,
      sameTextureFaceCountOutsidePlane: faces.filter(face => face.textureId === group.textureId).length - group.faces.length,
      surfaceLabelForReferenceOnly: group.surface,
      z: group.z,
      faceCount: group.faces.length,
      levelFaceFraction: group.faces.length / faces.length,
      projectedArea: group.projectedArea,
      projectedAreaFraction: group.projectedArea / totalProjectedArea,
      bounds: serializeBounds(group.bounds),
      perimeterSides: group.perimeterSides,
      nativeTexturedFaceCount: group.nativeTexturedFaceCount,
      nativeUntexturedFaceCount: group.nativeUntexturedFaceCount,
      nativeSemitransparentFaceCount: group.nativeSemitransparentFaceCount,
      nativeFanTriangleCount,
      estimatedMinimumMapDrawOperationsAvoided: {
        minimumMapGouraudDrawCount,
        affineTextureTriangleDrawCount,
        ordinaryMapEdgeDrawCount,
        guardedFallbackDrawCount,
        total: minimumMapGouraudDrawCount + affineTextureTriangleDrawCount + ordinaryMapEdgeDrawCount + guardedFallbackDrawCount,
        note: "Lower bound at the normal two-step Gouraud subdivision. Large projected faces can use three steps and cost more."
      },
      overlappingMobys: findMobyOverlaps(group.faces, group.z, mobys),
      disposition,
      evidence: screenshotConfirmedBroadPlanes.has(selector)
        ? "user-screenshot-confirmed broad editor sheet"
        : disposition === "keep-exception"
          ? selector === "stonehill:32@1024"
            ? "source-proven legitimate Stone Hill ocean"
            : "paired production capture confirmed essential Beast Makers swamp/ooze gameplay context"
          : "all-level exact-plane signature; runtime data remains untouched"
    };
  });
  const suppressedBroadPlaneReports = broadPlaneReports.filter(group =>
    group.disposition === "suppress-in-playable-view");

  return {
    levelKey,
    displayName: level.DisplayName,
    totalFaceCount: faces.length,
    sectorCount: overlay.sectorRenderMetadata.length,
    mobyCount: mobys.length,
    fullTerrainBounds: serializeBounds(fullBounds),
    totalProjectedArea,
    candidateFaceCount: candidateFaces.length,
    candidateFaceFraction: candidateFaces.length / faces.length,
    candidateProjectedArea: suppressedBroadPlaneReports.reduce((sum, group) => sum + group.projectedArea, 0),
    candidateProjectedAreaFraction: suppressedBroadPlaneReports.reduce((sum, group) => sum + group.projectedArea, 0) / totalProjectedArea,
    candidateNativeTexturedFaceCount: suppressedBroadPlaneReports.reduce((sum, group) => sum + group.nativeTexturedFaceCount, 0),
    candidateNativeUntexturedFaceCount: suppressedBroadPlaneReports.reduce((sum, group) => sum + group.nativeUntexturedFaceCount, 0),
    candidateNativeSemitransparentFaceCount: suppressedBroadPlaneReports.reduce((sum, group) => sum + group.nativeSemitransparentFaceCount, 0),
    remainingTerrainBounds: serializeBounds(remainingBounds),
    mobysOutsideRemainingTerrainBounds,
    candidateGroups: suppressedBroadPlaneReports,
    broadPlaneGroups: broadPlaneReports,
    semanticUnderlayExploration: {
      note: "Exploratory only. Surface/color inference is not used by the recommended default suppression predicate.",
      candidateFaceCount: semanticGroupReports.reduce((sum, group) => sum + group.faceCount, 0),
      candidateGroups: semanticGroupReports
    }
  };
}

const index = readJson(path.join(cacheRoot, "index.json"));
const builtInOverrides = loadBuiltInMaterialOverrides();
const levels = index.levels.map(level => analyzeLevel(level, builtInOverrides));
const candidateFaceCount = levels.reduce((sum, level) => sum + level.candidateFaceCount, 0);
const totalFaceCount = levels.reduce((sum, level) => sum + level.totalFaceCount, 0);
const candidateNativeTexturedFaceCount = levels.reduce((sum, level) => sum + level.candidateNativeTexturedFaceCount, 0);
const estimatedMinimumMapDrawOperationsAvoided = levels.reduce(
  (sum, level) => sum + level.candidateGroups.reduce(
    (levelSum, group) => levelSum + group.estimatedMinimumMapDrawOperationsAvoided.total,
    0),
  0);
const levelsWithCandidates = levels.filter(level => level.candidateFaceCount > 0).length;
const levelsWithFitRisk = levels.filter(level => level.mobysOutsideRemainingTerrainBounds.length > 0).length;

const report = {
  schema: "spyro-editor-playable-terrain-view-audit-v1",
  generatedAt: new Date().toISOString(),
  repositoryRoot: ".",
  cacheRoot: "editor-cache",
  command: `node scripts/Audit-PlayableTerrainView.mjs ${path.relative(repositoryRoot, outputPath)}`,
  scope: {
    levelCount: levels.length,
    totalFaceCount,
    candidateFaceCount,
    candidateFaceFraction: candidateFaceCount / totalFaceCount,
    candidateNativeTexturedFaceCount,
    estimatedMinimumMapDrawOperationsAvoided,
    levelsWithCandidates,
    levelsWithFitRisk,
    note: "Primary candidates use exact texture/Z plane geometry, projected coverage, and perimeter contact; they do not use color or semantic surface inference. They are editor-presentation candidates, not proof that native faces are non-playable or safe to delete. Opacity-only fading does not reduce face rendering work; skipping or simplifying native textured candidate faces does."
  },
  thresholds: {
    primaryGrouping: "exact native texture ID plus average Z",
    flatZEpsilon,
    minimumBroadPlaneFaces,
    minimumBroadPlaneAreaFraction,
    minimumBroadPlanePerimeterSides,
    broadPlaneKeepExceptions: [...broadPlaneKeepExceptions],
    objectZTolerance,
    candidateRule: "exact flat texture/Z plane and faceCount >= minimumBroadPlaneFaces and projectedAreaFraction >= minimumBroadPlaneAreaFraction and perimeterSides >= minimumBroadPlanePerimeterSides, excluding explicit keep exceptions",
    exploratorySemanticUnderlayRule: {
      eligibleSurfaces: ["water", "lava", "ooze"],
      grouping: "normalized surface plus average Z",
      minimumGroupFaces,
      minimumLevelFaceFraction,
      minimumProjectedAreaFraction,
      note: "Reported for comparison only; never use semantic/color inference as the default suppression predicate."
    }
  },
  safetyInvariants: [
    "This is presentation-only. Never remove candidate faces from the cache, edit store, export plan, collision data, or BIN/CUE.",
    "Complete Scene must present every cache face, byte-for-byte independent of the everyday view.",
    "Selected, hovered, edited, removed, and add-clone faces must remain present or be temporarily revealed even when they are candidates.",
    "Fit and Map object transforms must union presented terrain bounds with every visible Moby. Terrain-only bounds strand Sunny Flight objects; the Beast Makers swamp/ooze plane is retained after paired capture showed that it supplies essential object context.",
    "Do not remove the largest connected component, all detached components, or all perimeter-touching faces. Dark Passage and Dream Weavers have legitimate separated regions, and flight levels have legitimate detached routes and targets.",
    "Do not treat semantic water, lava, ooze, or color classification as proof that a face is background. The primary signature does not use it. Stone Hill ocean, Beast Makers swamp/ooze, and many hazard planes are valid native terrain and must remain inspectable.",
    "Do not hide an entire candidate group merely because few Mobys overlap it. Candidate groups with object overlap occur in Artisans, Stone Hill, flight levels, Beast Makers, Gnasty's World, and others.",
    "If performance is the goal, skip or simplify candidate fill, texture, Gouraud, and ordinary edge passes. Merely lowering alpha retains nearly all rendering cost.",
    "Any simplified candidate proxy must preserve a discoverable hover or Complete Scene path for terrain selection and editing."
  ],
  verificationGates: [
    "All 35 retail level caches load without a geometry-health issue.",
    "Complete Scene presented-face count equals the source overlay face count for every level.",
    "Playable View presented-face count plus suppressed-face count equals the source overlay face count for every level.",
    "The retail audit stays locked to 9,008 suppressed faces across eight exact planes, plus the retained 327-face Stone Hill ocean and 604-face Beast Makers swamp/ooze exceptions; any drift requires review.",
    "Selected and edited candidate faces remain visible after switching back to Playable View.",
    "Fit bounds contain every visible Moby and every flight target in Map and Fly 3D.",
    "Stone Hill ocean remains recoverable and selectable in Complete Scene; Playable View does not alter its saved or exported bytes.",
    "Beast Makers texture 41 at Z 951 remains visible in Playable View because paired production capture proved that it supplies essential swamp/ooze context around objects.",
    "Dark Passage and Dream Weavers retain all legitimate separated regions in Complete Scene and do not lose object-bearing regions in Playable View.",
    "Artisans texture 27 at Z 192 is recognized as the 780-face dominant sheet; the smaller local water groups are not accidentally conflated with it.",
    "Metal Head texture 67 at Z 128 is recognized as the 439-face, 46.39%-area broad sheet; the separate 64-face texture-67 plane at Z 127 remains present.",
    "Capture paired Playable View and Complete Scene images for every suppressed selector; foreground structures and all Mobys must remain visible and spatially coherent.",
    "A focused view-only smoke must keep the production terrain-edit signature, canonical TerrainEditStore serialization (ignoring only generatedAt), and the hashes/existence of project terrain-export input JSON identical before and after Playable/Complete toggles. A full disc export is not required for this presentation-only gate.",
    "A render diagnostic proves candidate native textured faces are actually skipped or simplified; visual alpha changes alone do not count as a performance improvement."
  ],
  levels
};

fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.writeFileSync(outputPath, `${JSON.stringify(report, null, 2)}\n`);
console.log(`Playable terrain audit: ${levels.length} levels, ${totalFaceCount.toLocaleString()} faces, ${candidateFaceCount.toLocaleString()} candidates (${(100 * candidateFaceCount / totalFaceCount).toFixed(2)}%), ${candidateNativeTexturedFaceCount.toLocaleString()} native textured.`);
console.log(`Candidate levels: ${levelsWithCandidates}; Fit-risk levels: ${levelsWithFitRisk}.`);
console.log(`Report: ${outputPath}`);

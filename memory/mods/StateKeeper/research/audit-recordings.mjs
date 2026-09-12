import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';
import assert from 'node:assert/strict';

// Research only: original manifests, chunks and game caches are never written.
const ids = [
  '98fc17ac-7ede-495c-a0a2-7dd00f208fb7',
  '77f6bbb7-b690-4b95-9ee1-3b5091aea50d',
  '67eb3a83-ad86-453d-9f61-539c212ed026',
  'a120f547-2d68-4799-9acd-0e409f0acc15',
  '635b97e3-fbc3-4bde-a948-132ddaba6bae',
  '27276489-b68f-46eb-9993-1dfd89ffbed4',
];
const inc = (o, k, n = 1) => o[k] = (o[k] || 0) + n;
const finite = Number.isFinite;
const cleanName = s => (s || '').replace(/<[^>]*>/g, '');
function savedDistances(s) {
  const result = new Map();
  for (let i = 0; i < (s?.distanceMeters || []).length; i++) {
    const a = s.distancePlayerPairs?.[i * 2], b = s.distancePlayerPairs?.[i * 2 + 1];
    if (a === undefined || b === undefined || !finite(s.distanceMeters[i])) continue;
    result.set([a, b].sort((x, y) => x - y).join(':'), s.distanceMeters[i]);
  }
  return result;
}
function validPosition(p) {
  return !p.dead && [p.positionX, p.positionY, p.positionZ].every(finite)
    && !(Math.abs(p.positionX) < 0.01 && Math.abs(p.positionY - 5000) < 0.01
      && Math.abs(p.positionZ + 5000) < 0.01);
}
function quantile(rows, q) {
  const sorted = [...rows].sort((a, b) => a[0] - b[0]);
  const target = sorted.reduce((sum, v) => sum + v[1], 0) * q;
  let sum = 0;
  for (const [v, w] of sorted) { sum += w; if (sum >= target) return v; }
  return null;
}
class Isolation {
  constructor() { this.open = null; this.last = null; this.episodes = []; }
  end(reason) {
    if (this.open && this.last - this.open.start >= 10)
      this.episodes.push({ ...this.open, end: this.last, seconds: this.last - this.open.start, reason });
    this.open = null;
    this.last = null;
  }
  observe(t, d, gapLimit) {
    if (!finite(t) || !finite(d)) { this.end('unavailable'); return; }
    if (this.last !== null && (t <= this.last || t - this.last > gapLimit)) this.end('timeBreak');
    if (this.open && d < 80) { this.last = t; this.end('rejoined'); }
    if (!this.open && d > 100) this.open = { start: t, peak: d };
    if (this.open) this.open.peak = Math.max(this.open.peak, d);
    this.last = t;
  }
}
function tests() {
  assert.equal(quantile([[10, 1], [100, 9]], 0.5), 100);
  assert.equal(savedDistances({ distancePlayerPairs: [1, 0], distanceMeters: [16] }).get('0:1'), 16);
  const iso = new Isolation();
  for (let t = 0; t <= 12; t++) iso.observe(t, t === 4 ? 90 : 101, 1);
  iso.observe(13, null, 1);
  iso.observe(100, 110, 1); iso.end('end');
  assert.equal(iso.episodes.length, 1);
  assert.equal(iso.episodes[0].seconds, 12);
  const gap = new Isolation();
  gap.observe(0, 110, 1); gap.observe(20, 110, 1); gap.end('end');
  assert.equal(gap.episodes.length, 0);
  const reset = new Isolation();
  reset.observe(20, 110, 1); reset.observe(0, 110, 1); reset.end('end');
  assert.equal(reset.episodes.length, 0);
  assert.equal(validPosition({ dead: false, positionX: 6000, positionY: 1, positionZ: 2 }), true);
  assert.equal(validPosition({ dead: false, positionX: 0, positionY: 5000, positionZ: -5000 }), false);
  console.log('Research helper checks passed (weighted quantile, hysteresis, gaps, reset, sentinel).');
}

function audit(root, id, gapLimit) {
  const dir = ['Runs', 'Favorites'].map(d => path.join(root, d)).find(d => fs.existsSync(path.join(d, id + '.json')));
  const manifest = JSON.parse(fs.readFileSync(path.join(dir, id + '.json'), 'utf8'));
  const out = { id, schema: manifest.schemaVersion, chunks: 0, samples: 0, inventory: 0, events: 0,
    errors: [], backwards: [], duplicateTimes: 0, gaps: [], eventTypes: {}, itemEventTypes: {},
    resourceEvents: {}, resourceEventsEqualValues: 0, targetedItems: [], consumedByPlayer: {},
    sourceKinds: {}, players: [], pairs: [], inventoryGuidCount: 0, consumedOwnerEvidence: {},
    statusMax: {}, afflictionIds: {}, petrifyPropertyFrames: 0, petrifyArrayNonzero: 0,
    metersPerWorldUnitExamples: [],
    definitions: (manifest.definitions || []).length,
    definitionsStructured: (manifest.definitions || []).filter(d => d.actions?.some(a => a.parameters?.length)).length };
  const cases = id === ids.at(-1) ? [228.001, 1013.882, 1805.191, 2134.8, 2538.929, 4261.242]
    .map(time => ({ time, frames: {}, events: [] })) : [];
  const players = new Map((manifest.players || []).map(p => [p.playerIndex, {
    index: p.playerIndex, name: cleanName(p.displayName), local: p.isLocal, frames: 0, aliveFrames: 0,
    regularMax: -Infinity, extraMax: -Infinity, totalMax: -Infinity, maxTotalFrame: null,
    extraOverOneFrames: 0, regularOverCapacityFrames: 0, aliveSeconds: 0, weightedTotal: 0,
    totalSum: 0, maxCapacityMismatch: 0, badStamina: 0, invalidPositionFrames: 0,
    jumps: 0, deaths: 0, passedOut: 0, isolation: new Isolation(),
  }]));
  const pairMap = new Map(), inventoryState = new Map(), guids = new Set();
  let previousSample = null, previousSequence = -1;
  // Only inventory and item events are retained for the ownership diagnostic.
  const itemTimeline = [];
  for (const info of manifest.chunks || []) {
    let chunk;
    try { chunk = JSON.parse(zlib.gunzipSync(fs.readFileSync(path.join(dir, info.fileName)))); }
    catch (e) { out.errors.push({ file: info.fileName, error: e.message }); previousSample = null; continue; }
    out.chunks++;
    if (info.sequence !== previousSequence + 1 || chunk.sequence !== info.sequence)
      out.errors.push({ file: info.fileName, error: 'sequence mismatch' });
    previousSequence = info.sequence;
    for (const [list, count] of [['samples', 'sampleCount'], ['inventorySnapshots', 'inventorySnapshotCount'], ['events', 'eventCount']])
      if ((chunk[list] || []).length !== info[count]) out.errors.push({ file: info.fileName, error: count + ' mismatch' });
    out.samples += (chunk.samples || []).length;
    out.inventory += (chunk.inventorySnapshots || []).length;
    out.events += (chunk.events || []).length;
    for (const s of chunk.samples || []) {
      if (!finite(s.time)) { out.errors.push({ error: 'invalid sample time' }); previousSample = null; continue; }
      const dt = previousSample ? s.time - previousSample.time : 0;
      if (dt < 0) out.backwards.push({ from: previousSample.time, to: s.time });
      if (previousSample && dt === 0) out.duplicateTimes++;
      if (dt > gapLimit) out.gaps.push({ from: previousSample.time, to: s.time, seconds: dt });
      const continuous = dt > 0 && dt <= gapLimit;
      const oldFrames = new Map((previousSample?.players || []).map(p => [p.playerIndex, p]));
      const frames = new Map((s.players || []).map(p => [p.playerIndex, p]));
      for (const c of cases) for (const offset of [-0.5, 0.5, 3.5, 8]) {
        const error = Math.abs(s.time - c.time - offset);
        if (error < 0.21 && (!c.frames[offset] || error < c.frames[offset].error))
          c.frames[offset] = { time: s.time, error, players: [...frames.values()].map(p => ({
            index: p.playerIndex, regular: p.regularStamina, extra: p.extraStamina,
            statuses: Object.fromEntries((p.statuses || []).map((v, i) => [manifest.statusTypeOrder[i], v]).filter(([, v]) => v)),
            afflictions: p.activeAfflictionTypes,
          })) };
      }
      const saved = savedDistances(s), oldSaved = savedDistances(previousSample);
      const nearest = new Map();
      for (const p of frames.values()) {
        const a = players.get(p.playerIndex);
        if (!a) { out.errors.push({ error: 'unknown player', index: p.playerIndex }); continue; }
        a.frames++;
        if (!p.dead) a.aliveFrames++;
        if (!validPosition(p)) a.invalidPositionFrames++;
        if (Object.hasOwn(p, 'petrifyAmount')) out.petrifyPropertyFrames++;
        const stoneIndex = manifest.statusTypeOrder.indexOf('Petrify');
        if (p.statuses?.[stoneIndex]) out.petrifyArrayNonzero++;
        for (const [i, v] of (p.statuses || []).entries()) {
          if (!finite(v)) { out.errors.push({ error: 'invalid status', time: s.time }); continue; }
          const key = manifest.statusTypeOrder[i] || String(i);
          out.statusMax[key] = Math.max(out.statusMax[key] || 0, v);
        }
        for (const v of p.activeAfflictionTypes || []) inc(out.afflictionIds, v);
        if (![p.regularStamina, p.extraStamina, p.maxStamina].every(finite)) { a.badStamina++; continue; }
        const total = p.regularStamina + p.extraStamina;
        a.totalSum += total;
        a.regularMax = Math.max(a.regularMax, p.regularStamina);
        a.extraMax = Math.max(a.extraMax, p.extraStamina);
        if (total > a.totalMax) { a.totalMax = total; a.maxTotalFrame = { time: s.time, ...p }; }
        if (p.extraStamina > 1.002) a.extraOverOneFrames++;
        if (p.regularStamina > p.maxStamina + 0.002) a.regularOverCapacityFrames++;
        a.maxCapacityMismatch = Math.max(a.maxCapacityMismatch,
          Math.abs(p.maxStamina - Math.max(0, 1 - (p.statuses || []).reduce((a, b) => a + b, 0))));
        const old = oldFrames.get(p.playerIndex);
        if (continuous && old && !old.dead && !p.dead && finite(old.regularStamina) && finite(old.extraStamina)) {
          a.aliveSeconds += dt;
          a.weightedTotal += dt * (old.regularStamina + old.extraStamina);
        }
      }
      const valid = [...frames.values()].filter(validPosition);
      for (let i = 0; i < valid.length; i++) for (let j = i + 1; j < valid.length; j++) {
        const a = valid[i], b = valid[j];
        const key = [a.playerIndex, b.playerIndex].sort((x, y) => x - y).join(':');
        const d = saved.get(key);
        if (!finite(d) || d < 0) continue;
        const units = Math.hypot(a.positionX - b.positionX, a.positionY - b.positionY, a.positionZ - b.positionZ);
        if (units > 10 && out.metersPerWorldUnitExamples.length < 6) out.metersPerWorldUnitExamples.push(d / units);
        nearest.set(a.playerIndex, Math.min(nearest.get(a.playerIndex) ?? Infinity, d));
        nearest.set(b.playerIndex, Math.min(nearest.get(b.playerIndex) ?? Infinity, d));
        if (!pairMap.has(key)) pairMap.set(key, { key, samples: 0, rows: [], seconds: 0, within25: 0, within50: 0, within100: 0 });
        const pair = pairMap.get(key); pair.samples++;
        const pa = oldFrames.get(a.playerIndex), pb = oldFrames.get(b.playerIndex);
        if (continuous && pa && pb && validPosition(pa) && validPosition(pb) && finite(oldSaved.get(key)) && oldSaved.get(key) >= 0) {
          const oldDistance = oldSaved.get(key);
          pair.rows.push([oldDistance, dt]); pair.seconds += dt;
          for (const threshold of [25, 50, 100]) if (oldDistance <= threshold) pair['within' + threshold] += dt;
        }
      }
      for (const a of players.values()) a.isolation.observe(s.time, nearest.get(a.index), gapLimit);
      previousSample = s;
    }
    for (const s of chunk.inventorySnapshots || []) {
      itemTimeline.push({ time: s.time, inventory: s });
      for (const item of s.slots || []) if (item.guid) guids.add(item.guid);
    }
    for (const e of chunk.events || []) {
      inc(out.eventTypes, e.type);
      const a = players.get(e.subjectPlayerIndex);
      if (a && e.type === 'PlayerJumped') a.jumps++;
      if (a && e.type === 'PlayerDied') a.deaths++;
      if (a && e.type === 'PlayerPassedOut') a.passedOut++;
      if (!e.itemName && !e.itemGuid) continue;
      for (const c of cases) if (Math.abs(e.time - c.time) <= 8) c.events.push(e);
      inc(out.itemEventTypes, e.type); inc(out.sourceKinds, e.type + ':' + e.source);
      if (e.targetPlayerIndex >= 0) out.targetedItems.push(e);
      if (e.type === 'ItemResourceChanged') {
        inc(out.resourceEvents, e.resourceKey || 'missing');
        if (e.value === e.previousValue) out.resourceEventsEqualValues++;
      }
      if (e.type === 'ItemConsumed') { inc(out.consumedByPlayer, e.subjectPlayerIndex); itemTimeline.push({ time: e.time, event: e }); }
    }
  }
  out.inventoryGuidCount = guids.size;
  if (cases.length) out.effectCaseWindows = cases;
  // Do not infer ownership across recordings whose clock has reset.
  if (!out.backwards.length) {
    const removals = new Map();
    itemTimeline.sort((a, b) => a.time - b.time || (a.event ? -1 : 1));
    for (const row of itemTimeline) {
      if (row.inventory) {
        const s = row.inventory;
        const next = new Set((s.slots || []).filter(i => i.guid).map(i => i.guid));
        for (const guid of inventoryState.get(s.playerIndex) || [])
          if (!next.has(guid)) removals.set(guid, { player: s.playerIndex, time: row.time });
        inventoryState.set(s.playerIndex, next);
      } else {
        const e = row.event;
        const owners = [...inventoryState].filter(([, items]) => items.has(e.itemGuid)).map(([index]) => index);
        const removal = removals.get(e.itemGuid);
        const candidate = owners.length === 1 ? owners[0] : owners.length === 0 && removal && row.time - removal.time <= 2 ? removal.player : null;
        inc(out.consumedOwnerEvidence, candidate === null ? 'unknownOrConflict' : candidate === e.subjectPlayerIndex ? 'same' : 'different');
        if (candidate !== null && candidate !== e.subjectPlayerIndex) {
          out.crossOwnerExamples ||= [];
          if (out.crossOwnerExamples.length < 12) out.crossOwnerExamples.push({ time: e.time, item: e.itemName, owner: candidate, consumer: e.subjectPlayerIndex, fromRemoval: !owners.length });
        }
      }
    }
  }
  for (const a of players.values()) {
    a.isolation.end('recordingEnd');
    a.isolation = a.isolation.episodes.sort((x, y) => y.seconds - x.seconds).slice(0, 3);
    a.rawSampleMeanTotal = a.totalSum / a.frames;
    a.aliveTimeWeightedMeanTotal = a.aliveSeconds ? a.weightedTotal / a.aliveSeconds : null;
    delete a.weightedTotal; delete a.totalSum;
    out.players.push(a);
  }
  for (const p of pairMap.values()) {
    const { rows, ...summary } = p;
    out.pairs.push({ ...summary, median: quantile(rows, 0.5), p90: quantile(rows, 0.9), p99: quantile(rows, 0.99),
      within25: p.within25 / p.seconds, within50: p.within50 / p.seconds, within100: p.within100 / p.seconds });
  }
  out.gapSeconds = out.gaps.reduce((s, g) => s + g.seconds, 0);
  return out;
}

tests();
if (!process.argv.includes('--self-test')) {
  const [root, output, gapArg] = process.argv.slice(2);
  if (!root || !output) throw new Error('Usage: node audit-recordings.mjs DATA_ROOT OUTPUT_JSON [GAP_LIMIT_SECONDS]');
  const resolvedOutput = path.resolve(output), resolvedRoot = path.resolve(root);
  if (resolvedOutput.toLowerCase().startsWith(resolvedRoot.toLowerCase() + path.sep)) throw new Error('Output must be outside the game data root');
  const gapLimit = Number(gapArg || 1);
  if (!finite(gapLimit) || gapLimit <= 0) throw new Error('Invalid gap limit');
  const start = performance.now();
  const runs = ids.map(id => {
    const r = audit(root, id, gapLimit);
    console.log(JSON.stringify({ id, chunks: r.chunks, samples: r.samples, inventory: r.inventory, events: r.events,
      errors: r.errors.length, backwards: r.backwards.length, gaps: r.gaps.length }));
    return r;
  });
  fs.writeFileSync(output, JSON.stringify({ researchVersion: 1, gapLimitSeconds: gapLimit,
    weighting: 'left endpoint, both endpoints valid, no bridging gaps/death; quantiles weighted by duration',
    elapsedSeconds: (performance.now() - start) / 1000, runs }, null, 2));
}

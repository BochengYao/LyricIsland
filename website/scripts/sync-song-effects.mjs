import { readFile, writeFile, mkdir, copyFile } from "node:fs/promises";
import { resolve, dirname, basename } from "node:path";
import { fileURLToPath } from "node:url";
import { createHash } from "node:crypto";

// Explicit source prevents accidentally publishing a catalog from an old checkout.
// node scripts/sync-song-effects.mjs <app>/Assets/SongEffects/catalog.json [--check]
const source = process.argv[2];
if (!source || source.startsWith("--")) throw new Error("Pass the release client's Assets/SongEffects/catalog.json path.");
const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const sourcePath = resolve(source);
const catalog = JSON.parse(await readFile(sourcePath, "utf8"));
if (catalog.schemaVersion !== 1 || !Array.isArray(catalog.entries)) throw new Error("Unsupported catalog schema.");
const ids = new Set();
const assets = new Set();
for (const entry of catalog.entries) {
  if (!entry.id || ids.has(entry.id) || !entry.match?.titleBase || !entry.match.artistAliases?.length || !entry.effect?.assets?.length) throw new Error(`Invalid song entry: ${entry.id}`);
  ids.add(entry.id);
  for (const asset of entry.effect.assets) {
    if (basename(asset) !== asset || !asset.endsWith(".svg")) throw new Error(`Invalid asset: ${asset}`);
    assets.add(asset);
  }
}
const target = resolve(root, "data/song-effects-catalog.json");
const assetTarget = resolve(root, "public/images/song-effects");
if (process.argv.includes("--check")) {
  if (JSON.stringify(catalog) !== JSON.stringify(JSON.parse(await readFile(target, "utf8")))) throw new Error("Website catalog differs from the release client.");
  for (const asset of assets) {
    if (!(await readFile(resolve(dirname(sourcePath), asset))).equals(await readFile(resolve(assetTarget, asset)))) throw new Error(`Asset differs: ${asset}`);
  }
  const provenance = JSON.parse(await readFile(resolve(root, "data/song-effect-source.json"), "utf8"));
  const fullscreen = resolve(dirname(sourcePath), "../../Fullscreen");
  for (const [file, expected] of Object.entries(provenance.renderers)) {
    const actual = createHash("sha256").update(await readFile(resolve(fullscreen, file))).digest("hex");
    if (actual !== expected) throw new Error(`Client renderer changed: ${file}. Audit the web port and effect facts before updating provenance.`);
  }
  console.log(`Verified ${ids.size} songs, ${assets.size} assets and ${Object.keys(provenance.renderers).length} renderer/matcher source hashes against the release client.`);
} else {
  await writeFile(target, JSON.stringify(catalog, null, 2) + "\n");
  await mkdir(assetTarget, { recursive: true });
  for (const asset of assets) await copyFile(resolve(dirname(sourcePath), asset), resolve(assetTarget, asset));
  console.log(`Synced ${ids.size} songs. Review display metadata in data/easter-songs.ts and the version note before publishing.`);
}

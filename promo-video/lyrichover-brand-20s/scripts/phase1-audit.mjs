import fs from "node:fs";
import path from "node:path";
import vm from "node:vm";
import { fileURLToPath } from "node:url";

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const at = (...parts) => path.join(projectRoot, ...parts);
const read = (...parts) => fs.readFileSync(at(...parts), "utf8");
const assert = (condition, message) => {
  if (!condition) throw new Error(message);
};
const count = (source, pattern) => (source.match(pattern) || []).length;
const filesWithExtension = (directory, extension) =>
  fs.readdirSync(at(...directory)).filter((name) => name.toLowerCase().endsWith(extension));

function evaluateBrowserScript(relativePath, exportedName) {
  const context = { window: {} };
  vm.runInNewContext(read(...relativePath), context, { filename: relativePath.join("/") });
  return context.window[exportedName];
}

const expectedScenes = [
  ["scene-01", 0, 2, "这一句，值得被看见。"],
  ["scene-02", 2, 5, "靠近时，让开一点。"],
  ["scene-03", 5, 9, "想怎么展开，就怎么呈现。"],
  ["scene-04", 9, 12, "换个播放器，歌词照常在场。"],
  ["scene-05", 12, 16, "唱到哪里，亮到哪里。"],
  ["scene-06", 16, 18.5, "少占一点，刚好看见。"],
  ["scene-07", 18.5, 20, "让歌词，始终在场。"]
];

const index = read("index.html");
const architecture = read("specs", "ARCHITECTURE.md");
const plan = evaluateBrowserScript(["scripts", "timeline-plan.js"], "LyricHoverPlan");
const morph = evaluateBrowserScript(["scripts", "morph-contracts.js"], "LyricHoverMorphContracts");
const manifest = JSON.parse(read("assets", "source-ppt", "manifest.json"));
const coralThread = read("assets", "production", "vectors", "coral-thread.svg");
assert(fs.existsSync(at("scripts", "render-final.ps1")), "Final render wrapper is missing.");

assert(plan.width === 1920 && plan.height === 1080, "Canvas must be 1920x1080.");
assert(plan.fps === 60 && plan.duration === 20, "Timeline must be 20 seconds at 60fps.");
assert(plan.worldWidth === 13440, "Seven-zone world width must be 13440px.");
assert(plan.scenes.length === expectedScenes.length, "Timeline must contain exactly seven scenes.");

for (let index = 0; index < expectedScenes.length; index += 1) {
  const [id, start, end, copy] = expectedScenes[index];
  const scene = plan.scenes[index];
  assert(scene.id === id, `Scene ${index + 1} id mismatch.`);
  assert(scene.start === start && scene.end === end, `${id} timing mismatch.`);
  assert(scene.copy === copy, `${id} copy mismatch.`);
  assert(scene.techniques.length > 0, `${id} must declare animation techniques.`);
  if (index > 0) assert(scene.start === plan.scenes[index - 1].end, `${id} must be contiguous.`);
}

assert(/data-composition-id="main"/.test(index), "Root composition main is missing.");
assert(/data-duration="20"/.test(index), "Root composition duration is not 20 seconds.");
assert(/data-width="1920"/.test(index) && /data-height="1080"/.test(index), "Root canvas metadata mismatch.");
assert(/gsap\.timeline\(\{ paused: true \}\)/.test(index), "Master timeline must be paused.");
assert(/window\.__timelines\.main\s*=\s*tl/.test(index), "Master timeline is not registered.");
assert(count(index, /data-scene="(?:01|02|03|04|05|06|07)"/g) === 7, "Seven scene zones are required.");
assert(count(index, /id="global-coral-thread"/g) === 1, "Global coral thread must be a unique persistent SVG.");
assert(count(index, /id="lyric-island"/g) === 1, "Lyric Island wrapper must be a unique persistent node.");
assert(count(index, /id="lyric-island-content-slot"/g) === 1, "Lyric Island content slot must be unique.");

assert(morph.selector === "#lyric-island", "Morph contract must target the persistent wrapper.");
assert(morph.contentSlot === "#lyric-island-content-slot", "Morph contract content slot mismatch.");
for (const state of ["opening", "hoverAvoidance", "compact", "expanded", "playerStable", "lyricFocus", "docked"]) {
  assert(morph.states[state], `Missing Morph state: ${state}.`);
}
assert(morph.states.hoverAvoidance.opacity === 0.34, "Hover avoidance opacity must remain 0.34.");
assert(morph.states.docked.y === 948, "Docked state must target the taskbar region.");

assert(/viewBox="0 0 13440 1080"/.test(coralThread), "Coral SVG viewBox mismatch.");
assert(/stroke="#F38F61"/.test(coralThread), "Coral SVG brand color mismatch.");
assert(count(coralThread, /<circle\b/g) === 7, "Coral SVG must expose seven scene nodes.");

assert(manifest.source.slideCount === 7, "Source PPT slide count mismatch.");
assert(manifest.inventory.svg === 15 && manifest.inventory.png === 27 && manifest.inventory.jpeg === 1, "Source manifest inventory mismatch.");
assert(filesWithExtension(["assets", "source-ppt", "vectors"], ".svg").length === 15, "Extracted SVG count mismatch.");
assert(filesWithExtension(["assets", "source-ppt", "raster"], ".png").length === 27, "Extracted PNG count mismatch.");
assert(filesWithExtension(["assets", "source-ppt", "raster"], ".jpeg").length === 1, "Extracted JPEG count mismatch.");
for (const alias of Object.values(manifest.productionAliases)) {
  assert(fs.existsSync(at("assets", "production", ...alias.split("/"))), `Missing production alias: ${alias}.`);
}

for (const section of [
  "## 1. 项目结构",
  "## 3. 20 秒时间轴",
  "## 4. Scene 职责边界",
  "## 5. 跨 Scene 持续存在的元素",
  "## 6. SVG 与位图使用",
  "## 7. 动画技术分配",
  "## 8. 场景连续过渡",
  "## 9. 9:16 适配策略",
  "## 10. 进入第二阶段前的确认点"
]) {
  assert(architecture.includes(section), `Architecture output is missing: ${section}.`);
}
for (const transition of ["01→02", "02→03", "03→04", "04→05", "05→06", "06→07"]) {
  assert(architecture.includes(transition), `Transition handoff is missing: ${transition}.`);
}

const mp4Files = [];
const visit = (directory) => {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    if (["node_modules", ".git"].includes(entry.name)) continue;
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) visit(fullPath);
    else if (entry.name.toLowerCase().endsWith(".mp4")) mp4Files.push(fullPath);
  }
};
visit(projectRoot);
assert(mp4Files.length === 0, "Phase one must not contain a final MP4 render.");

console.log("Phase 1 audit passed: 7/7 structural requirements verified.");
console.log("Canvas 1920x1080 | 60fps | 20s | 7 contiguous scenes | 0 MP4 renders");
console.log("Persistent objects: 1 coral thread | 1 Lyric Island wrapper | 1 content slot");
console.log("Source inventory: 15 SVG | 27 PNG | 1 JPEG | all production aliases present");

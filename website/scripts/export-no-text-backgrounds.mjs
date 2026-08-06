import { mkdir } from "node:fs/promises";
import path from "node:path";
import sharp from "sharp";

const outputDirectory = path.resolve(
  process.cwd(),
  "..",
  "output",
  "lyric-island-backgrounds-no-text",
);

const scenes = [
  { file: "01-hero-background.png", background: "#f3f0ee" },
  { file: "02-mouse-background.png", background: "#f3f0ee" },
  { file: "03-modules-background.png", background: "#fcfbfa" },
  { file: "04-collapse-background.png", background: "#141413" },
  { file: "05-translation-background.png", background: "#fcfbfa" },
  { file: "06-players-background.png", background: "#f3f0ee" },
];

await mkdir(outputDirectory, { recursive: true });

for (const scene of scenes) {
  await sharp({
    create: {
      width: 1920,
      height: 1080,
      channels: 3,
      background: scene.background,
    },
  })
    .png({ compressionLevel: 9 })
    .toFile(path.join(outputDirectory, scene.file));
}

const previewTiles = await Promise.all(
  scenes.map(async (scene, index) => ({
    input: await sharp(path.join(outputDirectory, scene.file))
      .resize(480, 270)
      .png()
      .toBuffer(),
    left: (index % 3) * 480,
    top: Math.floor(index / 3) * 270,
  })),
);

await sharp({
  create: {
    width: 1440,
    height: 540,
    channels: 3,
    background: "#141413",
  },
})
  .composite(previewTiles)
  .png({ compressionLevel: 9 })
  .toFile(path.join(outputDirectory, "backgrounds-preview.png"));

import { copyFile, mkdir } from "node:fs/promises";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const metadataDirectory = resolve(root, "dist", ".openai");
const metadataSource = resolve(root, ".openai", "hosting.json");

await mkdir(metadataDirectory, { recursive: true });

try {
  await copyFile(metadataSource, resolve(metadataDirectory, "hosting.json"));
} catch (error) {
  if (error?.code !== "ENOENT") {
    throw error;
  }
}

import { copyFile, mkdir } from "node:fs/promises";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const metadataDirectory = resolve(root, "dist", ".openai");

await mkdir(metadataDirectory, { recursive: true });
await copyFile(
  resolve(root, ".openai", "hosting.json"),
  resolve(metadataDirectory, "hosting.json")
);

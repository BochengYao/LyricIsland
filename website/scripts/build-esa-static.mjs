import { mkdir, rename, rm } from "node:fs/promises";
import { resolve } from "node:path";
import { spawn } from "node:child_process";

const root = resolve(import.meta.dirname, "..");
const apiDirectory = resolve(root, "app", "api");
const stagingDirectory = resolve(root, ".esa-build");
const stagedApiDirectory = resolve(stagingDirectory, "api");
const nextExecutable = resolve(
  root,
  "node_modules",
  ".bin",
  process.platform === "win32" ? "next.cmd" : "next"
);

await rm(resolve(root, "out"), { recursive: true, force: true });
await rm(stagingDirectory, { recursive: true, force: true });
await mkdir(stagingDirectory, { recursive: true });
await rename(apiDirectory, stagedApiDirectory);

try {
  const exitCode = await new Promise((resolveExit, reject) => {
    const child = spawn(nextExecutable, ["build"], {
      cwd: root,
      env: { ...process.env, ESA_STATIC_EXPORT: "1" },
      stdio: "inherit",
      shell: process.platform === "win32"
    });
    child.once("error", reject);
    child.once("exit", (code) => resolveExit(code ?? 1));
  });

  if (exitCode !== 0) {
    throw new Error(`Next.js static export failed with exit code ${exitCode}`);
  }
} finally {
  await rename(stagedApiDirectory, apiDirectory);
  await rm(stagingDirectory, { recursive: true, force: true });
}

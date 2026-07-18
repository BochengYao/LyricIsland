import { mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import { spawn } from "node:child_process";

const root = resolve(import.meta.dirname, "..");
const apiDirectory = resolve(root, "app", "api");
const stagingDirectory = resolve(root, "esa-source-staging");
const stagedApiDirectory = resolve(stagingDirectory, "api");
const functionBuildDirectory = resolve(root, "esa-dist");
const functionTemplate = resolve(root, "esa", "api.js");
const functionEntry = resolve(functionBuildDirectory, "entry.js");
const nextExecutable = resolve(
  root,
  "node_modules",
  ".bin",
  process.platform === "win32" ? "next.cmd" : "next"
);

await rm(resolve(root, "out"), { recursive: true, force: true });
await mkdir(stagingDirectory, { recursive: true });
await rm(stagedApiDirectory, { recursive: true, force: true });
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

  const replacements = {
    "__ESA_SUPABASE_URL__": process.env.SUPABASE_URL ?? "",
    "__ESA_SUPABASE_SERVICE_ROLE_KEY__": process.env.SUPABASE_SERVICE_ROLE_KEY ?? "",
    "__ESA_SUPABASE_STORAGE_BUCKET__": process.env.SUPABASE_STORAGE_BUCKET ?? "lyric-island-submissions",
    "__ESA_ADMIN_PASSWORD__": process.env.ADMIN_PASSWORD ?? "",
    "__ESA_ADMIN_SESSION_SECRET__": process.env.ADMIN_SESSION_SECRET ?? ""
  };
  let functionSource = await readFile(functionTemplate, "utf8");
  for (const [marker, value] of Object.entries(replacements)) {
    functionSource = functionSource.replaceAll(JSON.stringify(marker), JSON.stringify(value));
  }
  await mkdir(functionBuildDirectory, { recursive: true });
  await writeFile(functionEntry, functionSource, "utf8");
} finally {
  await rename(stagedApiDirectory, apiDirectory);
}

#!/usr/bin/env node
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { readFilterManifest } from "./src/filter-manifest.mjs";
import { cleanLockJson, smudgeLockJson } from "./src/filter.mjs";
import { toNinja } from "./src/commands/to-ninja.mjs";
import { toStandardPhase1 } from "./src/commands/to-standard.mjs";
import { deleteFromNinja } from "./src/commands/delete-from-ninja.mjs";
import { deleteFolder } from "./src/commands/delete-folder.mjs";

const HERE = dirname(fileURLToPath(import.meta.url));

function parseArgs(argv) {
  const out = {};
  for (let i = 0; i < argv.length; i += 2) {
    if (!argv[i].startsWith("--")) throw new Error(`bad arg: ${argv[i]}`);
    out[argv[i].slice(2)] = argv[i + 1];
  }
  return out;
}

async function readStdin() {
  const chunks = [];
  for await (const c of process.stdin) chunks.push(c);
  return Buffer.concat(chunks).toString("utf8");
}

async function main() {
  const [, , sub, ...rest] = process.argv;

  if (sub === "clean" || sub === "smudge") {
    const opts = parseArgs(rest);
    const projectRoot = opts["project-root"] || process.cwd();
    const fmPath = resolve(projectRoot, "Library", "UniClaude", "filter-manifest.json");
    const owned = readFilterManifest(fmPath).owned;
    const input = await readStdin();
    process.stdout.write(sub === "clean" ? cleanLockJson(input, owned) : smudgeLockJson(input, owned));
    return 0;
  }

  const opts = parseArgs(rest);
  const projectRoot = opts["project-root"];
  const libraryRoot = projectRoot ? resolve(projectRoot, "Library", "UniClaude") : undefined;
  const installerSourcePath = resolve(HERE, "installer.mjs");

  if (sub === "to-ninja") {
    if (!opts["git-url"]) throw new Error("--git-url required");
    const r = toNinja({ projectRoot, gitUrl: opts["git-url"], libraryRoot, installerSourcePath });
    console.log(JSON.stringify(r));
    return 0;
  }

  if (sub === "to-standard") {
    const r = toStandardPhase1({ projectRoot, libraryRoot, installerSourcePath });
    console.log(JSON.stringify(r));
    return 0;
  }

  if (sub === "delete-from-ninja") {
    const r = deleteFromNinja({ projectRoot, libraryRoot, installerSourcePath });
    console.log(JSON.stringify(r));
    return 0;
  }

  if (sub === "delete-folder") {
    await deleteFolder({
      path: opts["path"],
      touchManifest: opts["touch-manifest"],
      waitMs: parseInt(opts["wait-ms"] || "15000", 10),
      statusPath: opts["status-path"],
    });
    return 0;
  }

  console.error(`Unknown subcommand: ${sub}`);
  return 2;
}

main()
  .then(code => process.exit(code))
  .catch(err => {
    console.error(err.message || String(err));
    process.exit(1);
  });

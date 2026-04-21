import { test } from "node:test";
import { strict as assert } from "node:assert";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const installer = resolve(here, "..", "installer.mjs");

test("installer: no args exits 2", () => {
  const r = spawnSync("node", [installer], { encoding: "utf8" });
  assert.equal(r.status, 2);
  assert.match(r.stderr, /Unknown subcommand/);
});

test("installer: unknown subcommand exits 2", () => {
  const r = spawnSync("node", [installer, "bogus"], { encoding: "utf8" });
  assert.equal(r.status, 2);
  assert.match(r.stderr, /Unknown subcommand/);
});

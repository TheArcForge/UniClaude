#!/usr/bin/env node
const [, , subcommand, ...rest] = process.argv;

if (!subcommand) {
  console.error("usage: installer.mjs <subcommand> [args]");
  process.exit(2);
}

// Real implementations land in later tasks. Stub for now so the entrypoint is callable.
console.error(`Unknown subcommand: ${subcommand}`);
process.exit(2);

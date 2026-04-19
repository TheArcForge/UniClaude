# UniClaude Installer

Subcommand CLI used by the Unity editor to convert between Standard and Ninja install modes, and invoked by git as a clean/smudge filter on `Packages/packages-lock.json`.

## Subcommands

- `to-ninja --project-root <path> --git-url <url>` — convert Standard → Ninja
- `to-standard --project-root <path>` — convert Ninja → Standard (phase 1 only; schedules detached delete)
- `delete-from-ninja --project-root <path>` — uninstall from Ninja mode
- `delete-folder --path <p> --touch-manifest <p> --wait-ms <n>` — detached deletion helper
- `clean` — git clean filter: reads lock from stdin, strips owned entries, writes to stdout
- `smudge` — git smudge filter: reads lock from stdin, adds owned entries, writes to stdout

See `docs/superpowers/specs/2026-04-17-ninja-install-design.md` for the full design.

## Tests

`npm test` runs the vanilla Node test runner on `tests/*.test.mjs`.

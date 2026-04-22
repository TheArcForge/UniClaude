# UniClaude Installer

Subcommand CLI used by the Unity editor to convert between Standard and Ninja install modes, and invoked by git as a clean/smudge filter on `Packages/packages-lock.json`.

## Subcommands

- `to-ninja --project-root <path> --git-url <url>` — convert Standard → Ninja
- `to-standard --project-root <path> --unity-pid <pid> --unity-app-path <path>` — stage Ninja → Standard (writes marker + staged status; Unity spawns `finalize-transition` and exits)
- `delete-from-ninja --project-root <path> --unity-pid <pid> --unity-app-path <path>` — stage Ninja uninstall (same flow as `to-standard`)
- `finalize-transition --marker <path>` — detached helper: polls Unity PID, deletes embedded package, relaunches Unity
- `clean` — git clean filter: reads lock from stdin, strips owned entries, writes to stdout
- `smudge` — git smudge filter: reads lock from stdin, adds owned entries, writes to stdout

See `docs/superpowers/specs/2026-04-17-ninja-install-design.md` for the full design.

## Tests

`npm test` runs the vanilla Node test runner on `tests/*.test.mjs`.

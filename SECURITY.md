# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 0.1.x   | Yes       |

## Reporting a Vulnerability

If you discover a security vulnerability in UniClaude, please report it responsibly. **Do not open a public issue.**

### How to Report

Report vulnerabilities through [GitHub Security Advisories](https://github.com/ArcForge/com.arcforge.uniclaude/security/advisories/new). Include:

- A description of the vulnerability
- Steps to reproduce
- Potential impact
- Suggested fix (if you have one)

### What to Expect

- **Acknowledgment** within 48 hours of your report.
- **Assessment** within 7 days. We will confirm whether the issue is accepted and share our planned timeline.
- **Fix and disclosure** coordinated with you. We aim to release a patch within 30 days of confirming a vulnerability.
- **Credit** in the release notes (unless you prefer to remain anonymous).

We will not take legal action against researchers who report vulnerabilities in good faith and follow this policy.

## Security Model

UniClaude's security boundaries are documented in [ARCHITECTURE.md](docs/ARCHITECTURE.md#security). Key design decisions:

- **Localhost-only MCP server** — the HTTP transport binds to `127.0.0.1` and is not network-accessible.
- **Path sandboxing** — all file operations are validated to stay within the project root via `PathSandbox`.
- **No credential storage** — the Anthropic API key is read from the `ANTHROPIC_API_KEY` environment variable at runtime and is never persisted.
- **Permission system** — every MCP tool call requires explicit user approval before execution.

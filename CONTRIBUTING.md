# Contributing to UniClaude

Thanks for your interest in contributing to UniClaude! This guide covers everything you need to get started.

## Prerequisites

- **Unity 6000.3+** (Unity 6)
- **Node.js 18+**
- An **Anthropic API key** (for integration testing only)

## Development Setup

1. Clone the repository:
   ```bash
   git clone https://github.com/TheArcForge/UniClaude.git
   ```

2. Add the package to a Unity 6 project:
   - Open **Window > Package Manager**
   - Click **+** > **Add package from disk...**
   - Select the `package.json` in the cloned directory

3. Install sidecar dependencies:
   ```bash
   cd Sidecar~
   npm install
   ```

4. Build the sidecar (TypeScript to JavaScript):
   ```bash
   cd Sidecar~
   npm run build
   ```

The UniClaude window should now be available under **ArcForge > UniClaude**.

## Project Structure

```
Editor/
  Core/         # Data models, services, indexing, sidecar lifecycle
  MCP/          # MCP server, tool dispatch, domain reload strategies
    Tools/      # 60+ editor tools (one file per category)
    Transport/  # HTTP transport layer
    DomainReload/
  UI/           # Editor window, chat panel, input, rendering
    Input/      # Chat input subsystem
Tests/Editor/   # C# unit tests (NUnit)
  MCP/          # MCP tool tests
Sidecar~/       # Node.js bridge (TypeScript)
  src/          # Source code
  tests/        # TypeScript tests
```

Directories ending in `~` are ignored by Unity's asset pipeline but included in the package.

## Running Tests

### C# Tests

Open the Unity Test Runner (**Window > General > Test Runner**) and run the **EditMode** tests. All tests are in the `ArcForge.UniClaude.Tests.Editor` assembly.

From the command line:
```bash
Unity -runTests -testPlatform EditMode -projectPath /path/to/your/project -testResults results.xml
```

### TypeScript Tests

```bash
cd Sidecar~
npm test
```

This compiles the test TypeScript and runs the tests using Node.js's built-in test runner.

## Making Changes

### C# Code

- All C# code lives under `Editor/` and runs in the Unity Editor only (no runtime code).
- Assembly definition: `ArcForge.UniClaude.Editor.asmdef`
- Namespace: `UniClaude.Editor` (sub-namespaces for MCP, UI, etc.)
- Use `[MCPTool]` and `[MCPToolParam]` attributes to add new tools. See `Editor/MCP/Tools/` for examples.

### TypeScript Code

- Source is in `Sidecar~/src/`, compiled output goes to `Sidecar~/dist/`.
- After editing TypeScript, rebuild with `npm run build` in the `Sidecar~/` directory.
- The sidecar uses Express for HTTP and the Anthropic Agent SDK for Claude integration.

### Adding an MCP Tool

1. Create a static method in the appropriate file under `Editor/MCP/Tools/` (or create a new file for a new category).
2. Annotate with `[MCPTool("tool_name", "Description")]`.
3. Add `[MCPToolParam("param", "Description")]` to each parameter.
4. Return `MCPToolResult.Success(...)` or `MCPToolResult.Error(...)`.
5. Add tests in `Tests/Editor/MCP/`.

The tool is discovered automatically via reflection at editor startup.

## Pull Request Process

1. Fork the repository and create a branch from `main`.
2. Make your changes with clear, focused commits.
3. Add or update tests for any changed behavior.
4. Ensure all C# and TypeScript tests pass.
5. Update `CHANGELOG.md` under an `[Unreleased]` section if your change is user-facing.
6. Open a pull request with a description of what changed and why.

Keep PRs focused on a single concern. Large features should be discussed in an issue first.

## Reporting Bugs

Open an issue on [GitHub Issues](https://github.com/TheArcForge/UniClaude/issues) with:

- Unity version and OS
- Steps to reproduce
- Expected vs. actual behavior
- Output from `/healthcheck` if relevant

## Code Style

- Follow existing patterns in the codebase.
- XML doc comments on all public types and members.
- Use `PathSandbox.Resolve()` / `PathSandbox.ResolveWritable()` for all file path operations in MCP tools.
- Use `Undo.RecordObject()` / `Undo.RegisterCreatedObjectUndo()` for scene/component modifications.
- Prefer `MCPToolResult.Success()` / `MCPToolResult.Error()` over throwing exceptions in tools.

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).

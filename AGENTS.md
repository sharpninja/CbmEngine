# Agent Instructions

## Session Start

1. Read and process the complete `AGENTS-README-FIRST.yaml` marker in the repo root before relying on MCP. It contains the current endpoint, workspace path, API key, required plugin contract, trust bootstrap, and failure-reporting rules.
2. Verify marker trust before MCP use: use the required Codex plugin status flow to validate marker signature, health nonce, workspace path, session id, current turn, and supported namespaces.
3. Use the `mcpserver-codex-plugin` helper flow for session logging, TODOs, requirements, triage, memory, import/export, and traceability. Do not use raw REST when a plugin wrapper or MCP tool is available.
4. If a helper reports an unknown workspace path, trust failure, nonce mismatch, 401, or another registration error, report the exact command and response, stop MCP writes for that capability, record the unavailable state, and continue non-MCP work honestly.

On every subsequent user message:

1. Follow `AGENTS-README-FIRST.yaml` and the active plugin helper instructions for the session-log turn lifecycle.
2. Complete the user's request.

## Rules

1. Keep this file focused on durable workspace policy and conventions; avoid duplicating marker-file operational procedures.
2. Use PowerShell.Mcp for all shell commands in this workspace. Run commands from `F:\GitHub\CbmEngine` unless a task explicitly requires another location.
3. Use helper modules, plugin scripts, or MCP tools for session log and TODO operations. Do not make raw API calls when a supported helper exists.
4. Persist session-log updates immediately after meaningful changes when MCP session logging is available. If it is unavailable, state that clearly and preserve the failure evidence.
5. Capture meaningful turn detail: interpretation, response, status, actions, files modified, decisions, requirements discovered, blockers, and relevant validation output.
6. Do not fabricate information. If you made a mistake, acknowledge it. Distinguish facts from speculation.
7. Prioritize correctness over speed. Do not ship code you have not verified compiles and is logically sound.
8. Preserve user work. The worktree may be dirty; do not revert, delete, or overwrite changes you did not make unless the user explicitly asks.
9. Build JSON/YAML payloads from native objects and serialize them. Do not handwrite fragile JSON or YAML payload strings. For YAML files, mutate the parsed object and serialize the complete document.
10. Public APIs should have XML documentation. Follow nullable annotations, existing project style, DRY, SOLID, and the current architecture boundaries.
11. Keep responses concise. Do not use table-style output in user responses unless the user asks for it.

## Workspace Layout

- `CbmEngine.slnx` - solution containing CbmEngine, tests, tools, and selected ViceSharp submodule projects.
- `Directory.Build.props` - shared .NET settings; projects target `net10.0` with nullable reference types and implicit usings enabled.
- `src/CbmEngine.Abstractions` - contracts and value types only.
- `src/CbmEngine.Pipeline` - build-time content pipeline: VIC palette, bitmap encoding, CbmVid format, MIDI reader.
- `src/CbmEngine.Systems` - engine runtime over ViceSharp: machine, memory, sprites, tilemap, SID, cartridges, video, MIDI, boot helpers.
- `src/CbmEngine.Host.MonoGame` - windowing, blit, input, audio, and threaded emulator pump.
- `src/CbmEngine.Game.Sample` - runnable Frost Point/sample host and reference usage.
- `tests/CbmEngine.Tests.Unit` - xUnit unit tests.
- `tests/CbmEngine.Tests.Integration` - integration tests for ROM, host, boot, and emulation-backed paths.
- `tests/CbmEngine.Tests.Shared` - shared test helpers.
- `tools/` - fixture builders, CbmVid tools, capture analyzers, and supporting scripts.
- `external/vice-sharp` - git submodule for the ViceSharp emulation core. Keep submodule changes intentional and isolated.
- `docs/` - user and architecture documentation.
- `docs/Project/wiki/github` and `docs/Project/wiki/azure` - requirements, mapping, and test traceability documents.

There is currently no `.github/copilot-instructions.md` or `global.json`; do not claim those files exist unless they are added later.

## Build And Test

Prerequisites:

- .NET 10 SDK. Current inspected SDK: `10.0.301`.
- Git submodules initialized, especially `external/vice-sharp`.
- CC65 (`ca65`, `ld65`) on `PATH` for cartridge building and the default sample path.
- FFmpeg on `PATH` only for video-to-CbmVid encoding.
- VICE ROMs are expected under `external/vice-sharp/native/vice/vice/data` and are discovered by the sample when run from this checkout.

Recommended validation commands:

```powershell
dotnet restore CbmEngine.slnx -v minimal /p:RestoreFallbackFolders=
dotnet build CbmEngine.slnx --no-restore -v minimal
dotnet test tests\CbmEngine.Tests.Unit\CbmEngine.Tests.Unit.csproj --no-restore -v minimal
```

The explicit `RestoreFallbackFolders=` restore is intentional for this Windows checkout; stale generated assets can otherwise reference the missing fallback folder `C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages` and fail before compilation.

Use broader validation when touching integration-heavy behavior:

```powershell
dotnet test tests\CbmEngine.Tests.Integration\CbmEngine.Tests.Integration.csproj --no-restore -v minimal
```

Run the sample when relevant:

```powershell
dotnet run --project src\CbmEngine.Game.Sample
dotnet run --project src\CbmEngine.Game.Sample -- --headless --frames=120 --dump-png=out.png
```

## Architecture Conventions

- CbmEngine is hardware-honest: game code writes to real C64 memory/register concepts over ViceSharp. Avoid abstractions that hide or fake the VIC-II, SID, 6510, or memory map without a clear requirement.
- Respect the layer boundaries: Abstractions define shape, Pipeline handles build-time content, Systems wraps emulation and services, Host.MonoGame owns host UI/audio/threading, and Game.Sample demonstrates usage.
- Keep `CbmEngine.Pipeline` independent of the emulator runtime.
- Treat `MonoGameHost` and `CbmViewport` threading carefully. `IGame.Update` may run on the emulator pump thread; do not call UI framework APIs from it. Use existing enqueue/input mechanisms for cross-thread interaction.
- Preserve the RAM vs I/O rule. RAM access uses memory views/ranges/snapshots; I/O registers use `ReadIo`/`WriteIo` or services that route correctly.
- Keep generated fixtures and visual/audio/video test inputs as durable documentation unless the user explicitly asks to regenerate or remove them.

## Requirements And Traceability

- Before implementing a requirement-bearing change, inspect `docs/Project/wiki/github/Requirements-Matrix.md` and the matching Functional, Technical, Testing, and TR-per-FR mapping files.
- Use FR/TR/TEST identifiers in plans, commits, and validation notes when the change maps to tracked requirements.
- Update both requirements docs and tests when behavior changes. Do not leave deferred work hidden in skipped tests.
- If MCP requirements tooling is available, prefer it for requirements operations; otherwise edit the documented requirements files carefully and report the MCP limitation.

## MCP And Plugin Notes

- `AGENTS-README-FIRST.yaml` is the rendered runtime marker and may be regenerated by MCP Server. Treat it as current for endpoint/key details, but validate trust and health through the Codex plugin before relying on MCP writes.
- Codex must use the active `mcpserver-codex-plugin` wrapper before direct REPL calls. Run `Invoke-CodexMcpPlugin.ps1 -Command Status -WorkspacePath 'F:\GitHub\CbmEngine'` from the plugin root to verify plugin root, marker trust, health nonce, workspace path, session id, current turn, and supported namespaces.
- Invoke workflow methods through `Invoke-CodexMcpPlugin.ps1 -Command Invoke -Method <method> -Params <yaml>` so parameters are supplied through stdin or wrapper-supported argument handling instead of fragile nested shell quoting.
- Complete turns intentionally through the plugin, using `Invoke-CodexMcpPlugin.ps1 -Command CompleteTurn -Response <final response>` when the stop gate cannot infer completion.
- Use `mcpserver-codex-plugin` hook scripts for Codex turn handling when available: open the turn, verify after source-code edits, and run the stop gate before final response.
- Plugin or MCP failures discovered while working must be reported through the active plugin triage surface. If triage is unavailable because MCP or the plugin is unavailable, write the normal failsafe YAML report for later replay and continue non-MCP work. Do not substitute raw REST, another agent's plugin, TODOs, GitHub issues, or an alternate reporting channel.
- If plugin scripts or MCP calls fail, report the exact helper path, method, request id if present, and error response. Do not claim session logging, TODO, requirements, triage, or memory operations succeeded when they did not.
- Do not bypass the plugin with raw REST for session log, TODO, requirements, memory, triage, import/export, or traceability operations.

## Git Hygiene

- Check `git status --short` before edits and before final response.
- Keep changes scoped to the user request.
- Do not commit or push unless the user explicitly asks.
- If generated restore/build files change under ignored `bin/` or `obj/`, leave them alone unless they become tracked changes.

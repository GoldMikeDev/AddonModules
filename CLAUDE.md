# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## How to work with me

My goal here is to learn, not to accumulate code I don't understand. Act as a teacher/mentor, not a code-generation service:

- Don't just write the fix or feature for me. Explain the relevant concept, point me at the file/lines involved, and let me attempt the change first.
- Prefer asking guiding questions ("what do you think happens if the write head wraps past `maxIndex` here?") over stating the answer outright.
- If I ask you to implement something directly, it's fine to do it — but explain the reasoning as you go, and call out what I should understand about *why* it works, not just *what* changed.
- When reviewing my code, explain the bug/issue and why it's wrong before offering the corrected version, so I have a chance to reason about it myself.
- It's fine to give me the full answer when I'm stuck and ask for it directly, or for mechanical/boilerplate work — use judgment about when teaching mode vs. doing mode fits.

## What this repository is

`AddonModules` is a shared-source library of standalone C# utility/module files — not a standalone buildable project. There is no `.csproj`/`.sln`, no test suite, and no build/lint/test commands to run in this repo itself. Files here are meant to be linked or copied into other .NET console-tool projects (the user's various `dotnet tool` CLI apps), which then compile them as part of their own project.

Because of this:
- Do not add a `.csproj`/`.sln` or try to `dotnet build` this repo unless explicitly asked — it's not intended to be self-contained.
- The root namespace used throughout is `Rename.AddonModules` (and `Rename.AddonModules.Extensions`). This is a placeholder: consuming projects are expected to search-and-replace `Rename` with their own project's root namespace when they pull these files in. Keep this placeholder convention when adding new files rather than hardcoding a real namespace.
- `Admin/Elevator.cs` depends on CsWin32-generated code from `Admin/NativeMethods.txt` — consuming projects must add the `Microsoft.Windows.CsWin32` package and reference the `.txt` file via `<AdditionalFiles Include="...\Admin\NativeMethods.txt" />` in their `.csproj`, or the generated `Windows.Win32.*` types won't resolve.

## Layout and what each module does

- **`CSRB.cs`** — A custom persistent circular-ring-buffer file format ("CSRB") used as command history storage for CLI tools (analogous to shell readline history), stored at `%USERPROFILE%\.dotnet\tools\CSRB\CSRB.csrb`. Key design points to understand before touching this file:
  - The file is a fixed grid of `maxIndex` (4096) records, each `maxIndexLength` (256) bytes, addressed as byte ranges via `Extensions/RangeExtensions.cs` (`ReadField`/`WriteField` treat a C# `Range` as a fixed-width field descriptor into a `byte[,]`).
  - Each record has fixed-width fields: `index`, read-head pointer (`rh`), write-head pointer (`wh`), a `parity` bit, a `concat` count (for values spanning multiple records), and the `payload`.
  - Read/write heads are located by scanning for pointer markers; `ValidateAndRepair`/`ParitySearch` detect corruption (missing/duplicated heads) and self-repair using the parity bit and concat chains, emitting warnings (via `ConsoleSpinner` if present, else `Console.WriteLine`).
  - Values longer than one record's payload are split across multiple concatenated records (`concat` field tracks chain position/length); `Write`/`Read` handle chunking/reassembly, including careful UTF-8 rune-boundary splitting.
  - Most public methods accept an optional `ConsoleSpinner? spinner` so callers can route status/warning messages either through the spinner's queued output or directly to the console.

- **`JavaPropertiesParser.cs`** — A parser for Java `.properties` file syntax (line continuations via trailing `\`, `#`/`!` comments, `:`/`=`/whitespace separators, `\uXXXX` and `\n`/`\t`/etc. escapes). Populates the static `config` dictionary. `LoadProperties` reports failures via `Console.WriteLine` and rethrows — no external logger dependency.

- **`Spinner.cs`** (`ConsoleSpinner`) — Thread-based console spinner for long-running CLI operations. Supports queuing lines to print (`Enqueue`) that get flushed after the spinner stops (`StopAndFlush`/`RequestStopAndFlush`), and a `minSpinnerMs` to avoid a spinner flashing too briefly. Takes a shared `Lock` so multiple spinner-aware components can coordinate console output.

- **`Extensions/ParseExtensions.cs`** — `Parse(type, data)`: a reflection-based generic `TryParse` dispatcher keyed by a string type name (`"int"`, `"bool"`, `"string"`, etc.), throwing `ParseException` on unsupported types or parse failure. Used by `CSRB.cs` to read typed field values.

- **`Extensions/RangeExtensions.cs`** — Treats `System.Range` as a fixed-width field descriptor over a `byte[,]` buffer: `InclusiveRange`, `Length()`, `ReadField`/`WriteField`. This is the low-level field-access primitive `CSRB.cs` is built on.

- **`Extensions/StackExtensions.cs`** — Small naming-convenience aliases over `Stack<T>` (`Current`/`GoUp`/`GoDown` for `Peek`/`Pop`/`Push`), presumably for readability in a caller modeling directory/menu navigation.

- **`ToolBox/ToolBoxHandshake.cs`** — `VerifyToolBoxHost()`: a stdin handshake used by a CLI tool to confirm it was launched by the "ToolBox" parent process. If the `TOOLBOX_HOST` environment variable is `"1"`, it short-circuits to success immediately (using `TOOLBOX_PREFIX` for output if set) instead of waiting on stdin — this is a convenience shortcut for the expected case, not a security check; the underlying stdin handshake itself isn't tamper-proof either. Otherwise it reads a line from stdin with a 5-second timeout, expecting the exact string `"ToolBox is open"`, showing the shared `ConsoleSpinner` while waiting.

- **`Updater/Updater.cs`** + **`Updater/UpdateScript.ps1`** — Self-update mechanism for a `dotnet tool` global CLI package:
  - `TryHandleUpdateCommandTree` parses `--update`/`--updateMajor`/`--updateMinor` (mutually exclusive) plus `--forceUpdate`/`--skipVersion` args.
  - `UpdateTool` locates the consuming project's `.csproj` (walking up from CWD, then searching `~\source\repos` and the user profile), hashes the currently-installed `.nupkg` vs. a freshly built/packed one to skip no-op updates, bumps semver in the `.csproj` (`<Version>` tag) unless `--skipVersion`, then launches a PowerShell process running `UpdateScript.ps1`.
  - `UpdateScript.ps1` waits for the original process PID to exit, copies the new `.nupkg` into the local NuGet package source (read from `%APPDATA%\NuGet\NuGet.Config`), runs `dotnet tool update --global`, and rolls back the version bump in the `.csproj` on failure. It only shows a "Press Enter to exit" prompt when it's running in its own standalone console window (detected by comparing the console window's owning PID, via `GetConsoleWindow`/`GetWindowThreadProcessId`, to `$PID`) rather than one inherited from the calling process — so it doesn't hang a console someone else is already watching.
  - This module no longer handles elevation or credential prompting itself — that's been extracted to `Admin/Elevator.cs`, since the update flow doesn't require elevation. It's still Windows-only (`[assembly: SupportedOSPlatform("windows")]`), now because `FindPowerShellExe` and the `.dotnet\tools` package paths assume Windows path conventions rather than because of any P/Invoke elevation check.

- **`Admin/Elevator.cs`** + **`Admin/NativeMethods.txt`** — Standalone process-elevation helper, split out of `Updater.cs` so it can be reused wherever elevation is actually needed (the update flow no longer requires it). `ProcessElevated()` checks whether the current process token is elevated by reading a `TOKEN_ELEVATION` struct via CsWin32-generated `OpenProcessToken`/`GetTokenInformation` calls into a `stackalloc` buffer; `CLIcredentials` prompts for an admin username/password (`SecureString`) if not elevated. Uses CsWin32 (`Microsoft.Windows.CsWin32`) instead of hand-written P/Invoke, guarded by a runtime `OperatingSystem.IsWindowsVersionAtLeast(...)` check matching the minimum OS version CsWin32 declared for the P/Invoke'd functions — this satisfies the platform-compatibility analyzer without needing an assembly-level `[SupportedOSPlatform]` attribute, so the file works standalone even in a project that doesn't also have `Updater.cs`'s assembly attribute.

## Conventions to preserve when editing

- Warning/status messages consistently use emoji prefixes (⚠️, ✅, ❌, 🔄, etc.) and are routed through a `ConsoleSpinner?` parameter when one is available, falling back to `Console.WriteLine`/`Console.Error.WriteLine` otherwise — match this pattern in any new code paths.
- Field-width/layout constants in `CSRB.cs` (e.g. `maxIndex`, `maxIndexLength`, header field ranges) are tightly coupled — the inline comments document required invariants (e.g. `payload` range starts at `headerLength`, `maxConcat` must be less than `maxIndex`). Don't change one without checking the others.
- Braces and formatting follow a compact single-line-block style (`if (x) { y; return; }`) throughout — match this rather than expanding to multi-line braces.

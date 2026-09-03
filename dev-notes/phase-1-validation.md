# Phase 1 validation record

Validation date: 2026-09-03
Environment: Windows x64, standard project checkout

## Installed and detected

- .NET SDK 8.0.424 selected by `global.json`;
- .NET Windows Desktop runtime 8.0.30;
- Git 2.48.1.windows.1;
- GitHub CLI 2.97.0, authenticated to GitHub for the repository workflow;
- Inno Setup 6.7.3 installed for the current user under LocalAppData;
- Windows SDK x64 `signtool.exe` 10.0.26100.0 available for an optional future signing hook.

Visual Studio is not installed. It is intentionally optional because the supported .NET CLI workflow completed every compile/test/publish gate.

No printer is configured on the primary implementation machine. The print spooler is running, but enabling the Windows Microsoft Print to PDF feature requires an elevated Windows session that is not available to this development task. An independent reproduction reviewer reported Microsoft Print to PDF on the second validation machine. Printer availability is machine-specific and does not block Phase 1; a physical or PDF printer remains a Phase 4 manual-acceptance prerequisite for whichever machine runs that phase.

## Successful checks

- `./eng/verify-tools.ps1`;
- locked solution restore using committed package lock files;
- `dotnet format --verify-no-changes`;
- Release solution build with zero warnings and zero errors;
- four xUnit v3 smoke tests with Coverlet collection, including exact production project-edge and Core metadata boundary checks;
- Ical.Net 5.2.3 parsing of timezone, weekly recurrence, and EXDATE data;
- WPF `FixedDocument` creation on an STA thread;
- NuGet outdated-package check: no updates reported;
- NuGet direct/transitive vulnerability check: no vulnerable packages reported;
- self-contained `win-x64` single-file publish;
- published WPF executable startup using `--smoke-test`, exit code 0;
- self-contained startup validation with both x64 and general `DOTNET_ROOT` variables pointed to a nonexistent directory and machine-wide runtime lookup disabled, plus a supplemental host-resolution trace;
- Inno Setup compilation of the lowest-privilege toolchain-smoke installer; and
- `git diff --check`.

The generated publish, coverage, and installer-smoke artifacts live under ignored `artifacts/` and are not committed.

# Development environment

## Supported environment

Yahoo Month Print targets Windows 10/11 x64 and `net8.0-windows`. The command-line workflow does not require Visual Studio, but Visual Studio 2022 or its Build Tools can be installed for IDE use. A separately installed .NET runtime is not required on an end-user machine because release publishing is self-contained.

The repository's `global.json` pins the supported .NET 8 SDK feature band. `Directory.Packages.props` centrally pins NuGet dependencies, and committed lock files make CI restores repeatable.

## Required tools

Install these tools for local builds:

- Git;
- .NET 8 SDK, including the Windows Desktop targeting pack supplied by the SDK installer.

PR delivery additionally uses an authenticated GitHub CLI (`gh`). Installer work requires Inno Setup 6.3 or newer, including its command-line compiler `ISCC.exe`.

Example current-user installs from a PowerShell prompt are:

```powershell
winget install --id Git.Git --exact
winget install --id GitHub.cli --exact
winget install --id Microsoft.DotNet.SDK.8 --exact
winget install --id JRSoftware.InnoSetup --exact --scope user
gh auth login
```

Visual Studio 2022 Community or Build Tools with the “.NET desktop development” workload is optional. The repository uses the .NET CLI as the canonical build path so a full IDE is not a build prerequisite.

For Phase 4 manual testing, configure a physical printer or Windows PDF printer and arrange access to a clean, non-administrator Windows account or disposable VM. The printer and VM are manual acceptance resources, not compile-time dependencies.

Authenticode signing is optional. If release signing will be used, install the Windows SDK signing tools. Never place a certificate, private key, password, or developer-specific signing path in this repository.

## Verify the toolchain

From the repository root:

```powershell
./eng/verify-tools.ps1
./eng/verify-tools.ps1 -BuildToolsOnly
```

The default check validates required build, PR-delivery, and packaging tools and reports signing tools, printers, and Visual Studio as optional/manual capabilities. `-BuildToolsOnly` checks just Git, the selected .NET SDK, and Windows Desktop support; CI runs this mode so the documented entry point cannot silently rot.

## Restore, build, test, and publish

```powershell
dotnet restore YahooMonthPrint.sln
dotnet format YahooMonthPrint.sln --verify-no-changes --no-restore
dotnet build YahooMonthPrint.sln --configuration Release --no-restore
dotnet test YahooMonthPrint.sln --configuration Release --no-build --collect:"XPlat Code Coverage" --results-directory artifacts/test-results
dotnet publish src/YahooMonthPrint.App/YahooMonthPrint.App.csproj --configuration Release --runtime win-x64 --self-contained true --no-restore --output artifacts/publish/win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
./eng/package-smoke.ps1
```

Use `--locked-mode` for clean/CI validation after lock files have been generated. CI runs the locked restore, build, tests with coverage, vulnerability report, self-contained publish, and Inno Setup smoke compile on a Windows runner.

## Local application smoke test

Launch either the normal Release build or the self-contained published executable:

```powershell
dotnet run --project src/YahooMonthPrint.App/YahooMonthPrint.App.csproj --configuration Release
./artifacts/publish/win-x64/YahooMonthPrint.App.exe
./artifacts/publish/win-x64/YahooMonthPrint.App.exe --smoke-test
./eng/verify-self-contained.ps1
```

The `--smoke-test` option constructs the WPF shell, validates XAML/application startup, and exits without showing a persistent window. `verify-self-contained.ps1` additionally points the x64 and general `DOTNET_ROOT` variables at a deliberately absent directory, disables machine-wide runtime lookup, and requires a successful exit. It captures the .NET host trace as supplemental diagnostic evidence, but trace wording changes do not fail the build. The Phase 1 shell only confirms that WPF startup and project composition work. Calendar behavior begins in Phase 2.

## Optional dependency checks

Run these before changing a pinned package version:

```powershell
dotnet list YahooMonthPrint.sln package --outdated
dotnet list YahooMonthPrint.sln package --vulnerable --include-transitive
```

Package upgrades should be isolated, reviewed for license/API changes, and followed by a fresh restore, build, and complete test run.

## Secrets and integration-test data

Automated tests use sanitized fixtures. Future real-Yahoo integration tests will read `YMP_TEST_YAHOO_USER` and `YMP_TEST_YAHOO_APP_PASSWORD` only when explicitly enabled. Do not put those values in tracked files, command transcripts, CI artifacts, screenshots, settings, cache fixtures, or logs.

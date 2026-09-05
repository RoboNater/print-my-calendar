# Build and release

## One version source

`Directory.Build.props` supplies the development `VersionPrefix`. Release automation chooses one version and passes it to both `dotnet publish` (`-p:Version=...`) and `eng/package-release.ps1 -Version ...`. The application assemblies and `YahooMonthPrint-Setup.exe` therefore receive the same version. CI uses `1.0.<run number>` for reproducible unsigned development artifacts.

## Unsigned development release

From a clean checkout:

```powershell
dotnet restore YahooMonthPrint.sln --locked-mode
dotnet build YahooMonthPrint.sln --configuration Release --no-restore
dotnet test YahooMonthPrint.sln --configuration Release --no-build
dotnet publish src/YahooMonthPrint.App/YahooMonthPrint.App.csproj --configuration Release --runtime win-x64 --self-contained true --no-restore --output artifacts/publish/win-x64 -p:Version=1.0.0 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
./eng/verify-self-contained.ps1
./eng/package-release.ps1 -Version 1.0.0
```

The final files are `artifacts/installer/YahooMonthPrint-Setup.exe` and its `.sha256` checksum. The installer uses lowest privileges, installs under `%LOCALAPPDATA%\Programs\YahooMonthPrint`, registers an uninstaller, creates a Start Menu shortcut, and offers an optional desktop shortcut.

The stable installer AppId supports in-place upgrades/reinstalls. Uninstall runs the installed application's local-cleanup mode before removing program files. The policy is intentionally privacy-first: the Yahoo Credential Manager entry, settings, cache, and logs are removed. Uninstall does not revoke the app password at Yahoo and never changes Yahoo calendar data.

## Optional Authenticode signing

Unsigned development builds are expected. To sign, pass a Windows SDK `signtool.exe` path and a certificate path to `package-release.ps1`, and supply the password only through the protected `YMP_SIGNING_PASSWORD` environment variable:

```powershell
./eng/package-release.ps1 -Version 1.0.0 -SignToolPath <signtool.exe> -SigningCertificatePath <certificate.pfx>
```

The script signs the published executable before packaging and the installer afterward, using SHA-256 and HTTPS timestamping. It fails if signing was requested but any required input is unavailable. Never commit or print a certificate path, private key, or password.

## Visual and clean-machine validation

Generate reviewable Letter and A4 images with:

```powershell
./artifacts/publish/win-x64/YahooMonthPrint.App.exe --render-print-samples artifacts/print-samples
```

Before tagging v1.0, install the exact candidate on a clean standard-user Windows 10 or 11 machine. Complete the specification's Yahoo scenario, offline restart, all filters, hide/restore, Letter and A4 preview, Microsoft Print to PDF, another real or virtual printer, cancellation/error handling, upgrade/reinstall, and uninstall. Confirm no UAC or separate .NET install is needed; grayscale output is legible; Yahoo is unchanged; the credential is gone after uninstall; and settings, cache, logs, and artifacts contain no password or Authorization header. Record only sanitized evidence.

Phase 4 merging is not the release tag. Tag only after this manual validation succeeds against the installer rebuilt from the merged default branch.

# Build and release foundation

## Version source

`Directory.Build.props` supplies the development `VersionPrefix`. A release build overrides the version once at the command line or in release automation (for example, `-p:Version=1.0.0`); application and final-installer metadata will consume that same value in Phase 4.

## Unsigned development artifact

Restore and publish the self-contained Windows x64 application as documented in `development.md`. The result under `artifacts/publish/win-x64` must run without a separately installed .NET runtime.

`./eng/package-smoke.ps1` compiles a minimal lowest-privilege Inno Setup definition. It validates the compiler only; it is not the v1.0 installer. Phase 4 will replace this proof with the versioned `YahooMonthPrint-Setup.exe`, shortcuts, Add/Remove Programs metadata, upgrade/uninstall behavior, and clean standard-user acceptance testing.

## Signing hook

Unsigned development builds are expected. The eventual release job may invoke `signtool.exe` after publishing and after installer compilation when a certificate is supplied by the release environment. Certificate material and passwords must be provided by a protected secret store and must never be copied into the repository, source-controlled configuration, logs, or build artifacts.

The release pipeline must fail clearly when signing is explicitly requested but the signing tool or credential is unavailable. It must not silently publish an artifact described as signed.

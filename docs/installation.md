# Install Yahoo Month Print

## End-user requirements

Yahoo Month Print supports 64-bit Windows 10 and Windows 11. The installer is self-contained, so an end user does not need to install .NET, Visual Studio, Git, Inno Setup, or any other development tool.

To connect the application to Yahoo Calendar, the user needs:

- an internet connection for initial setup and calendar refreshes;
- a Yahoo account with Calendar data; and
- a Yahoo app password generated in Yahoo Account Security. Use the app password in Yahoo Month Print, not the account's normal password.

A configured Windows printer is needed for physical printing. Microsoft Print to PDF can be used without a physical printer.

## Install

1. Download `YahooMonthPrint-Setup.exe` from the project's release assets.
2. Optionally verify the download against `YahooMonthPrint-Setup.exe.sha256`.
3. Run the installer and follow its prompts.

The per-user installer does not require administrator privileges. It installs under `%LOCALAPPDATA%\Programs\YahooMonthPrint`, creates a Start Menu shortcut, and can optionally create a desktop shortcut.

Development builds are normally unsigned. Windows may therefore show an unknown-publisher or Microsoft Defender SmartScreen warning. Only continue when the installer came from a trusted project release and its checksum matches. Official builds should be Authenticode-signed when a release certificate is available.

Running a newer installer upgrades the existing installation in place. Yahoo Month Print appears in Windows Installed Apps and includes a conventional uninstaller. Uninstall removes the application's stored Yahoo credential, settings, cache, and logs, but it does not revoke the Yahoo app password or modify Yahoo Calendar data. Revoke an unused app password separately in Yahoo Account Security.

## Build the installer

Building is supported on Windows 10 or 11 x64. Install the .NET 8 SDK and Inno Setup 6.3 or newer:

```powershell
winget install --id Microsoft.DotNet.SDK.8 --exact
winget install --id JRSoftware.InnoSetup --exact --scope user
```

From the repository root, run the single build command with the release version:

```powershell
./eng/build-installer.ps1 -Version 1.0.0
```

The script restores locked dependencies, builds and tests the solution, publishes and verifies the self-contained Windows x64 application, compiles the installer, and writes a SHA-256 checksum. Its outputs are:

- `artifacts/installer/YahooMonthPrint-Setup.exe`
- `artifacts/installer/YahooMonthPrint-Setup.exe.sha256`

Pass `-SkipTests` only when iterating locally; release candidates should run the default complete workflow. See [Build and release](release.md) for optional Authenticode signing and release validation.

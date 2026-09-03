[Setup]
AppId={{12C74888-03E8-4A38-8177-EA74C892FE65}
AppName=Yahoo Month Print Toolchain Smoke
AppVersion=0.0.0
DefaultDirName={localappdata}\Programs\YahooMonthPrintToolchainSmoke
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
Uninstallable=no
CreateAppDir=no
OutputBaseFilename=YahooMonthPrint-Toolchain-Smoke
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Code]
procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel2.Caption :=
    'This artifact only validates the development installer compiler. It is not the Yahoo Month Print application installer.';
end;

#if VER < EncodeVer(6, 3, 0)
  #error Inno Setup 6.3 or newer is required by this script.
#endif

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif

[Setup]
AppId={{D09D281F-6DE6-4719-92AF-72B64D9FA5F8}
AppName=Yahoo Month Print
AppVersion={#AppVersion}
AppPublisher=Yahoo Month Print contributors
AppPublisherURL=https://github.com/RoboNater/print-my-calendar
AppSupportURL=https://github.com/RoboNater/print-my-calendar/issues
DefaultDirName={localappdata}\Programs\YahooMonthPrint
DefaultGroupName=Yahoo Month Print
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
OutputDir=..\artifacts\installer
OutputBaseFilename=YahooMonthPrint-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
UninstallDisplayIcon={app}\YahooMonthPrint.App.exe
VersionInfoVersion={#AppVersion}
VersionInfoProductName=Yahoo Month Print
VersionInfoProductVersion={#AppVersion}
VersionInfoCompany=Yahoo Month Print contributors

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Yahoo Month Print"; Filename: "{app}\YahooMonthPrint.App.exe"
Name: "{autodesktop}\Yahoo Month Print"; Filename: "{app}\YahooMonthPrint.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\YahooMonthPrint.App.exe"; Description: "Launch Yahoo Month Print"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\YahooMonthPrint.App.exe"; Parameters: "--uninstall-cleanup"; Flags: runhidden waituntilterminated; RunOnceId: "YahooMonthPrintLocalDataCleanup"

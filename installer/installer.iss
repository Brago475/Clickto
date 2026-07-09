; ============================================================
;  TCW Studio - Reusable Installer Script (Inno Setup)
;  Reuse for a future app: change the APP SETTINGS block below.
; ============================================================

; ---------------- APP SETTINGS ----------------
#define AppName        "Clickto"
#define AppVersion     "1.1.0"
#define AppPublisher   "TCW Studio"
#define AppURL         "http://thetcwstudio.com/"
#define AppExeName     "Clickto.exe"
; ----------------------------------------------

[Setup]
; Unique per-app ID. Keep SAME across versions of Clickto; make a NEW one per new app.
AppId={{B7C4E9A2-1D3F-4A6B-9C8E-2F5A7D1E0C3B}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableDirPage=no
LicenseFile=LICENSE.txt
OutputDir=..\installer-out
OutputBaseFilename={#AppName}-Setup-{#AppVersion}
SetupIconFile=..\Clickto\Assets\clickto.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
ArchitecturesInstallIn64BitMode=x64compatible
; Branding images
WizardImageFile=banner.bmp
WizardSmallImageFile=header.bmp

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "quicklaunchicon"; Description: "Create a Quick Launch shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; The self-contained exe, pulled from the Windows build output folder.
Source: "..\dist-win\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName} now"; Flags: nowait postinstall skipifsilent

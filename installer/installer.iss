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
#define AppTagline     "Record. Replay. Repeat."
; ----------------------------------------------

[Setup]
AppId={{B7C4E9A2-1D3F-4A6B-9C8E-2F5A7D1E0C3B}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
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
WizardImageFile=banner.bmp
WizardSmallImageFile=header.bmp
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
ArchitecturesInstallIn64BitMode=x64compatible
; Custom window title and branding text
AppComments={#AppTagline}
SetupMutex={#AppName}SetupMutex
WizardImageStretch=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; ---- Custom wording to make it feel unique ----
[Messages]
WelcomeLabel1=Welcome to {#AppName}
WelcomeLabel2=This will install {#AppName} {#AppVersion} on your computer.%n%n{#AppTagline}%n%nA free tool by {#AppPublisher}. Click Next to continue.
FinishedHeadingLabel=Clickto is ready
FinishedLabel=Clickto has been installed. Thanks for trying it — made with care by TCW Studio.
ClickNext=
BeveledLabel={#AppPublisher} · thetcwstudio.com

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "quicklaunchicon"; Description: "Create a Quick Launch shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\dist-win\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName} now"; Flags: nowait postinstall skipifsilent

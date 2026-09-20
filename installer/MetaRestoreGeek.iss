; MetaRestoreGeek installer
;
; Same shape as installer\CutGeek.iss and installer\VideoSizeGeek.iss. Two things here are
; decisions rather than defaults, and both are explained where they appear: PrivilegesRequired,
; and what the uninstaller does and does not remove.
;
; Build it locally with:  build.cmd installer
; CI builds it in .github\workflows\release.yml.

#define AppName        "MetaRestoreGeek"
#define AppSourceDir   "..\publish\app"
#define AppExeName     "MetaRestoreGeek.exe"
#define AppPublisher   "TechyGeeksHome"
#define AppURL         "https://techygeekshome.info/metarestoregeek/"
#define AppSupportURL  "https://github.com/techygeekshome/MetaRestoreGeek/issues"
#define AppUpdatesURL  "https://github.com/techygeekshome/MetaRestoreGeek/releases"
#define FirstYear      "2026"
#define CurrentYear    GetDateTimeString('yyyy', '', '')

; Read straight off the executable that is about to be packaged, so the installer can never
; claim a different version from the thing inside it.
#define AppVersion GetVersionNumbersString(AppSourceDir + "\" + AppExeName)

[Setup]
; NEVER regenerate this. Windows uses the AppId to tell an upgrade from a second parallel
; install; a new one means the next version installs alongside this one instead of over it.
; Freshly generated for this app; nothing has ever shipped under a different id.
AppId={{0CCC8C55-A282-47FD-9A19-C3000238C4FF}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppSupportURL}
AppUpdatesURL={#AppUpdatesURL}
AppCopyright=Copyright (C) {#FirstYear}-{#CurrentYear} {#AppPublisher}

VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup

WizardStyle=modern
UninstallDisplayName={#AppName} {#AppVersion}
UninstallDisplayIcon={app}\{#AppExeName}
LicenseFile=..\LICENSE
SetupIconFile=..\icons\metarestoregeek.ico

OutputDir=..\dist
OutputBaseFilename={#AppName}Setup

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
AllowNoIcons=yes

; The app's own manifest is asInvoker - it reads a Takeout export and writes restored copies of
; the photos beside it, and there is nothing in that which needs administrator rights. Installing
; it somewhere only an administrator can write would be pretending otherwise, so this is a
; per-user install with no UAC prompt. Anyone who wants it machine-wide can pass /ALLUSERS.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline dialog

Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
english.CreateDesktopShortcut=Create a &desktop shortcut
english.LaunchApp=Open {#AppName}
english.WebSite={#AppName} on the web

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopShortcut}"; GroupDescription: "Shortcuts:"

[Files]
Source: "{#AppSourceDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE";   DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; DestName: "README.md";   Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";                       Filename: "{app}\{#AppExeName}"
Name: "{group}\{cm:WebSite}";                     Filename: "{#AppURL}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";                 Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; The cached exiftool copy (once somebody has pointed MetaRestoreGeek at one) is left behind on
; purpose, same reasoning as CutGeek's models folder and VideoSizeGeek's ffmpeg cache: somebody
; who uninstalls to try a newer build should not have to find exiftool again. The error log goes
; with the rest of the folder.
Type: dirifempty; Name: "{localappdata}\TechyGeeksHome\MetaRestoreGeek\bin"
Type: files;      Name: "{localappdata}\TechyGeeksHome\MetaRestoreGeek\error.log"
Type: dirifempty; Name: "{localappdata}\TechyGeeksHome\MetaRestoreGeek"
Type: dirifempty; Name: "{localappdata}\TechyGeeksHome"

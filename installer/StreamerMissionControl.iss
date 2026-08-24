#define MyAppName "NerdSpace Labs - Streamer Mission Control"
#ifndef MyAppVersion
  #define MyAppVersion "0.8.0-alpha.5"
#endif
#define MyAppPublisher "NerdSpace Labs by OneEyedNerdy"
#define MyAppExeName "NerdSpace.StreamerMissionControl.exe"
#ifndef PublishDir
  #define PublishDir "..\publish\win-x64"
#endif

[Setup]
AppId={{D2786625-A2E4-4F54-8B52-1CE99A746CE2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Installer
VersionInfoProductName={#MyAppName}
AppComments=Self-contained .NET 10 Windows build; no separate .NET runtime or SDK installation required.
DefaultDirName={localappdata}\Programs\NerdSpace Labs\Streamer Mission Control
DefaultGroupName=NerdSpace Labs
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=NerdSpace-Streamer-Mission-Control-Setup-v{#MyAppVersion}
SetupIconFile=..\src\Nerdspace.OBSRecovery\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Streamer Mission Control"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\Streamer Mission Control"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Streamer Mission Control"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[InstallDelete]
Type: files; Name: "{app}\Nerdspace.OBSRecovery.exe"

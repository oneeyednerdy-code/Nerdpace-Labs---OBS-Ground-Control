#define MyAppName "Nerdspace Labs OBS Ground Control"
#ifndef MyAppVersion
  #define MyAppVersion "0.7.0-alpha.4"
#endif
#define MyAppPublisher "Nerdspace Labs by OneEyedNerdy"
#define MyAppExeName "Nerdspace.OBSRecovery.exe"
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
DefaultDirName={localappdata}\Programs\Nerdspace Labs\OBS Ground Control
DefaultGroupName=Nerdspace Labs
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=Nerdspace-OBS-Ground-Control-Setup-v{#MyAppVersion}
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
Name: "{group}\OBS Ground Control"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\OBS Ground Control"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch OBS Ground Control"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

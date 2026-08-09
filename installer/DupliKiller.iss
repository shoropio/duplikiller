; DupliKiller - Inno Setup script
; Build: ISCC.exe installer\DupliKiller.iss /DMyAppVersion=x.y.z
; Requires a self-contained single-file publish at publish\win-x64.

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName "DupliKiller"
#define MyAppPublisher "Shoropio Corporation"
#define MyAppExeName "DuplicateFinder.App.exe"
#define MyAppUrl "https://github.com/shoropio/duplikiller"

#define AppRoot SourcePath + "..\"
#define MyAppExeDir AppRoot + "publish\win-x64"
#define MyAppIcon AppRoot + "src\DuplicateFinder.App\Assets\app.ico"

[Setup]
AppId={{6d48fa07-eaad-42b2-be5e-80a9abb93aeb}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
AppUpdatesURL={#MyAppUrl}
DefaultDirName={autopf}\{#MyAppPublisher}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir={#AppRoot}publish
OutputBaseFilename=DupliKiller-{#MyAppVersion}-win-x64-setup
SetupIconFile={#MyAppIcon}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
CloseApplications=yes
MinVersion=10.0.17763

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
Source: "{#MyAppExeDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Ejecutar {#MyAppName}"; Flags: nowait postinstall skipifsilent

#define AppName "AzerothCore Manager"
#define AppVersion "0.9.0"
#define AppPublisher "thatslifex"
#define AppURL "https://github.com/thatslifex/AzerothCoreManager"
#define AppExeName "AzerothCoreManager.exe"

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
DefaultDirName={autopf}\AzerothCoreManager
DefaultGroupName={#AppName}
OutputBaseFilename=AzerothCoreManager-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
UninstallDisplayName={#AppName}
PrivilegesRequired=lowest
WizardStyle=modern
DisableWelcomePage=no
SetupIconFile=Resources\Assets\Logo\wotlk.ico

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Start AzerothCore Manager"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

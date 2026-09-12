; Script generated for Desktop Pet World
#define MyAppName "DesktopPetWorld"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "DesktopPetWorld"
#define MyAppURL "https://github.com/DuyHung-2206/DesktopPet"
#define MyAppExeName "DesktopPet.exe"

[Setup]
AppId={{E68A9F32-8491-44B2-BD19-866468DF9971}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\DesktopPetWorld
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=Installer
OutputBaseFilename=DesktopPetWorld-Setup
SetupIconFile=Assets\Icons\app.ico
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Tự động khởi động cùng Windows"; GroupDescription: "Khởi động:"; Flags: unchecked

[Files]
Source: "Publish\DesktopPetWorld\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "Publish\DesktopPetWorld\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\Icons\app.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\Icons\app.ico"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

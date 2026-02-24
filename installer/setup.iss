; Gamma Sutra — Inno Setup Script
; Compile with: ISCC.exe setup.iss
; Requires Inno Setup 6: https://jrsoftware.org/isinfo.php

#define AppName    "Gamma Sutra"
#define AppExe     "GammaSutra.exe"
#define AppPublisher "Sloan Reynolds"
#define AppVersion "1.0"
#define AppURL     ""
#define BuildDir   "..\publish"

[Setup]
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
DefaultDirName={localappdata}\GammaSutra
DefaultGroupName={#AppName}
OutputDir=.
OutputBaseFilename=GammaSutraSetup
SetupIconFile=..\Resources\icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#BuildDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";   Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}";   Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove the startup registry entry on uninstall
Filename: "{cmd}"; Parameters: "/c reg delete ""HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"" /v GammaSutra /f"; Flags: runhidden; RunOnceId: "RemoveStartup"

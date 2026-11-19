#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#define AppName "look"
#define AppPublisher "look"
#define AppExeName "look.exe"

[Setup]
AppId={{6D2AC4F4-5A1B-4DB5-9A6A-3F9A61A44C21}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\look
DefaultGroupName=look
OutputDir=..\artifacts
OutputBaseFilename=look-setup-{#AppVersion}-win-x64
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#AppExeName}
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
DisableProgramGroupPage=yes

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\artifacts\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: not IsWebView2Installed

[Icons]
Name: "{group}\look"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\look"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; StatusMsg: "Installing Microsoft Edge WebView2 Runtime…"; Flags: waituntilterminated; Check: not IsWebView2Installed
Filename: "{app}\{#AppExeName}"; Description: "Launch look"; Flags: nowait postinstall skipifsilent

[Code]
function IsWebView2Installed: Boolean;
var
  Version: string;
  RuntimeGuid: string;
begin
  RuntimeGuid := "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
  Result :=
    (RegQueryStringValue(HKLM32, "SOFTWARE\Microsoft\EdgeUpdate\Clients\" + RuntimeGuid, "pv", Version) and
      (Version <> "") and (Version <> "0.0.0.0")) or
    (RegQueryStringValue(HKCU, "Software\Microsoft\EdgeUpdate\Clients\" + RuntimeGuid, "pv", Version) and
      (Version <> "") and (Version <> "0.0.0.0"));
end;

; DEBATE AI · Windows-Installer (Inno Setup 6)
; Installiert pro Benutzer ohne Administratorrechte.
; Build: ISCC.exe /DAppVersion=0.2.0 DebateAI.iss  (aus diesem Ordner, nach Neu-bauen.cmd)

#ifndef AppVersion
  #define AppVersion "0.2.0"
#endif

[Setup]
AppId={{6E3B1F0A-5C1D-4C1B-9E55-DEBA7EA10001}
AppName=DEBATE AI
AppVersion={#AppVersion}
AppVerName=DEBATE AI {#AppVersion}
AppPublisher=Murat Lüdecke
DefaultDirName={localappdata}\Programs\DEBATE AI
DefaultGroupName=DEBATE AI
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=DEBATE-AI-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=DEBATE AI
VersionInfoVersion={#AppVersion}.0
; Code-Signierung: in der CI über SignTool konfigurieren, sobald ein Zertifikat vorliegt.

[Languages]
Name: "de"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktop-Verknüpfung erstellen"; GroupDescription: "Zusätzliche Verknüpfungen:"

[Files]
Source: "..\DEBATE-AI.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\DEBATE-AI.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\ANLEITUNG.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\docs\HARDWARE-TEST.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\DEBATE AI"; Filename: "{app}\DEBATE-AI.exe"
Name: "{group}\Anleitung"; Filename: "{app}\ANLEITUNG.md"
Name: "{autodesktop}\DEBATE AI"; Filename: "{app}\DEBATE-AI.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DEBATE-AI.exe"; Description: "DEBATE AI jetzt starten"; Flags: nowait postinstall skipifsilent

[Code]
// .NET Framework 4.8 ist ab Windows 10 1903 enthalten. Release-Schlüssel 528040 = 4.8.
function InitializeSetup(): Boolean;
var
  release: Cardinal;
begin
  Result := True;
  if not RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', release) or (release < 528040) then
  begin
    MsgBox('DEBATE AI benötigt .NET Framework 4.8. Bitte zuerst über Windows Update oder von microsoft.com installieren.', mbError, MB_OK);
    Result := False;
  end;
end;

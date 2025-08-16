; =========================
; SimTools Installer (.iss)
; Bundles ViGEm + HidHide (x64)
; Auto-detects the main EXE to launch/create shortcuts
; =========================

#define AppName        "SimTools"
#define AppPublisher   "Your Company"
#define AppVersion     "1.0.0"

; EDIT THESE TWO LINES to your actual folders:
#define BuildFolder    "C:\Users\sfbui\OneDrive\Bureaublad\Github Projecten\SimTool\SimTool\bin\Release"
#define DriversFolder  "C:\Users\sfbui\OneDrive\Bureaublad\Github Projecten\SimTool\Drivers"

[Setup]
AppId={{D84C7B1D-3F7E-4F91-9F8F-5C5F3E0C9ABC}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={pf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=.\Output
OutputBaseFilename=SimTools_Setup_{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
DisableWelcomePage=no
DisableDirPage=no
DisableProgramGroupPage=no
DisableReadyMemo=no
UninstallDisplayIcon={app}\{code:GetMainExe}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; --- Your app files ---
Source: "{#BuildFolder}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

; --- Drivers (your filenames from the screenshot) ---
Source: "{#DriversFolder}\ViGEmBusSetup_x64.msi"; DestDir: "{tmp}"; Flags: deleteafterinstall ignoreversion
Source: "{#DriversFolder}\HidHideMSI.msi";       DestDir: "{tmp}"; Flags: deleteafterinstall ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{code:GetMainExe}"
; Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{code:GetMainExe}"  ; optional desktop icon

[Run]
; --- Install ViGEm (skip if already present) ---
Filename: "msiexec.exe"; \
  Parameters: "/i ""{tmp}\ViGEmBusSetup_x64.msi"" /passive /norestart"; \
  Flags: runhidden waituntilterminated; \
  StatusMsg: "Installing ViGEm Bus driver..."; \
  Check: not IsServiceInstalled('ViGEmBus')

; --- Install HidHide (skip if already present) ---
Filename: "msiexec.exe"; \
  Parameters: "/i ""{tmp}\HidHideMSI.msi"" /passive /norestart"; \
  Flags: runhidden waituntilterminated; \
  StatusMsg: "Installing HidHide..."; \
  Check: not IsServiceInstalled('HidHide')

; --- Launch app after install ---
Filename: "{app}\{code:GetMainExe}"; Parameters: "--postinstall"; \
  Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
function IsServiceInstalled(const Name: string): Boolean;
var
  Key: string;
begin
  Key := 'SYSTEM\CurrentControlSet\Services\' + Name;
  Result := RegKeyExists(HKLM, Key);
end;

function GetMainExe(Param: string): string;
var
  Candidate: string;
  FindRec: TFindRec;
begin
  { 1) Try common names }
  Candidate := ExpandConstant('{app}\SimTools.exe');
  if FileExists(Candidate) then begin
    Result := 'SimTools.exe';
    Exit;
  end;

  Candidate := ExpandConstant('{app}\SimTool.exe');
  if FileExists(Candidate) then begin
    Result := 'SimTool.exe';
    Exit;
  end;

  Result := '';
  if FindFirst(ExpandConstant('{app}\*.exe'), FindRec) then
  begin
    try
      repeat
        if CompareText(FindRec.Name, 'unins000.exe') <> 0 then
        begin
          Result := FindRec.Name;
          break;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;

  { 3) If still empty, point to a name that will clearly fail rather than crash the installer }
  if Result = '' then
    Result := 'SimTools.exe';
end;

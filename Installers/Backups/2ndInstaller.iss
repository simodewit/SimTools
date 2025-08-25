; =========================
; SimTools Installer (.iss)
; Bundles vJoy + HidHide (silent)
; =========================

#define AppName        "SimTools"
#define AppPublisher   "Your Company"
#define AppVersion     "1.0.0"

; EDIT THESE TWO LINES:
#define BuildFolder "C:\Users\sfbui\OneDrive\Bureaublad\Github Projecten\SimTool\SimTool\bin\Release" 
#define DriversFolder "C:\Users\sfbui\OneDrive\Bureaublad\Github Projecten\SimTool\Drivers"

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

; --- Copy driver installers (any EXE/MSI matching *vjoy* or *hidhide*) to {tmp}, then delete ---
Source: "{#DriversFolder}\*vjoy*.exe";    DestDir: "{tmp}"; Flags: deleteafterinstall ignoreversion recursesubdirs; Check: DirExists(ExpandConstant('{#DriversFolder}'))
Source: "{#DriversFolder}\*hidhide*.msi"; DestDir: "{tmp}"; Flags: deleteafterinstall ignoreversion recursesubdirs; Check: DirExists(ExpandConstant('{#DriversFolder}'))

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{code:GetMainExe}"
; Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{code:GetMainExe}"  ; optional

[Run]
; --- Install vJoy silently if not present ---
Filename: "{code:GetVJoyRunner}"; \
  Parameters: "{code:GetVJoyParams}"; \
  Flags: runhidden waituntilterminated; \
  Check: NeedVJoy

; --- Install HidHide silently if not present ---
Filename: "{code:GetHidHideRunner}"; \
  Parameters: "{code:GetHidHideParams}"; \
  Flags: runhidden waituntilterminated; \
  Check: NeedHidHide

; --- Launch app (your app will do silent HidHide whitelisting on --postinstall) ---
Filename: "{app}\{code:GetMainExe}"; Parameters: "--postinstall"; \
  Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
var
  VJoyPath: string;
  VJoyIsMsi: Boolean;
  HidHidePath: string;
  HidHideIsMsi: Boolean;

function IsServiceInstalled(const Name: string): Boolean;
var Key: string;
begin
  Key := 'SYSTEM\CurrentControlSet\Services\' + Name;
  Result := RegKeyExists(HKLM, Key);
end;

function GetMainExe(Param: string): string;
var Candidate: string; FindRec: TFindRec;
begin
  Candidate := ExpandConstant('{app}\SimTools.exe');
  if FileExists(Candidate) then begin Result := 'SimTools.exe'; exit; end;

  Candidate := ExpandConstant('{app}\SimTool.exe');
  if FileExists(Candidate) then begin Result := 'SimTool.exe'; exit; end;

  Result := '';
  if FindFirst(ExpandConstant('{app}\*.exe'), FindRec) then
  begin
    try
      repeat
        if CompareText(FindRec.Name, 'unins000.exe') <> 0 then begin
          Result := FindRec.Name; break;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;

  if Result = '' then Result := 'SimTools.exe';
end;

function FindInstaller(const Token: string; var FilePath: string; var IsMsi: Boolean): Boolean;
var FindRec: TFindRec; Tmp: string;
begin
  Result := False; FilePath := ''; IsMsi := False;
  Tmp := ExpandConstant('{tmp}');
  if FindFirst(Tmp + '\*' + Token + '*', FindRec) then
  begin
    try
      repeat
        if (CompareText(ExtractFileExt(FindRec.Name), '.msi') = 0) or
           (CompareText(ExtractFileExt(FindRec.Name), '.exe') = 0) then
        begin
          FilePath := Tmp + '\' + FindRec.Name;
          IsMsi := CompareText(ExtractFileExt(FilePath), '.msi') = 0;
          Result := True; break;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function NeedVJoy(): Boolean;
begin
  Result := (not IsServiceInstalled('vjoy')) and FindInstaller('vjoy', VJoyPath, VJoyIsMsi);
end;

function GetVJoyRunner(Param: string): string;
begin
  if VJoyIsMsi then Result := 'msiexec.exe' else Result := VJoyPath;
end;

function GetVJoyParams(Param: string): string;
begin
  if VJoyIsMsi then
    Result := '/i "' + VJoyPath + '" /qn /norestart'
  else
    Result := '/quiet /norestart';
end;

function NeedHidHide(): Boolean;
begin
  Result := (not IsServiceInstalled('HidHide')) and FindInstaller('hidhide', HidHidePath, HidHideIsMsi);
end;

function GetHidHideRunner(Param: string): string;
begin
  if HidHideIsMsi then Result := 'msiexec.exe' else Result := HidHidePath;
end;

function GetHidHideParams(Param: string): string;
begin
  if HidHideIsMsi then
    Result := '/i "' + HidHidePath + '" /qn /norestart'
  else
    Result := '/quiet /norestart';
end;

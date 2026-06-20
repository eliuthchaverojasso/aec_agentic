#ifndef AppVersion
#define AppVersion "1.0.0-dev.1"
#endif
#ifndef BuildFlavor
#define BuildFlavor "unsigned"
#endif
#ifndef StageDir
#define StageDir "C:\Documents\Hyperghaps EMA\EMA-AI\artifacts\release\stage"
#endif
#ifndef OutputDir
#define OutputDir "C:\Documents\Hyperghaps EMA\EMA-AI\artifacts\release\installer"
#endif

[Setup]
AppId={{7A5AEF4D-6F2E-4C0A-8B7E-5E3C5E9A1111}}
AppName=EMA AI Professional Release
AppVersion={#AppVersion}
AppPublisher=EMA AI
DefaultDirName={localappdata}\EMA AI
DisableDirPage=yes
DisableProgramGroupPage=no
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=EMA_AI_Professional_Setup_{#AppVersion}-{#BuildFlavor}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=EMA AI Professional Release
CloseApplications=no

[Types]
Name: "pilotcore"; Description: "Pilot core bundle"
Name: "revitonly"; Description: "Revit add-in only"
Name: "pilotai"; Description: "Pilot core bundle with optional local AI"

[Components]
Name: "revit"; Description: "Revit Add-in"; Types: revitonly pilotcore pilotai
Name: "backend"; Description: "Backend API"; Types: pilotcore pilotai
Name: "database"; Description: "Local PostgreSQL database"; Types: pilotcore pilotai
Name: "frontend"; Description: "Frontend Dashboard"; Types: pilotcore pilotai
Name: "lifecycle"; Description: "Lifecycle tools"; Types: pilotcore pilotai
Name: "localai"; Description: "Optional Ask EMA AI / Ollama support"; Types: pilotai

[Dirs]
Name: "{app}"
Name: "{app}\backend"
Name: "{app}\frontend"
Name: "{app}\scripts"
Name: "{app}\docs"

[Files]
Source: "{#StageDir}\backend\*"; DestDir: "{app}\backend"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: backend
Source: "{#StageDir}\database\*"; DestDir: "{app}\database"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: database
Source: "{#StageDir}\frontend\*"; DestDir: "{app}\frontend"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: frontend
Source: "{#StageDir}\scripts\*"; DestDir: "{app}\scripts"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: lifecycle
Source: "{#StageDir}\docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: lifecycle
Source: "{#StageDir}\manifest.json"; DestDir: "{app}"; Flags: ignoreversion; Components: lifecycle
Source: "{#StageDir}\checksums.sha256"; DestDir: "{app}"; Flags: ignoreversion; Components: lifecycle
Source: "{#StageDir}\ema-ai.components.json"; DestDir: "{app}"; Flags: ignoreversion; Components: lifecycle
Source: "{#StageDir}\docker-compose.release.yml"; DestDir: "{app}"; Flags: ignoreversion; Components: backend database frontend lifecycle
Source: "{#StageDir}\revit\2022\EMAExtractor.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit
Source: "{#StageDir}\revit\2022\EMA AI\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022\EMA AI"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist; Components: revit
Source: "{#StageDir}\revit\2023\EMAExtractor.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit
Source: "{#StageDir}\revit\2023\EMA AI\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023\EMA AI"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist; Components: revit
Source: "{#StageDir}\revit\2024\EMAExtractor.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit
Source: "{#StageDir}\revit\2024\EMA AI\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\EMA AI"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist; Components: revit
Source: "{#StageDir}\revit\2025\EMAExtractor.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit
Source: "{#StageDir}\revit\2025\EMA AI\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\EMA AI"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist; Components: revit
Source: "{#StageDir}\revit\2026\EMAExtractor.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit
Source: "{#StageDir}\revit\2026\EMA AI\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\EMA AI"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist; Components: revit
Source: "{#StageDir}\revit\2027\EMAExtractor.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2027"; Flags: ignoreversion skipifsourcedoesntexist; Components: revit
Source: "{#StageDir}\revit\2027\EMA AI\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2027\EMA AI"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist; Components: revit

[Icons]
Name: "{autoprograms}\EMA AI\Start EMA AI"; Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\scripts\start-ema-ai.ps1"""; WorkingDir: "{app}"; Components: lifecycle
Name: "{autoprograms}\EMA AI\Health Check"; Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\scripts\health-ema-ai.ps1"""; WorkingDir: "{app}"; Components: lifecycle
Name: "{autoprograms}\EMA AI\Stop EMA AI"; Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\scripts\stop-ema-ai.ps1"""; WorkingDir: "{app}"; Components: lifecycle
Name: "{autoprograms}\EMA AI\Update EMA AI"; Filename: "powershell.exe"; Parameters: "-ExecutionPolicy Bypass -File ""{app}\scripts\update-ema-ai.ps1"""; WorkingDir: "{app}"; Components: lifecycle
Name: "{autoprograms}\EMA AI\Uninstall Data Preservation Notes"; Filename: "notepad.exe"; Parameters: """{app}\docs\release\KNOWN_LIMITATIONS.md"""; Components: lifecycle

[UninstallDelete]
Type: files; Name: "{app}\manifest.json"
Type: files; Name: "{app}\checksums.sha256"
Type: filesandordirs; Name: "{app}\revit"
Type: filesandordirs; Name: "{app}\backend"
Type: filesandordirs; Name: "{app}\frontend"
Type: filesandordirs; Name: "{app}\scripts"
Type: filesandordirs; Name: "{app}\docs"

[Code]
function IsRevitRunning(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('cmd.exe', '/C tasklist /FI "IMAGENAME eq Revit.exe" | find /I "Revit.exe"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function CheckDockerPresent(): Boolean;
begin
  Result := FileExists(ExpandConstant('{pf}\Docker\Docker\Docker Desktop.exe')) or FileExists(ExpandConstant('{pf32}\Docker\Docker\Docker Desktop.exe'));
end;

function CheckWebView2Present(): Boolean;
begin
  Result :=
    DirExists(ExpandConstant('{pf}\Microsoft\EdgeWebView\Application')) or
    DirExists(ExpandConstant('{pf32}\Microsoft\EdgeWebView\Application'));
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if IsRevitRunning() then
  begin
    MsgBox('Please close Autodesk Revit before installing EMA AI.', mbInformation, MB_OK);
    Result := False;
    exit;
  end;

  if not CheckWebView2Present() then
  begin
    MsgBox('WebView2 runtime was not detected. EMA AI will fall back to the browser for report rendering.', mbInformation, MB_OK);
  end;

  if not CheckDockerPresent() then
  begin
    MsgBox('Docker Desktop was not detected. Backend and database components require Docker and will not be usable until it is installed.', mbInformation, MB_OK);
  end;
end;

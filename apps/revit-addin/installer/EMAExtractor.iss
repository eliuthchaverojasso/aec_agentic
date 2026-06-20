#define AppVersion "1.0.0"
#define RevitYear "2024"
; Paths resolve relative to this script's directory (repo root = ..\..\..).
#define StageDir SourcePath + "..\..\..\dist\EMAExtractor_1.0.0"
#define OutputDir SourcePath + "..\..\..\dist"

[Setup]
AppId={{8C2B58D2-7F2A-4D73-9C50-111111111111}
AppName=EMA AI Extractor
AppVersion={#AppVersion}
AppPublisher=EMA AI
DefaultDirName={userappdata}\Autodesk\Revit\Addins\{#RevitYear}\EMA AI
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=EMA_AI_Extractor_Setup_{#AppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=EMA AI Extractor
CloseApplications=no

[Files]
Source: "{#StageDir}\EMAExtractor.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\{#RevitYear}"; Flags: ignoreversion
Source: "{#StageDir}\EMA AI\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\{#RevitYear}\EMA AI"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDir}\README_INSTALL.txt"; DestDir: "{userappdata}\Autodesk\Revit\Addins\{#RevitYear}\EMA AI"; Flags: ignoreversion
Source: "{#StageDir}\sample-configs\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\{#RevitYear}\EMA AI\sample-configs"; Flags: ignoreversion recursesubdirs createallsubdirs

[Run]
Filename: "{userappdata}\Autodesk\Revit\Addins\{#RevitYear}\EMA AI\README_INSTALL.txt"; Description: "Open installation guide"; Flags: postinstall shellexec skipifsilent unchecked

[UninstallDelete]
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\{#RevitYear}\EMAExtractor.addin"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\{#RevitYear}\EMA AI"

[Code]
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if Exec('cmd.exe', '/C tasklist /FI "IMAGENAME eq Revit.exe" | find /I "Revit.exe"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
  begin
    MsgBox('Please close Autodesk Revit before installing EMA AI Extractor.', mbInformation, MB_OK);
    Result := False;
  end;
end;

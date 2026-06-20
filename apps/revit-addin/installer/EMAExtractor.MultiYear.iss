#ifndef AppVersion
#define AppVersion "1.0.0"
#endif
#ifndef StageDir
#define StageDir "C:\Documents\Hyperghaps EMA\EMA-AI\artifacts\EMAExtractor\RevitAddinPackage"
#endif
#ifndef OutputDir
#define OutputDir "C:\Documents\Hyperghaps EMA\EMA-AI\artifacts\EMAExtractor\installer"
#endif

[Setup]
AppId={{8C2B58D2-7F2A-4D73-9C50-222720272027}
AppName=EMA AI Revit Add-in
AppVersion={#AppVersion}
AppPublisher=EMA AI
DefaultDirName={userappdata}\Autodesk\Revit\Addins
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=EMA_AI_Revit_Addin_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName=EMA AI Revit Add-in
CloseApplications=no

[Files]
Source: "{#StageDir}\manifests\2022\EMAExtractor.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022"; Flags: ignoreversion
Source: "{#StageDir}\payloads\2022\EMA AI\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022\EMA AI"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDir}\README_INSTALL.txt"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2022\EMA AI"; Flags: ignoreversion

Source: "{#StageDir}\manifests\2023\EMAExtractor.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023"; Flags: ignoreversion
Source: "{#StageDir}\payloads\2023\EMA AI\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023\EMA AI"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDir}\README_INSTALL.txt"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2023\EMA AI"; Flags: ignoreversion

Source: "{#StageDir}\manifests\2024\EMAExtractor.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024"; Flags: ignoreversion
Source: "{#StageDir}\payloads\2024\EMA AI\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\EMA AI"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDir}\README_INSTALL.txt"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2024\EMA AI"; Flags: ignoreversion

Source: "{#StageDir}\manifests\2025\EMAExtractor.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025"; Flags: ignoreversion
Source: "{#StageDir}\payloads\2025\EMA AI\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\EMA AI"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDir}\README_INSTALL.txt"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2025\EMA AI"; Flags: ignoreversion

Source: "{#StageDir}\manifests\2026\EMAExtractor.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026"; Flags: ignoreversion
Source: "{#StageDir}\payloads\2026\EMA AI\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\EMA AI"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDir}\README_INSTALL.txt"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2026\EMA AI"; Flags: ignoreversion

Source: "{#StageDir}\manifests\2027\EMAExtractor.addin"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2027"; Flags: ignoreversion
Source: "{#StageDir}\payloads\2027\EMA AI\*"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2027\EMA AI"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDir}\README_INSTALL.txt"; DestDir: "{userappdata}\Autodesk\Revit\Addins\2027\EMA AI"; Flags: ignoreversion

[UninstallDelete]
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2022\EMAExtractor.addin"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2022\EMA AI"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2023\EMAExtractor.addin"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2023\EMA AI"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2024\EMAExtractor.addin"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2024\EMA AI"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2025\EMAExtractor.addin"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2025\EMA AI"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2026\EMAExtractor.addin"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2026\EMA AI"
Type: files; Name: "{userappdata}\Autodesk\Revit\Addins\2027\EMAExtractor.addin"
Type: filesandordirs; Name: "{userappdata}\Autodesk\Revit\Addins\2027\EMA AI"

[Code]
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if Exec('cmd.exe', '/C tasklist /FI "IMAGENAME eq Revit.exe" | find /I "Revit.exe"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0) then
  begin
    MsgBox('Please close Autodesk Revit before installing EMA AI Revit Add-in.', mbInformation, MB_OK);
    Result := False;
  end;
end;

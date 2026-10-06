#define MyAppName "EduAnalytics"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "EduAnalytics"
#define MyAppExeName "EduAnalytics.UI.exe"
#define PublishDir "..\artifacts\publish\EduAnalytics"

[Setup]
AppId={{2C1CE99C-5165-4D74-99EC-1B83E5B87C21}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\EduAnalytics
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=EduAnalytics-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
SetupIconFile=..\EduAnalytics\src\EduAnalytics.UI\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Masaustu kisayolu olustur"; GroupDescription: "Kisayollar:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName} uygulamasini calistir"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  if not Exec(ExpandConstant('{cmd}'), '/C sqllocaldb info MSSQLLocalDB', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
  begin
    MsgBox('EduAnalytics veritabani icin SQL Server Express LocalDB kullanir. Bu bilgisayarda LocalDB bulunmazsa uygulama acilista veritabani hatasi verebilir.', mbInformation, MB_OK);
  end;
end;

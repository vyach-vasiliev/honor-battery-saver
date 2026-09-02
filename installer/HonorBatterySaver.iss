#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

#ifndef SourceRoot
  #define SourceRoot "..\artifacts\publish"
#endif

#define MyAppName "Honor Battery Saver"
#define MyAppPublisher "Honor Battery Saver"
#define MyAppExeName "Honor Battery Saver.exe"
#define MyServiceName "Honor Battery Saver"
#define MyServiceExeName "HonorBatterySaver.Service.exe"

[Setup]
AppId={{CCB54F7A-299A-4F9D-ABBF-131F382C38A3}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Honor Battery Saver
DefaultGroupName=Honor Battery Saver
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=..\artifacts\installer
OutputBaseFilename=HonorBatterySaverSetup
SetupIconFile=..\src\HonorBatterySaver.Tray\Assets\HonorBatterySaver.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible and not arm64
ArchitecturesInstallIn64BitMode=x64compatible and not arm64
MinVersion=10.0.22000
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousLanguage=yes
ChangesEnvironment=no
ChangesAssociations=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"; InfoBeforeFile: "disclaimer-en.txt"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"; InfoBeforeFile: "disclaimer-ru.txt"

[CustomMessages]
english.DotNetMissingMessage=Honor Battery Saver requires Microsoft .NET 10 Desktop Runtime x64.%n%nInstall the Desktop Runtime (not the basic .NET Runtime), then run Setup again.%n%nOpen the official Microsoft download page now?
english.WindowsClientRequired=Honor Battery Saver can be installed only on Windows 11 x64, build 22000 or newer.
english.ServiceInstallFailed=Setup could not register and start the Honor Battery Saver service. Error code: %1
english.ServiceStopFailed=The existing Honor Battery Saver service could not be stopped. Close Setup, stop the service, and try again.
english.ServiceStatus=Configuring the Honor Battery Saver service...
english.AutostartStatus=Enabling startup for the current user...
english.LaunchProgram=Launch Honor Battery Saver
english.DesktopIcon=Create a desktop shortcut
russian.DotNetMissingMessage=Для Honor Battery Saver требуется Microsoft .NET 10 Desktop Runtime x64.%n%nУстановите Desktop Runtime (не обычный .NET Runtime), затем снова запустите установщик.%n%nОткрыть официальную страницу загрузки Microsoft?
russian.WindowsClientRequired=Honor Battery Saver можно установить только на Windows 11 x64 сборки 22000 или новее.
russian.ServiceInstallFailed=Не удалось зарегистрировать и запустить службу Honor Battery Saver. Код ошибки: %1
russian.ServiceStopFailed=Не удалось остановить существующую службу Honor Battery Saver. Закройте установщик, остановите службу и повторите попытку.
russian.ServiceStatus=Настройка службы Honor Battery Saver...
russian.AutostartStatus=Настройка автозапуска для текущего пользователя...
russian.LaunchProgram=Запустить Honor Battery Saver
russian.DesktopIcon=Создать ярлык на рабочем столе

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceRoot}\Tray\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\Service\*"; DestDir: "{app}\Service"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\DISCLAIMER.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\DISCLAIMER.ru.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\PRIVACY.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Honor Battery Saver"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\Honor Battery Saver"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{sys}\reg.exe"; Parameters: "ADD ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v ""HonorBatterySaver"" /t REG_SZ /d """"{app}\{#MyAppExeName}"""" /f"; Flags: runhidden waituntilterminated runasoriginaluser; StatusMsg: "{cm:AutostartStatus}"
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallDelete]
Type: filesandordirs; Name: "{commonappdata}\HonorBatterySaver"

[Code]
const
  DotNetDownloadUrl = 'https://dotnet.microsoft.com/en-us/download/dotnet/10.0/runtime';
  DotNetRegistryKey = 'SOFTWARE\dotnet\Setup\InstalledVersions\x64';
  ServiceName = '{#MyServiceName}';
  ScManagerConnect = $0001;
  ServiceQueryStatus = $0004;
  ServiceStop = $0020;
  ServiceDelete = $00010000;
  ServiceControlStop = $00000001;
  ServiceStopped = $00000001;
  ServiceStopPending = $00000003;
  FileAttributeDirectory = $00000010;

type
  TServiceStatus = record
    ServiceType: LongWord;
    CurrentState: LongWord;
    ControlsAccepted: LongWord;
    Win32ExitCode: LongWord;
    ServiceSpecificExitCode: LongWord;
    CheckPoint: LongWord;
    WaitHint: LongWord;
  end;

function OpenSCManager(MachineName: string; DatabaseName: string;
  DesiredAccess: LongWord): THandle;
  external 'OpenSCManagerW@advapi32.dll stdcall';
function OpenService(ServiceManager: THandle; ServiceName: string;
  DesiredAccess: LongWord): THandle;
  external 'OpenServiceW@advapi32.dll stdcall';
function CloseServiceHandle(ServiceHandle: THandle): Boolean;
  external 'CloseServiceHandle@advapi32.dll stdcall';
function QueryServiceStatus(ServiceHandle: THandle;
  var ServiceStatus: TServiceStatus): Boolean;
  external 'QueryServiceStatus@advapi32.dll stdcall';
function ControlService(ServiceHandle: THandle; Control: LongWord;
  var ServiceStatus: TServiceStatus): Boolean;
  external 'ControlService@advapi32.dll stdcall';
function DeleteService(ServiceHandle: THandle): Boolean;
  external 'DeleteService@advapi32.dll stdcall';

function HasDesktopRuntimeAt(const DotNetRoot: string): Boolean;
var
  FindRec: TFindRec;
  RuntimeDirectory: string;
begin
  Result := False;
  if DotNetRoot = '' then
    Exit;

  RuntimeDirectory := AddBackslash(DotNetRoot) +
    'shared\Microsoft.WindowsDesktop.App';
  if not DirExists(RuntimeDirectory) then
    Exit;

  if FindFirst(AddBackslash(RuntimeDirectory) + '10.*', FindRec) then
  begin
    try
      repeat
        if ((FindRec.Attributes and FileAttributeDirectory) <> 0) and
           (CompareText(Copy(FindRec.Name, 1, 3), '10.') = 0) then
        begin
          Result := True;
          Exit;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function IsDesktopRuntimeInstalled: Boolean;
var
  DotNetRoot: string;
begin
  DotNetRoot := GetEnv('DOTNET_ROOT_X64');
  if HasDesktopRuntimeAt(DotNetRoot) then
  begin
    Result := True;
    Exit;
  end;

  DotNetRoot := '';
  RegQueryStringValue(HKLM64, DotNetRegistryKey, 'InstallLocation', DotNetRoot);
  if HasDesktopRuntimeAt(DotNetRoot) then
  begin
    Result := True;
    Exit;
  end;

  Result := HasDesktopRuntimeAt(ExpandConstant('{pf64}\dotnet'));
end;

function InitializeSetup: Boolean;
var
  DownloadUrl: string;
  ErrorCode: Integer;
  WindowsVersion: TWindowsVersion;
begin
  GetWindowsVersionEx(WindowsVersion);
  if WindowsVersion.ProductType <> VER_NT_WORKSTATION then
  begin
    SuppressibleMsgBox(CustomMessage('WindowsClientRequired'),
      mbCriticalError, MB_OK, IDOK);
    Result := False;
    Exit;
  end;

  Result := IsDesktopRuntimeInstalled;
  if Result then
    Exit;

  if SuppressibleMsgBox(CustomMessage('DotNetMissingMessage'),
       mbConfirmation, MB_YESNO, IDYES) = IDYES then
  begin
    DownloadUrl := DotNetDownloadUrl;
    if ActiveLanguage = 'russian' then
      DownloadUrl := 'https://dotnet.microsoft.com/ru-ru/download/dotnet/10.0/runtime';
    ShellExec('open', DownloadUrl, '', '', SW_SHOWNORMAL,
      ewNoWait, ErrorCode);
  end;
end;

function OpenInstalledService(const DesiredAccess: LongWord): THandle;
var
  ServiceManager: THandle;
begin
  Result := 0;
  ServiceManager := OpenSCManager('', '', ScManagerConnect);
  if ServiceManager = 0 then
    Exit;

  try
    Result := OpenService(ServiceManager, ServiceName, DesiredAccess);
  finally
    CloseServiceHandle(ServiceManager);
  end;
end;

function ServiceExists: Boolean;
var
  ServiceHandle: THandle;
begin
  ServiceHandle := OpenInstalledService(ServiceQueryStatus);
  Result := ServiceHandle <> 0;
  if Result then
    CloseServiceHandle(ServiceHandle);
end;

function StopInstalledService: Boolean;
var
  ServiceHandle: THandle;
  ServiceStatus: TServiceStatus;
  Attempts: Integer;
begin
  Result := True;
  ServiceHandle := OpenInstalledService(ServiceQueryStatus or ServiceStop);
  if ServiceHandle = 0 then
    Exit;

  try
    if not QueryServiceStatus(ServiceHandle, ServiceStatus) then
    begin
      Result := False;
      Exit;
    end;

    if ServiceStatus.CurrentState = ServiceStopped then
      Exit;

    if ServiceStatus.CurrentState <> ServiceStopPending then
      ControlService(ServiceHandle, ServiceControlStop, ServiceStatus);

    for Attempts := 1 to 60 do
    begin
      Sleep(250);
      if not QueryServiceStatus(ServiceHandle, ServiceStatus) then
      begin
        Result := False;
        Exit;
      end;
      if ServiceStatus.CurrentState = ServiceStopped then
        Exit;
    end;

    Result := False;
  finally
    CloseServiceHandle(ServiceHandle);
  end;
end;

function RunServiceCommand(const Parameters: string; var ErrorCode: Integer): Boolean;
begin
  Result := Exec(ExpandConstant('{sys}\sc.exe'), Parameters, '', SW_HIDE,
    ewWaitUntilTerminated, ErrorCode) and (ErrorCode = 0);
end;

function ConfigureAndStartService(var ErrorCode: Integer): Boolean;
var
  ServiceExecutable: string;
  ServiceBinaryPathArgument: string;
begin
  WizardForm.StatusLabel.Caption := CustomMessage('ServiceStatus');
  ServiceExecutable := ExpandConstant('{app}\Service\{#MyServiceExeName}');
  ServiceBinaryPathArgument := '"\"' + ServiceExecutable + '\""';

  if not ServiceExists then
  begin
    Result := RunServiceCommand(
      'create "' + ServiceName + '" binPath= ' + ServiceBinaryPathArgument +
      ' start= delayed-auto obj= LocalSystem DisplayName= "' +
      ServiceName + '"', ErrorCode);
    if not Result then
      Exit;
  end;

  Result := RunServiceCommand(
    'config "' + ServiceName + '" binPath= ' + ServiceBinaryPathArgument +
    ' start= delayed-auto obj= LocalSystem DisplayName= "' +
    ServiceName + '"', ErrorCode);
  if not Result then
    Exit;

  Result := RunServiceCommand(
    'description "' + ServiceName +
    '" "Applies validated HONOR battery protection profiles on local requests."',
    ErrorCode);
  if not Result then
    Exit;

  Result := RunServiceCommand(
    'failure "' + ServiceName +
    '" reset= 86400 actions= restart/5000/restart/15000', ErrorCode);
  if not Result then
    Exit;

  Result := RunServiceCommand(
    'failureflag "' + ServiceName + '" 1', ErrorCode);
  if not Result then
    Exit;

  Result := RunServiceCommand('start "' + ServiceName + '"', ErrorCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): string;
begin
  Result := '';
  if not StopInstalledService then
    Result := CustomMessage('ServiceStopFailed');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ErrorCode: Integer;
begin
  if CurStep <> ssPostInstall then
    Exit;

  ErrorCode := 0;
  if not ConfigureAndStartService(ErrorCode) then
  begin
    MsgBox(Format(CustomMessage('ServiceInstallFailed'), [IntToStr(ErrorCode)]),
      mbError, MB_OK);
    Abort;
  end;
end;

procedure RemoveInstalledService;
var
  ServiceHandle: THandle;
begin
  StopInstalledService;
  ServiceHandle := OpenInstalledService(ServiceDelete);
  if ServiceHandle = 0 then
    Exit;

  try
    DeleteService(ServiceHandle);
  finally
    CloseServiceHandle(ServiceHandle);
  end;
end;

procedure RemoveInstalledAutostartEntries;
var
  ExpectedValue: string;
  Index: Integer;
  RunKey: string;
  UserSids: TArrayOfString;
  Value: string;
begin
  ExpectedValue := AddQuotes(ExpandConstant('{app}\{#MyAppExeName}'));
  if not RegGetSubkeyNames(HKU, '', UserSids) then
    Exit;

  for Index := 0 to GetArrayLength(UserSids) - 1 do
  begin
    RunKey := UserSids[Index] +
      '\Software\Microsoft\Windows\CurrentVersion\Run';
    Value := '';
    if RegQueryStringValue(HKU, RunKey, 'HonorBatterySaver', Value) and
       (CompareText(Value, ExpectedValue) = 0) then
      RegDeleteValue(HKU, RunKey, 'HonorBatterySaver');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RemoveInstalledService;
    RemoveInstalledAutostartEntries;
  end;
end;

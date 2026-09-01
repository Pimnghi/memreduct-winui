#ifndef AppVersion
  #error AppVersion must be supplied by build-installer.ps1.
#endif
#ifndef Architecture
  #error Architecture must be supplied by build-installer.ps1.
#endif
#ifndef SourceDir
  #error SourceDir must be supplied by build-installer.ps1.
#endif
#ifndef OutputDir
  #error OutputDir must be supplied by build-installer.ps1.
#endif

#define AppName "Mem Reduct WinUI"
#define AppPublisher "Pimnghi"
#define AppExeName "memreduct-winui.exe"
#define AppIdValue "{{9E6B7385-4F71-4F87-823E-3FDAD05D34DD}"

#if Architecture == "x64"
  #define ArchitectureAllowed "x64compatible and not arm64"
  #define ArchitectureInstallMode "x64compatible and not arm64"
  #define OutputArchitecture "win-x64"
#elif Architecture == "ARM64"
  #define ArchitectureAllowed "arm64"
  #define ArchitectureInstallMode "arm64"
  #define OutputArchitecture "win-arm64"
#else
  #error Unsupported installer architecture.
#endif

[Setup]
AppId={#AppIdValue}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/Pimnghi
AppSupportURL=https://github.com/Pimnghi/memreduct-winui/issues
AppUpdatesURL=https://github.com/Pimnghi/memreduct-winui/releases
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Setup
VersionInfoProductName={#AppName}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
LicenseFile={#SourceDir}\LICENSE
SetupIconFile={#SourceDir}\Assets\AppIcon.ico
UninstallDisplayIcon={app}\Assets\AppIcon.ico
OutputDir={#OutputDir}
OutputBaseFilename=MemReductWinUI-{#AppVersion}-{#OutputArchitecture}-setup
ArchitecturesAllowed={#ArchitectureAllowed}
ArchitecturesInstallIn64BitMode={#ArchitectureInstallMode}
MinVersion=10.0.17763
PrivilegesRequired=admin
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes
Uninstallable=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "chinesetraditional"; MessagesFile: "compiler:Languages\ChineseTraditional.isl"

[CustomMessages]
english.DesktopIcon=Create a &desktop shortcut
english.LaunchProgram=Launch Mem Reduct WinUI
english.DeleteUserDataPrompt=Delete all Mem Reduct WinUI settings and logs?
chinesesimplified.DesktopIcon=创建桌面快捷方式(&D)
chinesesimplified.LaunchProgram=运行 Mem Reduct WinUI
chinesesimplified.DeleteUserDataPrompt=是否删除 Mem Reduct WinUI 的全部设置和日志？
chinesetraditional.DesktopIcon=建立桌面捷徑(&D)
chinesetraditional.LaunchProgram=執行 Mem Reduct WinUI
chinesetraditional.DeleteUserDataPrompt=是否刪除 Mem Reduct WinUI 的全部設定與記錄？

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
Name: "{commonappdata}\Mem Reduct WinUI\data"

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DeleteUserData: Boolean;

procedure StopRunningApplications;
var
  ResultCode: Integer;
begin
  Exec(
    ExpandConstant('{sys}\taskkill.exe'),
    '/F /IM "memreduct-winui.exe"',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);
  Exec(
    ExpandConstant('{sys}\taskkill.exe'),
    '/F /IM "mrw-cli.exe"',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);
end;

procedure RemoveSystemIntegration;
var
  ResultCode: Integer;
begin
  Exec(
    ExpandConstant('{sys}\schtasks.exe'),
    '/Delete /TN "MemReductWinUI" /F',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);
  RegDeleteKeyIncludingSubkeys(
    HKCU,
    'Software\Classes\AppUserModelId\Pimnghi.MemReductWinUI');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApplications;
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    if UninstallSilent then
      DeleteUserData := False
    else
      DeleteUserData :=
        MsgBox(
          ExpandConstant('{cm:DeleteUserDataPrompt}'),
          mbConfirmation,
          MB_YESNO or MB_DEFBUTTON2) = IDYES;
    StopRunningApplications;
    RemoveSystemIntegration;
  end
  else if (CurUninstallStep = usPostUninstall) and DeleteUserData then
  begin
    DelTree(
      ExpandConstant('{commonappdata}\Mem Reduct WinUI'),
      True,
      True,
      True);
  end;
end;

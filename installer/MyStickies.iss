; My Stickies 설치 스크립트 (Inno Setup 6)
; build-installer.ps1이 게시 결과(installer\publish\MyStickies.exe)를 넣고 이 스크립트를 컴파일함
; 사용자별 설치 (관리자 권한 불필요): %LocalAppData%\Programs\MyStickies

#ifndef AppVersion
  #define AppVersion "1.0.7"
#endif
#ifndef PublishDir
  #define PublishDir "publish"
#endif

#define AppName "My Stickies"
#define AppExe "MyStickies.exe"
#define AppPublisher "drasys"
#define AppUrl "https://github.com/workingdad365/my_stickies"

[Setup]
AppId={{7D3E4C2A-5B1F-4E8A-9C6D-2F8B1A0E5C31}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
VersionInfoVersion={#AppVersion}
; 앱의 자동 실행 등록 로직이 기대하는 설치 폴더와 동일
DefaultDirName={localappdata}\Programs\MyStickies
DisableDirPage=yes
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=MyStickies-Setup-{#AppVersion}
SetupIconFile=..\src\MyStickies\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; 실행 중인 앱은 설치 전에 자동 종료 (다시 시작 관리자 사용)
CloseApplications=yes
RestartApplications=no
ShowLanguageDialog=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
korean.LaunchAfterInstall=설치 후 {#AppName} 실행
korean.CreateDesktopIcon=바탕 화면 바로 가기 만들기
korean.AutoStart=Windows 로그인 시 자동 실행
korean.KeepDataNote=메모 데이터와 설정은 제거해도 남아 있습니다.
english.LaunchAfterInstall=Launch {#AppName} after install
english.CreateDesktopIcon=Create a desktop shortcut
english.AutoStart=Start automatically at Windows sign-in
english.KeepDataNote=Your notes and settings are kept after uninstall.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked
Name: "autostart"; Description: "{cm:AutoStart}"

[Files]
Source: "{#PublishDir}\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; 앱 설정 창의 자동 실행과 같은 키/값을 사용하므로 서로 호환됨
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MyStickies"; ValueData: """{app}\{#AppExe}"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchAfterInstall}"; Flags: nowait postinstall skipifsilent
; 앱 내 자동 업데이트(/SILENT /RELAUNCH=1)로 설치한 경우 앱을 바로 다시 실행
Filename: "{app}\{#AppExe}"; Flags: nowait; Check: IsRelaunchRequested

[UninstallRun]
; 제거 전 실행 중인 앱 종료
Filename: "{sys}\taskkill.exe"; Parameters: "/IM {#AppExe} /F"; Flags: runhidden; RunOnceId: "KillApp"

[Code]
// 앱 내 자동 업데이트가 넘기는 /RELAUNCH=1 파라미터 여부
function IsRelaunchRequested: Boolean;
begin
  Result := ExpandConstant('{param:RELAUNCH|0}') = '1';
end;

// 제거 시 앱이 직접 등록한 자동 실행 값도 정리 (설치 작업으로 만든 값은 uninsdeletevalue가 처리)
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'MyStickies');
end;

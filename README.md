# My Stickies

버전 1.0.0

Windows 화면 오른쪽 가장자리에 붙어 있는 스티커 메모 앱. macOS 앱 [Hold My Notes](https://holdmynotes.app/)의 조작감을 Windows에서 재현한 것.

평소에는 얇은 책갈피 모양으로 숨어 있다가, 마우스를 가져가면 메모들이 펼쳐지고, 메모 위에 올리면 내용이 보이며, 클릭하면 바로 편집됨.

## 주요 기능

- 우측 가장자리 도킹: 휴면(책갈피) -> 팬아웃(탭 목록) -> 확장(본문) 3단계 애니메이션
- 클릭 편집: 제목칸과 본문칸 개별 편집, 색상 변경, 본문 길이에 따라 카드 높이 자동 조절
- 숨김(보관)과 두 단계 삭제, 드래그로 순서 변경
- 메모 관리 창: 전체/활성/숨김 필터, 검색, 미리보기, 복원, 삭제, 내보내기(Markdown/텍스트), 가져오기
- SQLite 파일 저장, 저장 폴더 지정 (Synology Drive 등 동기화 폴더에 두고 여러 PC에서 공유 가능)
- 트레이 아이콘, 전역 단축키, 전체화면 앱 실행 중 자동 숨김, Windows 로그인 시 자동 실행
- 도킹 모니터 선택, 덱 표시 개수/위치/접힘 지연, 글꼴과 크기 설정
- 4K 고DPI 환경(PerMonitorV2) 대응

## 요구 사항

- Windows 10/11 (x64)
- 개발: .NET 9 SDK
- 실행: 게시된 단일 exe는 런타임을 포함하므로 별도 설치 불필요

## 빌드와 실행

```powershell
cd G:\project\my_stickies

# 빌드 (Release)
dotnet build MyStickies.sln -c Release

# 테스트
dotnet test tests\MyStickies.Tests\MyStickies.Tests.csproj -c Release

# 개발 중 실행
dotnet run --project src\MyStickies -c Release
```

검증용 시작 인자

| 인자 | 동작 |
|---|---|
| `--all-notes` | 시작하자마자 메모 관리 창을 엶 |
| `--settings` | 시작하자마자 설정 창을 엶 |

## 게시 (배포용 exe)

```powershell
pwsh .\publish.ps1          # 게시만
pwsh .\publish.ps1 -Run     # 게시 후 실행
pwsh .\publish.ps1 -Output D:\Apps\MyStickies   # 다른 폴더로 게시
```

- Release, win-x64, .NET 런타임 포함 단일 exe로 게시
- 기본 출력 위치: `%LocalAppData%\Programs\MyStickies\MyStickies.exe`
- 실행 중인 인스턴스가 있으면 자동으로 종료한 뒤 게시
- 자동 실행 등록은 게시된 exe에서 하는 것을 권장 (빌드 출력 폴더의 exe를 등록하면 정리 시 깨질 수 있음)

## 설치 파일 만들기 (Inno Setup)

```powershell
winget install JRSoftware.InnoSetup      # 최초 1회
pwsh .\build-installer.ps1                # dist\MyStickies-Setup-<버전>.exe 생성
pwsh .\build-installer.ps1 -Install       # 생성 후 조용히 설치 (검증용)
```

- 사용자별 설치(관리자 권한 불필요), 설치 폴더는 `%LocalAppData%\Programs\MyStickies`
- 시작 메뉴 바로 가기, 선택 항목으로 바탕 화면 바로 가기와 로그인 시 자동 실행
- 실행 중인 앱은 설치 전에 자동 종료
- 제거해도 메모 데이터와 설정(`%LocalAppData%\MyStickies`, 지정한 저장 폴더)은 남음
- 코드 서명이 없으므로 다른 PC에서는 SmartScreen 경고가 뜰 수 있음 ("추가 정보 > 실행")

## 사용법

### 덱 조작

| 동작 | 결과 |
|---|---|
| 책갈피에 마우스 올림 | 메모 탭이 펼쳐짐 |
| 탭에 마우스 올림 | 카드가 확장되어 제목과 본문 표시 |
| 카드 클릭 | 편집 모드 (제목 클릭 시 제목칸, 그 외 본문칸) |
| 카드를 위아래로 드래그 | 순서 변경 |
| 마우스를 치움 | 지연 시간 후 접힘 |
| 책갈피 또는 카드 우클릭 | 메모 관리, 설정, 종료 메뉴 |

### 카드 버튼

| 버튼 | 동작 |
|---|---|
| `+` (덱 아래) | 새 메모 추가 |
| `−` (카드 우측 상단) | 숨김. 덱에서 빠지고 메모 관리 창의 "숨김"에서 복원 가능 |
| `×` (카드 우측 상단) | 삭제. 3초 안에 한 번 더 눌러야 실제 삭제 |
| 색상 점 (편집 중 하단) | 카드 색 변경 |

### 편집 단축키

| 키 | 동작 |
|---|---|
| Enter (제목칸) | 본문칸으로 이동 |
| Ctrl+Enter, Ctrl+S | 저장하고 편집 종료 |
| Esc | 변경 취소 |
| 바깥 클릭, 다른 카드 클릭 | 저장하고 편집 종료 |

제목을 비우면 `새 메모 (yyyy-MM-dd HH:mm)` 형식의 기본 제목이 붙음.

### 전역 단축키

| 키 | 동작 |
|---|---|
| Ctrl+Alt+S | 덱 열기/닫기 |
| Ctrl+Alt+N | 새 메모를 만들고 바로 편집 |

다른 프로그램과 겹치면 설정에서 끌 수 있음.

### 메모 관리 창

트레이 아이콘 우클릭 또는 덱 우클릭 메뉴의 "메모 관리"로 엶.

- 전체 / 활성 / 숨김 필터와 제목·본문 검색
- 선택한 메모의 숨김/복원, 삭제(확인 대화상자), 내보내기
- 상단의 가져오기(.md/.txt 여러 파일), 전체 내보내기(Markdown 한 파일), 새 메모

## 데이터 위치와 동기화

- 메모는 SQLite 파일 `my_stickies.db` 하나에 저장됨
- 최초 실행 때 저장 폴더를 물어봄. 기본값은 `%LocalAppData%\MyStickies`
- 설정에서 언제든 폴더를 바꿀 수 있음. 새 폴더에 파일이 있으면 그대로 읽고, 없으면 현재 메모를 복사하거나 안내 메모만 든 새 파일로 시작
- 폴더 위치 등 PC별 설정은 `%LocalAppData%\MyStickies\settings.json`에 따로 저장됨
- 동기화 폴더(Synology Drive, OneDrive 등)를 지정하면 여러 PC에서 같은 메모를 사용 가능. 다른 PC에서 파일이 바뀌면 자동으로 다시 읽음
- 단일 사용자 전제. 두 PC에서 동시에 편집하면 나중에 동기화된 쪽이 남음

DB 스키마는 `PRAGMA user_version`으로 관리되며 구버전 파일은 시작 시 자동 마이그레이션됨. 예전 파일 이름 `notes.db`도 자동으로 `my_stickies.db`로 바뀜.

## 설정 항목

트레이 아이콘 우클릭 > 설정

| 항목 | 내용 |
|---|---|
| 메모 저장 폴더 | DB 파일 위치 |
| 덱을 붙일 모니터 | 다중 모니터 중 선택. 모니터별 DPI 반영 |
| 표시 개수 | 덱에 보일 최근 메모 수 (3~8) |
| 시작 위치 | 화면 위에서 몇 % 지점부터 덱이 시작할지 |
| 접힘 지연 | 마우스가 벗어난 뒤 접히기까지 시간 |
| 전체화면 앱 숨김 | 같은 모니터에서 전체화면 앱이 앞에 있으면 덱 숨김 |
| 전역 단축키 | Ctrl+Alt+S, Ctrl+Alt+N 사용 여부 |
| 글꼴 / 본문 크기 | 시스템 글꼴 중 선택, 10~24pt |
| Windows 로그인 시 자동 실행 | HKCU Run 키 등록 |

## 프로젝트 구조

```
my_stickies/
  MyStickies.sln
  publish.ps1              게시 스크립트
  build-installer.ps1      설치 파일 생성 스크립트
  installer/MyStickies.iss Inno Setup 설치 스크립트
  PLAN.md                  개발 계획과 진행 상황
  src/MyStickies/
    App.xaml(.cs)          앱 리소스(글꼴/크기), 단일 인스턴스
    MainWindow.xaml(.cs)   도킹 창, 덱 상태 전환, 드래그, 설정 적용
    Controls/NoteTab       카드 한 장: 애니메이션, 편집, 색상, 버튼
    Windows/               메모 관리 창, 설정 창
    Data/                  SQLite 저장소, NoteStore, 설정, 내보내기, 자동 실행
    Layout/                덱 배치 계산, 모니터 정보, 글꼴 설정
    Interop/               Win32: 도구 창, DPI, 전체화면 감지, 전역 단축키
    Tray/                  트레이 아이콘
    Models/                Note, 팔레트, 안내 메모, 상대 시각
    Assets/app.ico         앱 아이콘
  tests/MyStickies.Tests/  xunit 단위 테스트
```

## 기술

- C# / WPF / .NET 9, Microsoft.Data.Sqlite
- 트레이 아이콘만 WinForms NotifyIcon 사용
- 투명 창의 알파 0 영역은 클릭이 아래 창으로 통과되며, 팬아웃 중에는 보이지 않는 호버 영역으로 마우스를 붙잡아 둠

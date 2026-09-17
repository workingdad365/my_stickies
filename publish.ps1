# MyStickies 게시 스크립트
# Release 구성, win-x64, .NET 런타임 포함 단일 exe로 게시
# 기본 출력 위치: %LocalAppData%\Programs\MyStickies (자동 실행 등록이 가리키는 설치 폴더)
# 사용: pwsh ./publish.ps1 [-Output <폴더>] [-Run]

param(
    [string]$Output = (Join-Path $env:LOCALAPPDATA "Programs\MyStickies"),
    [switch]$Run
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "src\MyStickies\MyStickies.csproj"

# 실행 중인 인스턴스가 exe를 잠그고 있으면 복사가 실패하므로 먼저 종료
$running = Get-Process MyStickies -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "실행 중인 MyStickies 종료 중..."
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 800
}

Write-Host "게시 중: $Output"
dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -o $Output

if ($LASTEXITCODE -ne 0) { throw "dotnet publish 실패 (exit $LASTEXITCODE)" }

$exe = Join-Path $Output "MyStickies.exe"
Write-Host "완료: $exe"

if ($Run) {
    Start-Process $exe
}

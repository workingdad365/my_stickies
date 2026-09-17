# MyStickies 설치 파일 생성 스크립트
# 1) Release 단일 exe로 installer\publish 에 게시
# 2) Inno Setup(ISCC)으로 dist\MyStickies-Setup-<버전>.exe 생성
# 사용: pwsh ./build-installer.ps1 [-Install]
#   -Install: 생성 직후 조용히 설치 (검증용)

param(
    [switch]$Install
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# 버전은 프로젝트 파일의 <Version>에서 읽음
$csproj = Join-Path $root "src\MyStickies\MyStickies.csproj"
$version = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "csproj에서 Version을 찾지 못함" }

# ISCC 위치: 사용자별 설치 또는 시스템 설치
$isccCandidates = @(
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6이 없습니다. 설치: winget install JRSoftware.InnoSetup" }

$publishDir = Join-Path $root "installer\publish"
Write-Host "1/2 게시: $publishDir (v$version)"
& (Join-Path $root "publish.ps1") -Output $publishDir

Write-Host "2/2 설치 파일 생성"
& $iscc "/DAppVersion=$version" "/DPublishDir=$publishDir" (Join-Path $root "installer\MyStickies.iss")
if ($LASTEXITCODE -ne 0) { throw "ISCC 실패 (exit $LASTEXITCODE)" }

$setup = Join-Path $root "dist\MyStickies-Setup-$version.exe"
Write-Host "완료: $setup"

if ($Install) {
    Write-Host "조용히 설치 중..."
    $p = Start-Process $setup -ArgumentList "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS" -Wait -PassThru
    Write-Host "설치 종료 코드: $($p.ExitCode)"
}

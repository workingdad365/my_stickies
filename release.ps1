# MyStickies 설치 파일을 GitHub Release에 게시하는 스크립트
# 커밋 및 푸시 후 build-installer.ps1로 설치 파일을 생성한 다음 실행함
# 사용 예: .\release.ps1 1.0.8 "한국어·영어 지원"

[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+$')]
    [string]$Version,

    [Parameter(Mandatory, Position = 1)]
    [ValidateNotNullOrEmpty()]
    [string]$Message
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false

if ([string]::IsNullOrWhiteSpace($Message)) { throw "릴리스 메시지가 비어 있음" }
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI가 없음. gh 설치 및 gh auth login 후 다시 실행할 것"
}
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw "git을 찾을 수 없음" }

$project = Join-Path $PSScriptRoot "src\MyStickies\MyStickies.csproj"
$projectVersion = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version |
    Where-Object { $_ } | Select-Object -First 1
if ($Version -ne $projectVersion) {
    throw "입력 버전($Version)과 프로젝트 버전($projectVersion)이 다름"
}

$setup = Join-Path $PSScriptRoot "dist\MyStickies-Setup-$Version.exe"
if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) {
    throw "설치 파일이 없음: $setup. 먼저 .\build-installer.ps1을 실행할 것"
}

$notesFile = $null
Push-Location -LiteralPath $PSScriptRoot
try {
    $changes = & git status --porcelain
    if ($LASTEXITCODE -ne 0) { throw "Git 작업 상태 조회 실패 (exit $LASTEXITCODE)" }
    if ($changes) { throw "커밋하지 않은 변경 사항이 있음. 커밋 및 푸시 후 다시 실행할 것" }

    $commit = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw "현재 커밋 조회 실패 (exit $LASTEXITCODE)" }

    # 한글, 따옴표와 줄바꿈을 그대로 전달하도록 UTF-8 파일로 릴리스 본문을 작성함
    $notesFile = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($notesFile, $Message, [System.Text.UTF8Encoding]::new($false))

    $tag = "v$Version"
    Write-Host "릴리스 게시: $tag / $setup"
    & gh release create $tag $setup --target $commit --title $tag --notes-file $notesFile --latest
    if ($LASTEXITCODE -ne 0) {
        throw "릴리스 게시 실패 (exit $LASTEXITCODE). gh 로그인, 커밋 푸시 여부와 동일 버전 릴리스를 확인할 것"
    }
    Write-Host "완료: $tag"
}
finally {
    if ($notesFile -and (Test-Path -LiteralPath $notesFile)) {
        Remove-Item -LiteralPath $notesFile -Force
    }
    Pop-Location
}

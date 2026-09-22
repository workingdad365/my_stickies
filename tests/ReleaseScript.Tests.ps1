# 외부 명령을 대체하여 실제 GitHub 게시 없이 릴리스 스크립트를 검증함
# 실행 예: pwsh -NoProfile -File .\tests\ReleaseScript.Tests.ps1

$ErrorActionPreference = "Stop"
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("mystickies_release_" + [Guid]::NewGuid().ToString("N"))
$originalLocation = (Get-Location).Path
$global:MyStickiesReleaseTestState = @{
    Dirty = $false
    ExitCode = 0
    Calls = [System.Collections.Generic.List[object]]::new()
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function git {
    $global:LASTEXITCODE = 0
    switch ($args[0]) {
        "status" { if ($global:MyStickiesReleaseTestState.Dirty) { " M README.md" } }
        "rev-parse" { "0123456789012345678901234567890123456789" }
        default { throw "예상하지 않은 git 호출: $args" }
    }
}

function gh {
    Assert-True ($args[0] -eq "release" -and $args[1] -eq "create") "릴리스 생성 명령이어야 함"
    $noteIndex = [Array]::IndexOf($args, "--notes-file")
    Assert-True ($noteIndex -ge 0) "본문을 파일로 전달해야 함"
    $path = $args[$noteIndex + 1]
    $global:MyStickiesReleaseTestState.Calls.Add([pscustomobject]@{
        Arguments = @($args)
        Notes = [System.IO.File]::ReadAllText($path)
        NotesPath = $path
        Location = (Get-Location).Path
    })
    $global:LASTEXITCODE = $global:MyStickiesReleaseTestState.ExitCode
}

function Assert-Fails([scriptblock]$Action, [string]$Pattern) {
    $failure = $null
    try { & $Action } catch { $failure = $_ }
    Assert-True ($null -ne $failure -and $failure.ToString() -match $Pattern) "예상한 오류가 발생해야 함: $Pattern"
}

try {
    $projectDirectory = Join-Path $fixtureRoot "src\MyStickies"
    $distDirectory = Join-Path $fixtureRoot "dist"
    New-Item -ItemType Directory -Path $projectDirectory, $distDirectory -Force | Out-Null
    $releaseScript = Join-Path $fixtureRoot "release.ps1"
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "..\release.ps1") -Destination $releaseScript
    Set-Content -LiteralPath (Join-Path $projectDirectory "MyStickies.csproj") -Value '<Project><PropertyGroup><Version>1.0.7</Version></PropertyGroup></Project>'
    $installer = Join-Path $distDirectory "MyStickies-Setup-1.0.7.exe"
    Set-Content -LiteralPath $installer -Value "검증용 설치 파일"

    # 위치 인자, 한글, 인용부호, 줄바꿈과 커밋 지정 검증
    $message = "메모 고정 기능 추가`n본문의 ""인용부호"" 유지"
    & $releaseScript 1.0.7 $message
    Assert-True ($global:MyStickiesReleaseTestState.Calls.Count -eq 1) "게시 명령은 한 번만 호출되어야 함"
    $call = $global:MyStickiesReleaseTestState.Calls[0]
    Assert-True ($call.Arguments[2] -eq "v1.0.7") "버전에 맞는 태그를 사용해야 함"
    Assert-True ($call.Arguments[3] -eq $installer) "지정 버전 설치 파일만 첨부해야 함"
    Assert-True ($call.Notes -ceq $message) "메시지가 그대로 전달되어야 함"
    $targetIndex = [Array]::IndexOf($call.Arguments, "--target")
    Assert-True ($targetIndex -ge 0 -and $call.Arguments[$targetIndex + 1] -eq "0123456789012345678901234567890123456789") "현재 커밋을 대상으로 지정해야 함"
    Assert-True ($call.Arguments -contains "--latest") "최신 릴리스로 지정해야 함"
    Assert-True ($call.Location -eq $fixtureRoot) "스크립트가 있는 저장소에서 실행해야 함"
    Assert-True (-not (Test-Path -LiteralPath $call.NotesPath)) "본문 임시 파일을 정리해야 함"
    Assert-True ((Get-Location).Path -eq $originalLocation) "원래 작업 폴더를 복원해야 함"

    Assert-Fails { & $releaseScript 1.0.8 "버전 불일치" } "프로젝트 버전"
    Assert-Fails { & $releaseScript 1.0.7 "   " } "메시지가 비어"
    Assert-Fails { & $releaseScript "../1.0.7" "잘못된 버전" } "Version"

    Remove-Item -LiteralPath $installer
    Assert-Fails { & $releaseScript 1.0.7 "파일 누락" } "설치 파일이 없음"
    Set-Content -LiteralPath $installer -Value "검증용 설치 파일"

    $global:MyStickiesReleaseTestState.Dirty = $true
    Assert-Fails { & $releaseScript 1.0.7 "미커밋 상태" } "커밋하지 않은"
    $global:MyStickiesReleaseTestState.Dirty = $false
    Assert-True ($global:MyStickiesReleaseTestState.Calls.Count -eq 1) "입력 오류가 있으면 게시를 호출하지 않아야 함"

    $global:MyStickiesReleaseTestState.ExitCode = 1
    Assert-Fails { & $releaseScript 1.0.7 "게시 오류" } "게시 실패"
    Assert-True (-not (Test-Path -LiteralPath $global:MyStickiesReleaseTestState.Calls[1].NotesPath)) "실패 시에도 임시 파일을 정리해야 함"
    Assert-True ((Get-Location).Path -eq $originalLocation) "실패 시에도 작업 폴더를 복원해야 함"
    Write-Host "릴리스 스크립트 검증 통과: 정상 게시 인자 및 오류 처리 7가지"
}
finally {
    # 이번 검증에서 만든 임시 폴더 안으로 삭제 범위를 제한함
    $tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $resolvedFixture = [System.IO.Path]::GetFullPath($fixtureRoot)
    if (-not $resolvedFixture.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not ([System.IO.Path]::GetFileName($resolvedFixture) -match '^mystickies_release_[0-9a-f]{32}$')) {
        throw "검증용 임시 폴더 범위를 확인할 수 없음"
    }
    if (Test-Path -LiteralPath $resolvedFixture) {
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
    Remove-Variable -Name MyStickiesReleaseTestState -Scope Global
}

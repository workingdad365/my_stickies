param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../../docs/media'),
    [ValidateSet('ko', 'en', 'zh-CN', 'ja')]
    [string[]]$Language = @('ko', 'en', 'zh-CN', 'ja')
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
$ffmpeg = (Get-Command ffmpeg -ErrorAction Stop).Source
$tempRoot = Join-Path $env:LOCALAPPDATA 'Temp/opencode'
$work = Join-Path $tempRoot ('my-stickies-demo-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $work, $output | Out-Null

function Replace-Checked([string]$Path, [string]$Old, [string]$New) {
    $text = [IO.File]::ReadAllText($Path)
    if (-not $text.Contains($Old)) { throw "데모 생성 대상 코드가 변경됨: $Path / $Old" }
    [IO.File]::WriteAllText($Path, $text.Replace($Old, $New))
}

try {
    # 임시 복사본에서만 저장 경로와 실행 환경을 격리함.
    $app = Join-Path $work 'app'
    New-Item -ItemType Directory $app | Out-Null
    Get-ChildItem (Join-Path $repo 'src/MyStickies') -Exclude bin,obj |
        Copy-Item -Destination $app -Recurse
    Copy-Item (Join-Path $PSScriptRoot 'MovieDirector.cs') $app
    Replace-Checked (Join-Path $app 'Data/AppSettings.cs') `
        'Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyStickies"' `
        'AppContext.BaseDirectory, "demo-data", Environment.GetCommandLineArgs()[3]'
    Replace-Checked (Join-Path $app 'App.xaml') 'StartupUri="MainWindow.xaml"' ''
    Replace-Checked (Join-Path $app 'App.xaml.cs') 'MyStickies.SingleInstance' 'MyStickies.IsolatedMovie'
    Replace-Checked (Join-Path $app 'App.xaml.cs') 'base.OnStartup(e);' "base.OnStartup(e);`n        MovieDirector.Start(this);"
    Replace-Checked (Join-Path $app 'MainWindow.xaml.cs') `
        'var wa = MonitorInfo.Resolve(_settings.DockMonitor).WorkAreaDip;' `
        'var wa = new Rect(-20000, -20000, 960, 640);'
    Replace-Checked (Join-Path $app 'MainWindow.xaml.cs') '            InitTray();' ''
    Replace-Checked (Join-Path $app 'Windows/FloatingNoteWindow.xaml.cs') `
        'var areas = MonitorInfo.All().OrderByDescending(m => m.IsPrimary).Select(m => m.WorkAreaDip).ToArray();' `
        'var areas = new[] { new Rect(-20000, -20000, 960, 640) };'

    dotnet build (Join-Path $app 'MyStickies.csproj') -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw '데모 앱 빌드 실패' }
    $exe = Join-Path $app 'bin/Release/net9.0-windows/MyStickies.exe'
    Copy-Item (Join-Path $PSScriptRoot 'locales.json') (Split-Path $exe)
    foreach ($locale in ($Language | Select-Object -Unique)) {
        $stem = if ($locale -eq 'ko') { 'my-stickies-demo' } else { "my-stickies-demo-$locale" }
        $process = Start-Process $exe -ArgumentList @("`"$output`"", "`"$ffmpeg`"", $locale) -PassThru -Wait
        if ($process.ExitCode -ne 0) { throw "데모 렌더링 실패 ($locale): 종료 코드 $($process.ExitCode)" }

        & $ffmpeg -hide_banner -loglevel error -y -i (Join-Path $output "$stem.mp4") `
            -vf 'fps=15,scale=960:-1:flags=lanczos' -c:v libwebp_anim -lossless 0 -q:v 72 `
            -compression_level 5 -loop 0 -an (Join-Path $output "$stem.webp")
        if ($LASTEXITCODE -ne 0) { throw "반복 미리보기 인코딩 실패 ($locale)" }
    }
    Write-Host "영상 생성 완료: $output"
}
finally {
    Remove-Item $work -Recurse -Force
}

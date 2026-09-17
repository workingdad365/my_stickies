using System.IO;
using Microsoft.Win32;

namespace MyStickies.Data;

/// <summary>Windows 로그인 시 자동 실행 등록 (HKCU Run 키)</summary>
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MyStickies";

    /// <summary>publish.ps1이 게시하는 설치 폴더: %LocalAppData%\Programs\MyStickies</summary>
    public static string InstallDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "MyStickies");

    /// <summary>현재 프로세스의 실행 파일 경로</summary>
    public static string? CurrentExePath => Environment.ProcessPath;

    /// <summary>Run 키에 등록할 명령줄. 경로에 공백이 있어도 되도록 따옴표로 감쌈</summary>
    public static string BuildCommand(string exePath) => $"\"{exePath}\"";

    /// <summary>실행 파일이 설치 폴더 안에 있는지. 빌드 출력(bin\Debug 등)에서 실행 중이면 false</summary>
    public static bool IsUnderDirectory(string exePath, string directory)
    {
        var dir = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(exePath).StartsWith(dir, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRunningFromInstallDirectory() =>
        CurrentExePath is { } exe && IsUnderDirectory(exe, InstallDirectory);

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (key is null) return;

        if (enabled)
        {
            var exe = CurrentExePath;
            if (string.IsNullOrEmpty(exe)) return;
            key.SetValue(ValueName, BuildCommand(exe));
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}

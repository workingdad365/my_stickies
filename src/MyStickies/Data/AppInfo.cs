using System.Reflection;

namespace MyStickies.Data;

/// <summary>앱 이름과 버전. 버전은 프로젝트 파일의 Version 속성에서 가져옴</summary>
public static class AppInfo
{
    public const string Name = "My Stickies";

    /// <summary>예: 1.0.0. 정보 버전에 붙는 커밋 해시(+...)는 제거</summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var asm = typeof(AppInfo).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            var plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }
        var v = asm.GetName().Version;
        return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }
}

using System.IO;
using System.Text.Json;

namespace MyStickies.Data;

/// <summary>
/// PC별 로컬 설정. 메모 DB 위치 등 이 PC에만 해당하는 값을 저장.
/// 동기화 폴더에 두는 DB와 달리 항상 %LocalAppData%에 보관
/// </summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>메모 DB 파일(my_stickies.db)을 두는 폴더</summary>
    public string DataDirectory { get; set; } = DefaultDataDirectory;

    /// <summary>덱을 붙일 모니터의 장치 이름. null이면 주 모니터</summary>
    public string? DockMonitor { get; set; }

    /// <summary>덱에 동시에 표시할 최대 메모 수</summary>
    public int DeckMaxNotes { get; set; } = 5;

    /// <summary>덱 시작 위치. 화면 위에서부터 퍼센트</summary>
    public int DeckTopPercent { get; set; } = 15;

    /// <summary>마우스가 벗어난 뒤 덱이 접히기까지 지연 (ms)</summary>
    public int CollapseDelayMs { get; set; } = 350;

    /// <summary>전체화면 앱이 같은 모니터 맨 앞에 있을 때 덱을 숨길지</summary>
    public bool HideOnFullscreen { get; set; } = true;

    /// <summary>전역 단축키(Ctrl+Alt+S 덱, Ctrl+Alt+N 새 메모) 사용 여부</summary>
    public bool GlobalHotkeys { get; set; } = true;

    /// <summary>메모 제목과 본문에 쓰는 글꼴 이름</summary>
    public string FontFamily { get; set; } = "Malgun Gothic";

    /// <summary>본문 글꼴 크기. 제목과 상세 창 크기는 이 값에서 파생</summary>
    public int FontSize { get; set; } = 14;

    /// <summary>이 PC의 앱 로컬 폴더: %LocalAppData%\MyStickies</summary>
    public static string LocalDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyStickies");

    /// <summary>기본 메모 저장 폴더. 로컬 폴더와 같음</summary>
    public static string DefaultDataDirectory => LocalDirectory;

    /// <summary>설정 파일 경로</summary>
    public static string SettingsPath => Path.Combine(LocalDirectory, "settings.json");

    /// <summary>설정 파일 로드. 없거나 손상되었으면 null (최초 실행으로 간주)</summary>
    public static AppSettings? Load(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
            if (settings is null || string.IsNullOrWhiteSpace(settings.DataDirectory))
                return null;
            return settings;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }
}

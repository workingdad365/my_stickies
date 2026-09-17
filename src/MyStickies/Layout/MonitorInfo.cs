using System.Windows;
using MyStickies.Interop;
using WF = System.Windows.Forms;

namespace MyStickies.Layout;

/// <summary>모니터 한 대의 정보. 좌표는 물리 픽셀, Scale은 DPI 배율(100% = 1.0)</summary>
public sealed record MonitorInfo(
    string DeviceName,
    int Index,
    bool IsPrimary,
    Rect Bounds,
    Rect WorkArea,
    double Scale)
{
    /// <summary>설정 창 목록에 표시할 이름. 예: 모니터 1 (3840x2160, 200%, 주)</summary>
    public string DisplayName
    {
        get
        {
            var label = $"모니터 {Index + 1} ({Bounds.Width:0}x{Bounds.Height:0}, {Scale * 100:0}%";
            if (IsPrimary) label += ", 주";
            return label + ")";
        }
    }

    /// <summary>작업 영역을 DIP(장치 독립 픽셀)로 변환</summary>
    public Rect WorkAreaDip => ToDip(WorkArea, Scale);

    /// <summary>물리 픽셀 사각형을 배율로 나눠 DIP로 변환</summary>
    public static Rect ToDip(Rect physical, double scale) =>
        new(physical.X / scale, physical.Y / scale, physical.Width / scale, physical.Height / scale);

    /// <summary>연결된 모니터 전체. 주 모니터가 항상 포함됨</summary>
    public static IReadOnlyList<MonitorInfo> All()
    {
        var screens = WF.Screen.AllScreens;
        var list = new List<MonitorInfo>(screens.Length);
        for (var i = 0; i < screens.Length; i++)
        {
            var s = screens[i];
            var scale = NativeMethods.GetScaleForPoint(s.Bounds.Left + 1, s.Bounds.Top + 1);
            list.Add(new MonitorInfo(
                s.DeviceName, i, s.Primary,
                new Rect(s.Bounds.Left, s.Bounds.Top, s.Bounds.Width, s.Bounds.Height),
                new Rect(s.WorkingArea.Left, s.WorkingArea.Top, s.WorkingArea.Width, s.WorkingArea.Height),
                scale));
        }
        return list;
    }

    /// <summary>장치 이름으로 모니터 선택. 없거나 분리되었으면 주 모니터</summary>
    public static MonitorInfo Resolve(string? deviceName)
    {
        var all = All();
        return all.FirstOrDefault(m => deviceName is not null &&
                                       string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
               ?? all.First(m => m.IsPrimary);
    }
}

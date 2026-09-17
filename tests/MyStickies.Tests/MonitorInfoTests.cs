using System.Windows;
using MyStickies.Layout;

namespace MyStickies.Tests;

public class MonitorInfoTests
{
    [Fact]
    public void ToDip_DividesByScale()
    {
        // 4K 200% 모니터의 작업 영역 (작업 표시줄 96px 제외)
        var dip = MonitorInfo.ToDip(new Rect(0, 0, 3840, 2064), 2.0);

        Assert.Equal(new Rect(0, 0, 1920, 1032), dip);
    }

    [Fact]
    public void ToDip_KeepsOffsetOfSecondaryMonitor()
    {
        var dip = MonitorInfo.ToDip(new Rect(3840, 0, 3840, 2160), 2.0);

        Assert.Equal(1920, dip.X);
        Assert.Equal(1920, dip.Width);
    }

    [Fact]
    public void DisplayName_ShowsResolutionScaleAndPrimaryMark()
    {
        var primary = new MonitorInfo(@"\\.\DISPLAY1", 0, true, new Rect(0, 0, 3840, 2160), new Rect(0, 0, 3840, 2064), 2.0);
        var second = new MonitorInfo(@"\\.\DISPLAY2", 1, false, new Rect(3840, 0, 3840, 2160), new Rect(3840, 0, 3840, 2160), 1.5);

        Assert.Equal("모니터 1 (3840x2160, 200%, 주)", primary.DisplayName);
        Assert.Equal("모니터 2 (3840x2160, 150%)", second.DisplayName);
    }

    [Fact]
    public void All_ContainsExactlyOnePrimary()
    {
        var monitors = MonitorInfo.All();

        Assert.NotEmpty(monitors);
        Assert.Single(monitors, m => m.IsPrimary);
        Assert.All(monitors, m => Assert.True(m.Scale >= 1.0));
    }

    [Fact]
    public void Resolve_FallsBackToPrimaryForUnknownName()
    {
        var resolved = MonitorInfo.Resolve(@"\\.\NOPE");
        Assert.True(resolved.IsPrimary);
    }
}

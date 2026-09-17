using MyStickies.Interop;

namespace MyStickies.Tests;

public class FullscreenDetectorTests
{
    private static FullscreenDetector.RECT R(int l, int t, int r, int b) =>
        new() { Left = l, Top = t, Right = r, Bottom = b };

    private static readonly FullscreenDetector.RECT Monitor = R(3840, 0, 7680, 2160);

    [Fact]
    public void ExactMonitorRect_IsFullscreen()
    {
        Assert.True(FullscreenDetector.CoversMonitor(R(3840, 0, 7680, 2160), Monitor));
    }

    [Fact]
    public void LargerThanMonitor_IsFullscreen()
    {
        // 일부 게임은 테두리만큼 모니터보다 큰 사각형을 가짐
        Assert.True(FullscreenDetector.CoversMonitor(R(3832, -8, 7688, 2168), Monitor));
    }

    [Fact]
    public void MaximizedWindowLeavingTaskbar_IsNotFullscreen()
    {
        Assert.False(FullscreenDetector.CoversMonitor(R(3840, 0, 7680, 2064), Monitor));
    }

    [Fact]
    public void NormalWindow_IsNotFullscreen()
    {
        Assert.False(FullscreenDetector.CoversMonitor(R(4000, 100, 6000, 1500), Monitor));
    }

    [Fact]
    public void NoForegroundFullscreen_ForOwnWindowHandle()
    {
        // 테스트 실행 중 앞에 있는 창이 테스트 프로세스 창일 수 없으므로, 임의 핸들에 대해 예외 없이 동작해야 함
        var result = FullscreenDetector.IsForegroundFullscreenOnSameMonitor(IntPtr.Zero);
        Assert.IsType<bool>(result);
    }
}

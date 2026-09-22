using System.Windows;

namespace MyStickies.Layout;

/// <summary>고정 메모를 연결된 모니터의 작업 영역 안에 배치하는 계산</summary>
public static class FloatingNoteGeometry
{
    public static Point ClampPosition(double left, double top, double width, double height,
        IReadOnlyList<Rect> workAreas)
    {
        if (workAreas.Count == 0) return new Point(0, 0);
        if (!double.IsFinite(left) || !double.IsFinite(top))
            return workAreas[0].TopLeft;

        var window = new Rect(left, top, width, height);
        var area = workAreas.OrderByDescending(a => IntersectionArea(a, window)).First();
        return new Point(
            Math.Clamp(left, area.Left, Math.Max(area.Left, area.Right - width)),
            Math.Clamp(top, area.Top, Math.Max(area.Top, area.Bottom - height)));
    }

    private static double IntersectionArea(Rect a, Rect b)
    {
        var intersection = Rect.Intersect(a, b);
        return intersection.IsEmpty ? 0 : intersection.Width * intersection.Height;
    }
}

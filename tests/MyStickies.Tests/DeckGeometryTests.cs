using MyStickies.Layout;

namespace MyStickies.Tests;

public class DeckGeometryTests
{
    [Fact]
    public void WindowRect_IsDockedToRightEdgeOfWorkArea()
    {
        // 4K 150% 배율 기준 작업 영역: 2560 x 1392 DIP (작업 표시줄 제외)
        var (left, top, width, height) = DeckGeometry.WindowRect(0, 0, 2560, 1392);

        Assert.Equal(2560 - DeckGeometry.WindowWidth, left);
        Assert.Equal(0, top);
        Assert.Equal(DeckGeometry.WindowWidth, width);
        Assert.Equal(1392, height);
    }

    [Fact]
    public void WindowRect_RespectsWorkAreaOffset()
    {
        var (left, top, _, _) = DeckGeometry.WindowRect(100, 40, 2000, 1000);

        Assert.Equal(100 + 2000 - DeckGeometry.WindowWidth, left);
        Assert.Equal(40, top);
    }

    [Fact]
    public void Offsets_AreOrderedExpandedFannedDormant()
    {
        Assert.Equal(0, DeckGeometry.ExpandedOffset);
        Assert.Equal(DeckGeometry.CardWidth - DeckGeometry.PeekWidth, DeckGeometry.FannedOffset);
        Assert.True(DeckGeometry.ExpandedOffset < DeckGeometry.FannedOffset);
        Assert.True(DeckGeometry.FannedOffset < DeckGeometry.DormantOffset);
        Assert.True(DeckGeometry.DormantOffset >= DeckGeometry.CardWidth, "휴면 오프셋은 카드를 완전히 숨겨야 함");
    }

    [Fact]
    public void DeckTop_UsesRatioWhenDeckFits()
    {
        var top = DeckGeometry.DeckTop(1000, 600);
        Assert.Equal(1000 * DeckGeometry.DeckTopRatio, top);
    }

    [Fact]
    public void DeckTop_ClampsSoDeckStaysOnScreen()
    {
        var top = DeckGeometry.DeckTop(1000, 900);
        Assert.Equal(1000 - 900 - DeckGeometry.DeckBottomMargin, top);
    }

    [Fact]
    public void DeckTop_IsZeroWhenDeckTallerThanScreen()
    {
        var top = DeckGeometry.DeckTop(500, 900);
        Assert.Equal(0, top);
    }
}

public class DeckConfigureTests : IDisposable
{
    public void Dispose() => DeckGeometry.Configure(DeckGeometry.DefaultMaxDeckNotes, DeckGeometry.DefaultDeckTopRatio);

    [Fact]
    public void Configure_AppliesValuesWithinRange()
    {
        DeckGeometry.Configure(7, 0.3);

        Assert.Equal(7, DeckGeometry.MaxDeckNotes);
        Assert.Equal(0.3, DeckGeometry.DeckTopRatio);
        Assert.Equal(7 * (DeckGeometry.CardHeight + DeckGeometry.CardGap), DeckGeometry.DeckCapacityHeight);
    }

    [Fact]
    public void Configure_ClampsOutOfRangeValues()
    {
        DeckGeometry.Configure(100, 5);
        Assert.Equal(DeckGeometry.MaxMaxDeckNotes, DeckGeometry.MaxDeckNotes);
        Assert.Equal(DeckGeometry.MaxDeckTopRatio, DeckGeometry.DeckTopRatio);

        DeckGeometry.Configure(0, -1);
        Assert.Equal(DeckGeometry.MinMaxDeckNotes, DeckGeometry.MaxDeckNotes);
        Assert.Equal(0, DeckGeometry.DeckTopRatio);
    }
}

public class ExpandedHeightTests
{
    [Fact]
    public void ShortContent_UsesMinimumHeight()
    {
        Assert.Equal(DeckGeometry.ExpandedHeight, DeckGeometry.ExpandedHeightFor(40));
    }

    [Fact]
    public void MediumContent_GrowsWithVerticalMargin()
    {
        var content = 300;
        Assert.Equal(content + DeckGeometry.BodyVerticalMargin, DeckGeometry.ExpandedHeightFor(content));
    }

    [Fact]
    public void LongContent_IsCappedAtMaximum()
    {
        Assert.Equal(DeckGeometry.MaxExpandedHeight, DeckGeometry.ExpandedHeightFor(2000));
    }

    [Fact]
    public void BodyTextWidth_LeavesRoomForLabelColumnAndMargins()
    {
        Assert.Equal(DeckGeometry.CardWidth - DeckGeometry.LabelColumnWidth - 1 - DeckGeometry.BodyHorizontalMargin,
            DeckGeometry.BodyTextWidth);
        Assert.True(DeckGeometry.BodyTextWidth > 200);
    }
}

public class DeckCapacityTests
{
    [Fact]
    public void DeckBlockHeight_IsIndependentOfNoteCount()
    {
        var expected = DeckGeometry.MaxDeckNotes * (DeckGeometry.CardHeight + DeckGeometry.CardGap)
                       + DeckGeometry.PlusGap + DeckGeometry.PlusButtonSize;
        Assert.Equal(expected, DeckGeometry.DeckBlockHeight);
    }

    [Fact]
    public void PlusTop_SitsBelowFullDeck()
    {
        var plusTop = DeckGeometry.PlusTop(200);
        Assert.Equal(200 + DeckGeometry.DeckCapacityHeight + DeckGeometry.PlusGap, plusTop);
    }

    [Fact]
    public void FullDeckWithPlus_FitsIn4kWorkAreaAt200Percent()
    {
        // 4K 200% 배율: 작업 영역 높이 1032 DIP
        var deckTop = DeckGeometry.DeckTop(1032, DeckGeometry.DeckBlockHeight);
        Assert.True(deckTop >= 0);
        Assert.True(DeckGeometry.PlusTop(deckTop) + DeckGeometry.PlusButtonSize + DeckGeometry.DeckBottomMargin <= 1032,
            "덱 하단에 여백이 남아야 함");
    }
}

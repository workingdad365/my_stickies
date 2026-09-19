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
    public void DeckTop_CentersDeckOnConfiguredLineWhenItFits()
    {
        var top = DeckGeometry.DeckTop(1000, 600);
        Assert.Equal(1000 * DeckGeometry.DeckCenterRatio - 300, top);
    }

    [Fact]
    public void DeckTop_GrowsEvenlyAroundCenterAsNotesAreAdded()
    {
        var one = DeckGeometry.DeckTop(1000, DeckGeometry.DeckBlockHeightFor(1));
        var three = DeckGeometry.DeckTop(1000, DeckGeometry.DeckBlockHeightFor(3));
        var step = DeckGeometry.CardHeight + DeckGeometry.CardGap;
        Assert.Equal(one - step, three, 6);
        Assert.Equal(one + DeckGeometry.DeckBlockHeightFor(1) / 2, three + DeckGeometry.DeckBlockHeightFor(3) / 2, 6);
    }

    [Fact]
    public void DeckTop_ClampsSoDeckStaysAboveBottomMargin()
    {
        // 중심선을 아래쪽(70%)에 두면 덱이 하단 여백을 넘으므로 여백 위로 끌어올려짐
        DeckGeometry.Configure(DeckGeometry.DefaultMaxDeckNotes, 0.7);
        try
        {
            var top = DeckGeometry.DeckTop(1000, 900);
            Assert.Equal(1000 - 900 - DeckGeometry.DeckBottomMargin, top);
        }
        finally { DeckGeometry.Configure(DeckGeometry.DefaultMaxDeckNotes, DeckGeometry.DefaultDeckCenterRatio); }
    }

    [Fact]
    public void DeckTop_NeverGoesAboveTopMargin()
    {
        Assert.Equal(DeckGeometry.DeckTopMargin, DeckGeometry.DeckTop(500, 900));
        DeckGeometry.Configure(DeckGeometry.DefaultMaxDeckNotes, 0.1);
        try { Assert.Equal(DeckGeometry.DeckTopMargin, DeckGeometry.DeckTop(1000, 600)); }
        finally { DeckGeometry.Configure(DeckGeometry.DefaultMaxDeckNotes, DeckGeometry.DefaultDeckCenterRatio); }
    }
}

public class DeckConfigureTests : IDisposable
{
    public void Dispose() => DeckGeometry.Configure(DeckGeometry.DefaultMaxDeckNotes, DeckGeometry.DefaultDeckCenterRatio);

    [Theory]
    [InlineData(0, 32)]
    [InlineData(1, 92)]
    [InlineData(5, 236)]
    [InlineData(-1, 32)]
    [InlineData(100, 236)]
    public void BookmarkBlockHeight_IncludesControlsOnlyWithNotes(int count, double expected)
    {
        Assert.Equal(expected, DeckGeometry.BookmarkBlockHeightFor(count));
    }

    [Fact]
    public void BookmarkDrag_MovesEvenWhenExpandedDeckFillsWorkArea()
    {
        DeckGeometry.Configure(8, 0.5);
        var initialTop = DeckGeometry.BookmarkTop(1032, 8);
        var ratio = DeckGeometry.BookmarkCenterRatioForTop(1032, 8, initialTop + 100);
        DeckGeometry.Configure(8, ratio);

        Assert.Equal(initialTop + 100, DeckGeometry.BookmarkTop(1032, 8), 6);
        Assert.Equal(DeckGeometry.DeckTopMargin,
            DeckGeometry.DeckTop(1032, DeckGeometry.DeckBlockHeightFor(8)));
    }

    [Theory]
    [InlineData(-1000)]
    [InlineData(2000)]
    public void BookmarkDrag_ClampsControlsInsideWorkArea(double requestedTop)
    {
        var ratio = DeckGeometry.BookmarkCenterRatioForTop(600, 5, requestedTop);
        DeckGeometry.Configure(5, ratio);
        var top = DeckGeometry.BookmarkTop(600, 5);

        Assert.InRange(ratio, DeckGeometry.MinDeckCenterRatio, DeckGeometry.MaxDeckCenterRatio);
        Assert.InRange(top, DeckGeometry.DeckTopMargin,
            600 - DeckGeometry.BookmarkBlockHeightFor(5) - DeckGeometry.DeckBottomMargin);
    }

    [Fact]
    public void BookmarkDrag_ZeroWorkHeightKeepsCurrentRatio()
    {
        Assert.Equal(DeckGeometry.DeckCenterRatio,
            DeckGeometry.BookmarkCenterRatioForTop(0, 1, 100));
    }

    [Fact]
    public void Configure_AppliesValuesWithinRange()
    {
        DeckGeometry.Configure(7, 0.3);

        Assert.Equal(7, DeckGeometry.MaxDeckNotes);
        Assert.Equal(0.3, DeckGeometry.DeckCenterRatio);
        Assert.Equal(7 * (DeckGeometry.CardHeight + DeckGeometry.CardGap), DeckGeometry.DeckCapacityHeight);
    }

    [Fact]
    public void Configure_ClampsOutOfRangeValues()
    {
        DeckGeometry.Configure(100, 5);
        Assert.Equal(DeckGeometry.MaxMaxDeckNotes, DeckGeometry.MaxDeckNotes);
        Assert.Equal(DeckGeometry.MaxDeckCenterRatio, DeckGeometry.DeckCenterRatio);

        DeckGeometry.Configure(0, -1);
        Assert.Equal(DeckGeometry.MinMaxDeckNotes, DeckGeometry.MaxDeckNotes);
        Assert.Equal(DeckGeometry.MinDeckCenterRatio, DeckGeometry.DeckCenterRatio);
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
    public void PlusTop_SitsBelowLastVisibleNote()
    {
        var plusTop = DeckGeometry.PlusTop(200, 2);
        Assert.Equal(200 + 2 * (DeckGeometry.CardHeight + DeckGeometry.CardGap) + DeckGeometry.PlusGap, plusTop);
    }

    [Fact]
    public void DeckHeightFor_ClampsToCapacity()
    {
        Assert.Equal(0, DeckGeometry.DeckHeightFor(0));
        Assert.Equal(DeckGeometry.CardHeight + DeckGeometry.CardGap, DeckGeometry.DeckHeightFor(1));
        Assert.Equal(DeckGeometry.DeckCapacityHeight, DeckGeometry.DeckHeightFor(DeckGeometry.MaxDeckNotes + 3));
        Assert.Equal(DeckGeometry.DeckBlockHeight, DeckGeometry.DeckBlockHeightFor(DeckGeometry.MaxDeckNotes));
    }

    [Fact]
    public void FullDeckWithPlus_FitsIn4kWorkAreaAt200Percent()
    {
        // 4K 200% 배율: 작업 영역 높이 1032 DIP
        var count = DeckGeometry.MaxDeckNotes;
        var deckTop = DeckGeometry.DeckTop(1032, DeckGeometry.DeckBlockHeightFor(count));
        Assert.True(deckTop >= 0);
        Assert.True(DeckGeometry.PlusTop(deckTop, count) + DeckGeometry.PlusButtonSize + DeckGeometry.DeckBottomMargin <= 1032,
            "덱 하단에 여백이 남아야 함");
    }
}

/// <summary>1080p 작업 영역(1032 DIP)에서 덱 최대 6장, 중심 50%로 두고 메모 수가 바뀌는 경우</summary>
public class DeckTopWithFewNotesTests : IDisposable
{
    public DeckTopWithFewNotesTests() => DeckGeometry.Configure(6, 0.5);
    public void Dispose() => DeckGeometry.Configure(DeckGeometry.DefaultMaxDeckNotes, DeckGeometry.DefaultDeckCenterRatio);

    [Fact]
    public void SingleNote_SitsAtScreenCenter_NotAtTop()
    {
        var block = DeckGeometry.DeckBlockHeightFor(1);
        var deckTop = DeckGeometry.DeckTop(1032, block);
        Assert.Equal(516 - block / 2, deckTop);
        Assert.True(deckTop > 300, "메모가 1장이면 화면 위쪽에 붙지 않아야 함");
    }

    [Fact]
    public void FullDeck_TooTallForBothMargins_KeepsTopMarginAndStaysOnScreen()
    {
        // 6장(982 DIP)은 상하 여백 40씩을 모두 확보할 수 없음. 상단 여백을 우선하고 화면 안에는 들어가야 함
        var deckTop = DeckGeometry.DeckTop(1032, DeckGeometry.DeckBlockHeightFor(6));
        Assert.Equal(DeckGeometry.DeckTopMargin, deckTop);
        Assert.True(DeckGeometry.PlusTop(deckTop, 6) + DeckGeometry.PlusButtonSize <= 1032);
    }

    [Fact]
    public void FiveNotes_FitWithinBothMargins()
    {
        var deckTop = DeckGeometry.DeckTop(1032, DeckGeometry.DeckBlockHeightFor(5));
        Assert.True(deckTop >= DeckGeometry.DeckTopMargin);
        Assert.True(DeckGeometry.PlusTop(deckTop, 5) + DeckGeometry.PlusButtonSize + DeckGeometry.DeckBottomMargin <= 1032);
    }

    [Fact]
    public void CapacityBasedClamp_WouldHavePinnedDeckToTop()
    {
        // 수정 전 동작 재현: 최대 수용 개수 기준 보정은 메모가 1장이어도 덱을 화면 맨 위에 붙였음
        var oldTop = DeckGeometry.DeckTop(1032, DeckGeometry.DeckBlockHeight);
        Assert.Equal(DeckGeometry.DeckTopMargin, oldTop);
    }
}

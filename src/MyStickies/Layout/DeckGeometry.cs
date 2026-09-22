namespace MyStickies.Layout;

/// <summary>
/// 덱(노트 묶음) 배치 계산. WPF 타입에 의존하지 않는 순수 계산이므로 단위 테스트 가능.
/// 단위는 모두 DIP(장치 독립 픽셀). 4K 등 고DPI 환경에서는 WPF가 자동으로 배율 적용.
/// </summary>
public static class DeckGeometry
{
    /// <summary>노트 카드 전체 폭</summary>
    public const double CardWidth = 340;

    /// <summary>팬아웃 상태에서 화면에 보이는 폭 (세로 라벨 + 점선)</summary>
    public const double PeekWidth = 76;

    /// <summary>팬아웃 상태의 카드 높이</summary>
    public const double CardHeight = 150;

    /// <summary>확장 상태의 최소 카드 높이</summary>
    public const double ExpandedHeight = 200;

    /// <summary>확장 상태의 최대 카드 높이. 초과분은 본문 스크롤</summary>
    public const double MaxExpandedHeight = 480;

    /// <summary>카드 왼쪽 세로 라벨 열 폭</summary>
    public const double LabelColumnWidth = 44;

    /// <summary>본문 영역 좌우 여백 (왼쪽 14 + 오른쪽 12)</summary>
    public const double BodyHorizontalMargin = 26;

    /// <summary>본문 영역 상하 여백 (위 12 + 아래 12)</summary>
    public const double BodyVerticalMargin = 24;

    /// <summary>본문 텍스트가 줄바꿈되는 폭 (카드 폭 - 라벨 열 - 점선 1 - 좌우 여백)</summary>
    public static double BodyTextWidth => CardWidth - LabelColumnWidth - 1 - BodyHorizontalMargin;

    /// <summary>본문 내용 높이에 맞는 확장 카드 높이. 최소/최대 범위로 제한</summary>
    public static double ExpandedHeightFor(double contentHeight) =>
        Math.Clamp(contentHeight + BodyVerticalMargin, ExpandedHeight, MaxExpandedHeight);

    /// <summary>도킹 창 폭 (카드 폭 + 그림자 여유)</summary>
    public const double WindowWidth = 360;

    /// <summary>카드 사이 세로 간격</summary>
    public const double CardGap = 8;

    /// <summary>덱 표시 개수 기본값과 허용 범위</summary>
    public const int DefaultMaxDeckNotes = 5;
    public const int MinMaxDeckNotes = 3;
    public const int MaxMaxDeckNotes = 8;

    /// <summary>덱 세로 중심 위치 비율 기본값(0.5 = 화면 세로 중앙)과 허용 범위</summary>
    public const double DefaultDeckCenterRatio = 0.5;
    public const double MinDeckCenterRatio = 0.1;
    public const double MaxDeckCenterRatio = 0.9;

    /// <summary>덱에 동시에 표시하는 최대 노트 수. 초과분은 최근 노트만 표시. Configure로 변경</summary>
    public static int MaxDeckNotes { get; private set; } = DefaultMaxDeckNotes;

    /// <summary>새 노트 추가 버튼 크기</summary>
    public const double PlusButtonSize = 30;

    /// <summary>덱 하단과 추가 버튼 사이 간격</summary>
    public const double PlusGap = 4;

    /// <summary>화면 높이 대비 덱 세로 중심 위치 비율. 덱은 이 선을 중심으로 위아래로 늘어남. Configure로 변경</summary>
    public static double DeckCenterRatio { get; private set; } = DefaultDeckCenterRatio;

    /// <summary>설정값 적용. 범위를 벗어나면 허용 범위로 보정</summary>
    public static void Configure(int maxDeckNotes, double deckCenterRatio)
    {
        MaxDeckNotes = Math.Clamp(maxDeckNotes, MinMaxDeckNotes, MaxMaxDeckNotes);
        DeckCenterRatio = Math.Clamp(deckCenterRatio, MinDeckCenterRatio, MaxDeckCenterRatio);
    }

    /// <summary>덱 블록 위에 남겨 두는 최소 여백. 다른 창의 제목 표시줄 버튼을 가리지 않도록 함</summary>
    public const double DeckTopMargin = 40;

    /// <summary>덱 블록 아래에 남겨 두는 최소 여백. 모니터 바닥에 붙지 않도록 함</summary>
    public const double DeckBottomMargin = 40;

    /// <summary>팬아웃 중 마우스를 붙잡아 두는 보이지 않는 호버 영역 폭. 탭 노출 폭보다 약간 넓게</summary>
    public static double HoverZoneWidth => PeekWidth + 12;

    /// <summary>추가 버튼 아래에서 커서가 조금 벗어나도 팬아웃을 유지하는 여백</summary>
    public const double HoverZoneBottomPadding = 16;

    /// <summary>실제 덱 높이와 하단 여백을 포함하며, 이전 높이를 넘기면 카드 축소 중에도 이동 경로를 유지함</summary>
    public static double HoverZoneHeightFor(double deckHeight, double previousHeight = 0) =>
        Math.Max(deckHeight + HoverZoneBottomPadding, previousHeight);

    /// <summary>덱이 가득 찼을 때 카드 영역 높이. 노트 수와 무관하게 고정</summary>
    public static double DeckCapacityHeight => MaxDeckNotes * (CardHeight + CardGap);

    /// <summary>덱 카드 영역 + 추가 버튼까지 포함한 전체 높이</summary>
    public static double DeckBlockHeight => DeckCapacityHeight + PlusGap + PlusButtonSize;

    /// <summary>실제 표시 노트 수 기준 카드 영역 높이. 최대 수용 개수를 넘지 않음</summary>
    public static double DeckHeightFor(int noteCount) =>
        Math.Clamp(noteCount, 0, MaxDeckNotes) * (CardHeight + CardGap);

    /// <summary>
    /// 실제 표시 노트 수 기준 카드 영역 + 추가 버튼 전체 높이.
    /// 시작 위치 보정과 호버 영역은 최대 수용 개수가 아니라 이 값을 사용함.
    /// 최대 수용 개수 기준으로 보정하면 작은 화면에서 메모가 적어도 시작 위치 설정이 무시됨
    /// </summary>
    public static double DeckBlockHeightFor(int noteCount) => DeckHeightFor(noteCount) + PlusGap + PlusButtonSize;

    public const double BookmarkControlHeight = 20;

    public static double BookmarkBlockHeightFor(int noteCount)
    {
        var count = Math.Clamp(noteCount, 0, MaxDeckNotes);
        return count == 0 ? 32 : 16 + count * 36 + BookmarkControlHeight * 2;
    }

    public static double BookmarkTop(double workHeight, int noteCount) =>
        DeckTop(workHeight, BookmarkBlockHeightFor(noteCount));

    public static double BookmarkCenterRatioForTop(double workHeight, int noteCount, double top)
    {
        if (workHeight <= 0) return DeckCenterRatio;
        var blockHeight = BookmarkBlockHeightFor(noteCount);
        var maxTop = Math.Max(DeckTopMargin, workHeight - blockHeight - DeckBottomMargin);
        var clampedTop = Math.Clamp(top, DeckTopMargin, maxTop);
        return Math.Clamp((clampedTop + blockHeight / 2) / workHeight,
            MinDeckCenterRatio, MaxDeckCenterRatio);
    }

    /// <summary>추가 버튼 상단 위치(팬아웃 상태 기준). 실제 표시 중인 마지막 노트 바로 아래. 배치 검증에 사용</summary>
    public static double PlusTop(double deckTop, int noteCount) => deckTop + DeckHeightFor(noteCount) + PlusGap;

    /// <summary>확장 상태: 카드가 완전히 화면 안에 위치</summary>
    public static double ExpandedOffset => 0;

    /// <summary>팬아웃 상태: PeekWidth 만큼만 보이도록 오른쪽으로 밀어냄</summary>
    public static double FannedOffset => CardWidth - PeekWidth;

    /// <summary>휴면 상태: 카드가 창 밖으로 완전히 나감</summary>
    public static double DormantOffset => WindowWidth;

    /// <summary>작업 영역 우측 가장자리에 세로로 붙는 창 영역 계산</summary>
    public static (double Left, double Top, double Width, double Height) WindowRect(
        double workLeft, double workTop, double workWidth, double workHeight) =>
        (workLeft + workWidth - WindowWidth, workTop, WindowWidth, workHeight);

    /// <summary>
    /// 덱의 상단 위치. 설정한 중심선에 덱 블록의 세로 중앙을 맞추고,
    /// 상하 여백을 벗어나면 화면 안으로 보정. 덱이 화면보다 크면 상단 여백에 붙임
    /// </summary>
    public static double DeckTop(double workHeight, double deckHeight)
    {
        var top = workHeight * DeckCenterRatio - deckHeight / 2;
        var max = Math.Max(DeckTopMargin, workHeight - deckHeight - DeckBottomMargin);
        return Math.Clamp(top, DeckTopMargin, max);
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using MyStickies.Converters;
using MyStickies.Layout;
using MyStickies.Models;
using MyStickies.Localization;

namespace MyStickies.Controls;

/// <summary>편집 종료 이벤트 인자. Saved가 false면 변경을 버리고 종료한 것</summary>
public sealed class EditEndedEventArgs(RoutedEvent routedEvent, object source, bool saved)
    : RoutedEventArgs(routedEvent, source)
{
    public bool Saved { get; } = saved;
}

/// <summary>덱에 꽂힌 노트 한 장. 슬라이드/확장 애니메이션과 편집 모드 담당</summary>
public partial class NoteTab : UserControl
{
    private static readonly TimeSpan SlideDuration = TimeSpan.FromMilliseconds(260);
    private static readonly TimeSpan ExpandDuration = TimeSpan.FromMilliseconds(200);

    private static IEasingFunction Ease => new CubicEase { EasingMode = EasingMode.EaseOut };

    public static readonly RoutedEvent EditStartedEvent = EventManager.RegisterRoutedEvent(
        nameof(EditStarted), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NoteTab));

    public static readonly RoutedEvent EditEndedEvent = EventManager.RegisterRoutedEvent(
        nameof(EditEnded), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NoteTab));

    public static readonly RoutedEvent DeleteRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(DeleteRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NoteTab));

    public static readonly RoutedEvent PinRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(PinRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NoteTab));

    /// <summary>고정 또는 고정 해제 요청</summary>
    public event RoutedEventHandler PinRequested
    {
        add => AddHandler(PinRequestedEvent, value);
        remove => RemoveHandler(PinRequestedEvent, value);
    }

    /// <summary>편집 모드 진입. 창 활성화 등 상위에서 처리할 준비를 요청</summary>
    public event RoutedEventHandler EditStarted
    {
        add => AddHandler(EditStartedEvent, value);
        remove => RemoveHandler(EditStartedEvent, value);
    }

    /// <summary>편집 모드 종료. 인자는 EditEndedEventArgs이며 Saved로 저장 여부 구분</summary>
    public event RoutedEventHandler EditEnded
    {
        add => AddHandler(EditEndedEvent, value);
        remove => RemoveHandler(EditEndedEvent, value);
    }

    /// <summary>삭제 버튼 클릭</summary>
    public event RoutedEventHandler DeleteRequested
    {
        add => AddHandler(DeleteRequestedEvent, value);
        remove => RemoveHandler(DeleteRequestedEvent, value);
    }

    public static readonly RoutedEvent ArchiveRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(ArchiveRequested), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NoteTab));

    /// <summary>숨김 버튼 클릭</summary>
    public event RoutedEventHandler ArchiveRequested
    {
        add => AddHandler(ArchiveRequestedEvent, value);
        remove => RemoveHandler(ArchiveRequestedEvent, value);
    }

    public bool IsExpanded { get; private set; }
    public bool IsEditing { get; private set; }
    private bool _useWindowSize;

    /// <summary>편집 취소 시 되돌릴 색상</summary>
    private string _colorBeforeEdit = NotePalette.Blue;

    private static readonly SolidColorBrush DeleteIdleBrush = HexToBrushConverter.Brush("#2E000000");
    private static readonly SolidColorBrush DeleteArmedBrush = HexToBrushConverter.Brush("#D9433A");

    /// <summary>삭제 확인 대기 상태. 일정 시간 안에 다시 누르지 않으면 해제</summary>
    private bool _deleteArmed;
    private readonly DispatcherTimer _deleteArmTimer = new() { Interval = TimeSpan.FromSeconds(3) };

    public Note? Note => DataContext as Note;

    private static readonly SolidColorBrush SelectedRing = HexToBrushConverter.Brush("#8A3A3A48");

    public NoteTab()
    {
        InitializeComponent();
        BuildColorDots();
        _deleteArmTimer.Tick += (_, _) => DisarmDelete();
        Unloaded += (_, _) => DisarmDelete();
    }

    /// <summary>독립 창 표시 설정. 접힘 없이 본문을 표시하고 고정 해제 버튼으로 전환함</summary>
    public void ConfigureFloating()
    {
        Margin = new Thickness(0);
        Card.CornerRadius = new CornerRadius(16);
        PinButton.Background = HexToBrushConverter.Brush("#80404060");
        Strings.Bind(PinButton, ToolTipProperty, "UnpinTooltip");
        Strings.Bind(PinButton, System.Windows.Automation.AutomationProperties.NameProperty, "UnpinNote");
        SideLabel.Cursor = Cursors.SizeAll;
        Strings.Bind(SideLabel, ToolTipProperty, "MovePinned");
        JumpTo(0);
        SetExpanded(true);
    }

    /// <summary>다른 창에서 바뀐 내용에 맞춰 펼친 카드 높이를 다시 계산함</summary>
    public void RefreshExpandedHeight() => UpdateExpandedHeight();

    /// <summary>고정 창에서 직접 정한 크기를 사용하고 내용에 따른 자동 높이 조절을 중지함</summary>
    public void UseFloatingWindowSize()
    {
        _useWindowSize = true;
        BeginAnimation(HeightProperty, null);
        Width = double.NaN;
        Height = double.NaN;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
    }

    /// <summary>삭제 버튼을 확인 상태로 전환: 빨간 배경에 "삭제" 표시</summary>
    private void ArmDelete()
    {
        _deleteArmed = true;
        DeleteButton.Background = DeleteArmedBrush;
        DeleteButton.Padding = new Thickness(8, 0, 8, 0);
        Strings.Bind(DeleteLabel, TextBlock.TextProperty, "Delete");
        DeleteLabel.FontSize = 11;
        DeleteLabel.FontWeight = FontWeights.SemiBold;
        DeleteLabel.Margin = new Thickness(0, -1, 0, 0);
        _deleteArmTimer.Stop();
        _deleteArmTimer.Start();
    }

    /// <summary>삭제 버튼을 기본 × 상태로 되돌림</summary>
    private void DisarmDelete()
    {
        _deleteArmTimer.Stop();
        if (!_deleteArmed) return;
        _deleteArmed = false;
        DeleteButton.Background = DeleteIdleBrush;
        DeleteButton.Padding = new Thickness(0);
        DeleteLabel.Text = "×";
        DeleteLabel.FontSize = 15;
        DeleteLabel.FontWeight = FontWeights.Normal;
        DeleteLabel.Margin = new Thickness(0, -3, 0, 0);
    }

    /// <summary>팔레트 색상별 선택 점 생성. 클릭 시 노트 색상 즉시 변경</summary>
    private void BuildColorDots()
    {
        foreach (var hex in NotePalette.All)
        {
            var dot = new Ellipse
            {
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 8, 0),
                Fill = HexToBrushConverter.Brush(hex),
                Stroke = Brushes.Transparent,
                StrokeThickness = 2,
                Cursor = Cursors.Hand,
                Tag = hex,
            };
            dot.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (Note is null) return;
                Note.ColorHex = hex;
                RefreshColorSelection();
            };
            ColorRow.Children.Add(dot);
        }
    }

    /// <summary>현재 노트 색상에 해당하는 점에 테두리 표시</summary>
    private void RefreshColorSelection()
    {
        foreach (var child in ColorRow.Children)
        {
            if (child is not Ellipse dot) continue;
            dot.Stroke = (string?)dot.Tag == Note?.ColorHex ? SelectedRing : Brushes.Transparent;
        }
    }

    /// <summary>카드를 지정 X 오프셋으로 슬라이드</summary>
    public void SlideTo(double x, TimeSpan? delay = null)
    {
        var anim = new DoubleAnimation(x, SlideDuration)
        {
            EasingFunction = Ease,
            BeginTime = delay ?? TimeSpan.Zero,
        };
        Translate.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    /// <summary>즉시 지정 X 오프셋으로 이동 (애니메이션 없음)</summary>
    public void JumpTo(double x)
    {
        Translate.BeginAnimation(TranslateTransform.XProperty, null);
        Translate.X = x;
    }

    /// <summary>본문 표시 여부 전환. 높이와 본문 투명도 애니메이션</summary>
    public void SetExpanded(bool expanded)
    {
        if (IsExpanded == expanded) return;
        IsExpanded = expanded;
        if (!expanded)
            DisarmDelete();

        var height = expanded ? MeasureExpandedHeight() : DeckGeometry.CardHeight;
        BeginAnimation(HeightProperty, new DoubleAnimation(height, ExpandDuration) { EasingFunction = Ease });

        var opacity = expanded ? 1.0 : 0.0;
        var opacityAnim = new DoubleAnimation(opacity, ExpandDuration)
        {
            BeginTime = expanded ? TimeSpan.FromMilliseconds(60) : TimeSpan.Zero,
        };
        BodyPanel.BeginAnimation(OpacityProperty, opacityAnim);
    }

    /// <summary>현재 표시 중인 제목/본문(편집 중이면 입력칸)을 카드 폭으로 측정해 필요한 카드 높이 계산</summary>
    private double MeasureExpandedHeight()
    {
        var constraint = new Size(DeckGeometry.BodyTextWidth, double.PositiveInfinity);
        FrameworkElement title = IsEditing ? TitleBox : TitleText;
        FrameworkElement body = IsEditing ? BodyBox : BodyText;
        title.Measure(constraint);
        body.Measure(constraint);

        var content = title.DesiredSize.Height + 6 + body.DesiredSize.Height;
        if (IsEditing)
            content += ColorRow.Margin.Top + 16;

        return DeckGeometry.ExpandedHeightFor(content);
    }

    /// <summary>확장 상태에서 내용 변화에 맞춰 카드 높이 재조정</summary>
    private void UpdateExpandedHeight()
    {
        if (!IsExpanded || _useWindowSize) return;
        var height = MeasureExpandedHeight();
        if (Math.Abs(height - Height) < 0.5) return;
        BeginAnimation(HeightProperty, new DoubleAnimation(height, ExpandDuration) { EasingFunction = Ease });
    }

    private void BodyBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsEditing)
            UpdateExpandedHeight();
    }

    /// <summary>편집 모드 진입. focusTitle이 true면 제목칸, 아니면 본문칸에 커서</summary>
    public void BeginEdit(bool focusTitle)
    {
        if (IsEditing || Note is null) return;
        IsEditing = true;

        TitleBox.Text = Note.Title;
        BodyBox.Text = Note.Body;
        _colorBeforeEdit = Note.ColorHex;
        RefreshColorSelection();
        ShowEditors(true);

        RaiseEvent(new RoutedEventArgs(EditStartedEvent, this));

        var target = focusTitle ? TitleBox : BodyBox;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            if (!IsEditing || !IsVisible) return;
            Keyboard.Focus(target);
            target.CaretIndex = target.Text.Length;
        });
    }

    /// <summary>
    /// 편집 종료. save가 true면 입력 내용을 노트에 반영하고 제목이 비었으면 기본 제목 적용.
    /// false면 입력과 색상 변경을 버리고 편집 전 상태로 되돌림
    /// </summary>
    public void EndEdit(bool save = true)
    {
        if (!IsEditing || Note is null) return;
        IsEditing = false;

        if (save)
        {
            var now = DateTime.Now;
            Note.Title = TitleBox.Text.Trim();
            Note.Body = BodyBox.Text;
            Note.EnsureTitle(now);
            Note.UpdatedAt = now;
        }
        else
        {
            Note.ColorHex = _colorBeforeEdit;
        }

        ShowEditors(false);
        RaiseEvent(new EditEndedEventArgs(EditEndedEvent, this, save));
    }

    private void ShowEditors(bool editing)
    {
        TitleText.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        BodyScroll.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        TitleBox.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        BodyBox.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        ColorRow.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        UpdateExpandedHeight();
    }

    private void Title_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsExpanded || IsEditing) return;
        e.Handled = true;
        BeginEdit(focusTitle: true);
    }

    /// <summary>제목 이외의 카드 영역 클릭은 본문 편집으로 진입</summary>
    private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!IsExpanded || IsEditing) return;
        BeginEdit(focusTitle: false);
    }

    private void Delete_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (!_deleteArmed)
        {
            ArmDelete();
            return;
        }

        DisarmDelete();
        RaiseEvent(new RoutedEventArgs(DeleteRequestedEvent, this));
    }

    private void Complete_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        RaiseEvent(new RoutedEventArgs(ArchiveRequestedEvent, this));
    }

    private void Pin_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        DisarmDelete();
        RaiseEvent(new RoutedEventArgs(PinRequestedEvent, this));
    }

    /// <summary>공통 단축키: Esc 취소, Ctrl+Enter 또는 Ctrl+S 저장. 처리했으면 true</summary>
    private bool HandleEditShortcut(KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            EndEdit(save: false);
            return true;
        }
        if (ctrl && (e.Key == Key.Enter || e.Key == Key.S))
        {
            e.Handled = true;
            EndEdit(save: true);
            return true;
        }
        return false;
    }

    private void TitleBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (HandleEditShortcut(e)) return;

        // 제목칸에서 Enter는 본문칸으로 이동
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Keyboard.Focus(BodyBox);
            BodyBox.CaretIndex = BodyBox.Text.Length;
        }
    }

    private void BodyBox_KeyDown(object sender, KeyEventArgs e) => HandleEditShortcut(e);
}

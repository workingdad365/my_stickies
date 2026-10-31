using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MyStickies.Localization;

namespace MyStickies.Controls;

/// <summary>왼쪽 버튼으로 선택한 텍스트를 버튼을 놓을 때 복사하고 결과를 알림</summary>
public class CopyOnDragTextBox : TextBox
{
    private (int Start, int Length)? _selectionOnPress;
    private ToolTip? _copyNotice;
    private readonly DispatcherTimer _noticeTimer = new() { Interval = TimeSpan.FromSeconds(1.8) };

    public bool IsSelecting => _selectionOnPress.HasValue;
    public event RoutedEventHandler? ReadClicked;

    public CopyOnDragTextBox()
    {
        // TextBox가 처리한 이벤트도 받아 기본 선택 처리가 끝난 뒤 범위를 확인함
        AddHandler(Mouse.MouseDownEvent, new MouseButtonEventHandler(Text_MouseDown), true);
        AddHandler(Mouse.MouseUpEvent, new MouseButtonEventHandler(Text_MouseUp), true);
        _noticeTimer.Tick += (_, _) => HideCopyNotice();
        Unloaded += (_, _) => ResetInteraction();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) ResetInteraction();
        };
    }

    private void Text_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        _selectionOnPress = null;
        for (var source = e.OriginalSource as DependencyObject; source is not null && source != this;
             source = ParentOf(source))
        {
            if (source is ScrollBar) return;
        }

        HideCopyNotice();
        _selectionOnPress = (SelectionStart, SelectionLength);
    }

    private void Text_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _selectionOnPress is not { } initial) return;
        _selectionOnPress = null;
        e.Handled = true;

        if (SelectionLength > 0 && initial != (SelectionStart, SelectionLength))
        {
            try
            {
                WriteClipboardText(SelectedText);
                ShowCopyNotice("SelectionCopied", e.GetPosition(this));
            }
            catch (ExternalException)
            {
                ShowCopyNotice("SelectionCopyFailed", e.GetPosition(this));
            }
        }
        else if (IsReadOnly && SelectionLength == 0)
        {
            ReadClicked?.Invoke(this, e);
        }
    }

    /// <summary>클립보드 경합 시 WPF의 재시도를 사용하고 앱 종료 후에도 문자열을 유지함</summary>
    protected virtual void WriteClipboardText(string text) => Clipboard.SetDataObject(text, true);

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        // 정상적인 버튼 해제에서는 MouseUp까지 선택 상태를 유지함
        if (Mouse.LeftButton == MouseButtonState.Pressed)
            _selectionOnPress = null;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) ResetInteraction();
        base.OnPreviewKeyDown(e);
    }

    private void ShowCopyNotice(string key, Point position)
    {
        _copyNotice ??= new ToolTip
        {
            PlacementTarget = this,
            Placement = PlacementMode.Relative,
            StaysOpen = true,
            IsHitTestVisible = false,
            Focusable = false,
            Background = new SolidColorBrush(Color.FromRgb(35, 35, 43)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 6, 10, 6),
            FontSize = 12,
        };
        Strings.Bind(_copyNotice, ContentControl.ContentProperty, key);
        _copyNotice.HorizontalOffset = Math.Clamp(position.X, 0, ActualWidth);
        _copyNotice.VerticalOffset = Math.Clamp(position.Y, 0, ActualHeight) + 18;
        _copyNotice.IsOpen = IsVisible;
        _noticeTimer.Stop();
        if (_copyNotice.IsOpen) _noticeTimer.Start();
    }

    private void HideCopyNotice()
    {
        _noticeTimer.Stop();
        if (_copyNotice is not null) _copyNotice.IsOpen = false;
    }

    private void ResetInteraction()
    {
        _selectionOnPress = null;
        HideCopyNotice();
    }

    internal static bool IsTextBoxSource(DependencyObject? source)
    {
        for (; source is not null; source = ParentOf(source))
            if (source is CopyOnDragTextBox) return true;
        return false;
    }

    private static DependencyObject? ParentOf(DependencyObject source) =>
        source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
}

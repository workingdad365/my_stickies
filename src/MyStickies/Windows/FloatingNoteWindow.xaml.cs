using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using MyStickies.Controls;
using MyStickies.Interop;
using MyStickies.Layout;
using MyStickies.Models;

namespace MyStickies.Windows;

/// <summary>다른 앱으로 포커스가 이동해도 펼쳐진 상태로 유지되는 고정 메모 창</summary>
public partial class FloatingNoteWindow : Window
{
    private bool _saveOnClose = true;
    private bool _hasCustomSize;
    private bool _resizing;
    private Size _resizeLimit;

    public double? CustomWidth => _hasCustomSize ? Width : null;
    public double? CustomHeight => _hasCustomSize ? Height : null;

    public Note Note => (Note)DataContext;
    public bool IsEditing => NoteView.IsEditing;
    public event EventHandler? UnpinRequested;
    public event EventHandler? ArchiveRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? PlacementChanged;
    public event EventHandler<EditEndedEventArgs>? EditEnded;

    public FloatingNoteWindow(Note note)
    {
        InitializeComponent();
        DataContext = note;
        NoteView.ConfigureFloating();
        NoteView.PinRequested += (_, _) => UnpinRequested?.Invoke(this, EventArgs.Empty);
        NoteView.ArchiveRequested += (_, _) => ArchiveRequested?.Invoke(this, EventArgs.Empty);
        NoteView.DeleteRequested += (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty);
        NoteView.EditStarted += (_, _) => Activate();
        NoteView.EditEnded += (_, e) =>
        {
            if (e is EditEndedEventArgs ended) EditEnded?.Invoke(this, ended);
        };
        SourceInitialized += (_, _) => NativeMethods.MakeToolWindow(new WindowInteropHelper(this).Handle);
        Deactivated += (_, _) => EndEdit();
        Loaded += (_, _) => KeepOnScreen();
        SizeChanged += (_, _) =>
        {
            if (IsLoaded && !_resizing) KeepOnScreen();
        };
    }

    public void EndEdit(bool save = true) => NoteView.EndEdit(save);

    /// <summary>이전 버전의 크기 없는 설정이나 손상된 값은 자동 크기로 유지함</summary>
    public void RestoreSize(double? width, double? height)
    {
        if (width is not { } w || height is not { } h
            || !double.IsFinite(w) || !double.IsFinite(h) || w <= 0 || h <= 0) return;
        SetCustomSize(FloatingNoteGeometry.ClampSize(w, h, CurrentWorkAreaSize()));
    }

    private Size CurrentWorkAreaSize()
    {
        var areas = MonitorInfo.All().OrderByDescending(m => m.IsPrimary).Select(m => m.WorkAreaDip).ToArray();
        var point = new Point(double.IsFinite(Left) ? Left : 0, double.IsFinite(Top) ? Top : 0);
        var area = areas.FirstOrDefault(a => a.Contains(point));
        if (area.IsEmpty || area.Width == 0) area = areas.FirstOrDefault();
        return area.Width > 0 ? area.Size : new Size(1920, 1080);
    }

    private void SetCustomSize(Size size)
    {
        // 자동 높이 애니메이션을 제거한 후 창의 안쪽 영역에 카드를 맞춤
        SizeToContent = SizeToContent.Manual;
        NoteView.UseFloatingWindowSize();
        Width = size.Width;
        Height = size.Height;
        _hasCustomSize = true;
    }

    private void Resize_DragStarted(object sender, DragStartedEventArgs e)
    {
        _resizing = true;
        _resizeLimit = CurrentWorkAreaSize();
        SetCustomSize(new Size(
            ActualWidth > 0 ? ActualWidth : CustomWidth ?? DeckGeometry.CardWidth + 16,
            ActualHeight > 0 ? ActualHeight : CustomHeight ?? DeckGeometry.ExpandedHeight + 16));
        e.Handled = true;
    }

    private void Resize_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!_resizing) return;
        var size = FloatingNoteGeometry.ClampSize(
            Width + (ReferenceEquals(sender, CornerResize) ? e.HorizontalChange : 0),
            Height + e.VerticalChange, _resizeLimit);
        Width = size.Width;
        Height = size.Height;
        e.Handled = true;
    }

    private void Resize_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _resizing = false;
        KeepOnScreen();
        PlacementChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    /// <summary>DB 재로드로 교체된 노트 객체와 관리 창에서 바뀐 내용을 반영함</summary>
    public void RefreshNote(Note note)
    {
        if (!ReferenceEquals(Note, note))
        {
            EndEdit(save: false);
            DataContext = note;
        }
        NoteView.RefreshExpandedHeight();
    }

    /// <summary>삭제되거나 다른 DB로 전환된 메모의 편집 내용을 다시 저장하지 않고 창을 닫음</summary>
    public void CloseWithoutSaving()
    {
        _saveOnClose = false;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        EndEdit(_saveOnClose);
        base.OnClosing(e);
    }

    public void KeepOnScreen()
    {
        var areas = MonitorInfo.All().OrderByDescending(m => m.IsPrimary).Select(m => m.WorkAreaDip).ToArray();
        var point = FloatingNoteGeometry.ClampPosition(Left, Top,
            ActualWidth > 0 ? ActualWidth : DeckGeometry.CardWidth + 16,
            ActualHeight > 0 ? ActualHeight : DeckGeometry.ExpandedHeight + 16, areas);
        if (Left == point.X && Top == point.Y) return;
        Left = point.X;
        Top = point.Y;
        PlacementChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (BottomResize.IsMouseOver || CornerResize.IsMouseOver) return;
        var position = e.GetPosition(NoteView);
        if (position.X < 0 || position.X > DeckGeometry.LabelColumnWidth
            || position.Y < 0 || position.Y > NoteView.ActualHeight) return;

        e.Handled = true;
        DragMove();
        KeepOnScreen();
        PlacementChanged?.Invoke(this, EventArgs.Empty);
    }
}

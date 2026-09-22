using System.ComponentModel;
using System.Windows;
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
            if (IsLoaded) KeepOnScreen();
        };
    }

    public void EndEdit(bool save = true) => NoteView.EndEdit(save);

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
        var position = e.GetPosition(NoteView);
        if (position.X < 0 || position.X > DeckGeometry.LabelColumnWidth
            || position.Y < 0 || position.Y > NoteView.ActualHeight) return;

        e.Handled = true;
        DragMove();
        KeepOnScreen();
        PlacementChanged?.Invoke(this, EventArgs.Empty);
    }
}

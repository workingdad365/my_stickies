using System.IO;
using System.Windows;
using System.Windows.Threading;
using MyStickies.Controls;
using MyStickies.Data;
using MyStickies.Models;
using MyStickies.Windows;

namespace MyStickies;

public partial class MainWindow
{
    private readonly Dictionary<Guid, FloatingNoteWindow> _floatingNotes = [];
    private readonly DispatcherTimer _pinSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private bool _floatingSyncQueued;
    private bool _closingFloatingNotes;
    private bool _shuttingDown;

    private string PinDatabasePath => Path.GetFullPath(_store.DbPath);

    private void InitializeFloatingNotes()
    {
        _pinSaveTimer.Tick += (_, _) => SavePinnedNotes();
        Closing += (_, _) =>
        {
            _shuttingDown = true;
            _editingTab?.EndEdit();
            CloseFloatingNotes();
        };
    }

    /// <summary>편집 내용을 저장한 뒤 선택한 메모만 독립 창으로 옮기고 나머지 덱을 접음</summary>
    private void Tab_PinRequested(object sender, RoutedEventArgs e)
    {
        if (sender is not NoteTab tab || tab.Note is not { } original) return;
        e.Handled = true;
        var point = tab.TranslatePoint(new Point(0, 0), this);
        _editingTab?.EndEdit();
        var note = Notes.FirstOrDefault(n => n.Id == original.Id && !n.IsArchived);
        if (note is null) return;

        _pressedTab = null;
        _collapseTimer.Stop();
        Collapse();
        OpenFloatingNote(note, Left + point.X - 8, Top + point.Y - 8);
        SyncDeckNotes();
        SavePinnedNotes();
    }

    private void OpenFloatingNote(Note note, double left, double top)
    {
        if (_floatingNotes.TryGetValue(note.Id, out var existing))
        {
            existing.Activate();
            return;
        }

        var window = new FloatingNoteWindow(note) { Left = left, Top = top };
        _floatingNotes.Add(note.Id, window);
        window.UnpinRequested += (_, _) => window.Close();
        window.EditEnded += (_, e) =>
        {
            if (e.Saved && Notes.Contains(window.Note) && !window.Note.IsArchived)
                _store.Save(window.Note);
            if (_reloadPending && !_closingFloatingNotes && !_shuttingDown)
                OnExternalChange();
        };
        window.ArchiveRequested += (_, _) =>
        {
            window.EndEdit();
            var current = Notes.FirstOrDefault(n => n.Id == note.Id);
            if (current is not null) _store.Archive(current, DateTime.Now);
            window.CloseWithoutSaving();
        };
        window.DeleteRequested += (_, _) =>
        {
            window.CloseWithoutSaving();
            var current = Notes.FirstOrDefault(n => n.Id == note.Id);
            if (current is not null) _store.Delete(current);
        };
        window.PlacementChanged += (_, _) =>
        {
            if (_closingFloatingNotes || _shuttingDown) return;
            _pinSaveTimer.Stop();
            _pinSaveTimer.Start();
        };
        window.Closed += (_, _) =>
        {
            _floatingNotes.Remove(note.Id);
            if (_closingFloatingNotes || _shuttingDown) return;
            SavePinnedNotes();
            SyncDeckNotes();
        };
        window.Show();
    }

    private void RestorePinnedNotes()
    {
        foreach (var state in PinnedNoteState.Restorable(_settings.PinnedNotes, PinDatabasePath, Notes).ToArray())
        {
            var note = Notes.First(n => n.Id == state.NoteId);
            OpenFloatingNote(note, state.Left, state.Top);
        }
        SyncDeckNotes();
        SavePinnedNotes();
    }

    /// <summary>현재 DB의 고정 상태만 갱신하여 다른 저장 폴더의 창 위치를 보존함</summary>
    private void SavePinnedNotes()
    {
        _pinSaveTimer.Stop();
        if (_closingFloatingNotes) return;
        var path = PinDatabasePath;
        _settings.PinnedNotes.RemoveAll(s => s.IsForDatabase(path));
        _settings.PinnedNotes.AddRange(_floatingNotes.Values.Select(w =>
            new PinnedNoteState(path, w.Note.Id, w.Left, w.Top)));
        _settings.Save(AppSettings.SettingsPath);
    }

    /// <summary>종료와 DB 전환 시 편집 및 위치를 저장하고, 고정 해제 없이 창만 닫음</summary>
    private void CloseFloatingNotes()
    {
        _closingFloatingNotes = true;
        try
        {
            foreach (var window in _floatingNotes.Values.ToArray()) window.EndEdit();
        }
        finally { _closingFloatingNotes = false; }

        SavePinnedNotes();
        _closingFloatingNotes = true;
        try
        {
            foreach (var window in _floatingNotes.Values.ToArray()) window.Close();
        }
        finally { _closingFloatingNotes = false; }
    }

    /// <summary>DB 재로드의 Clear/Add가 끝난 뒤 고정 창을 동기화하여 중간의 빈 목록을 삭제로 오인하지 않음</summary>
    private void QueueFloatingNotesSync()
    {
        if (_floatingSyncQueued || _closingFloatingNotes || _shuttingDown) return;
        _floatingSyncQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _floatingSyncQueued = false;
            if (_closingFloatingNotes || _shuttingDown) return;
            foreach (var window in _floatingNotes.Values.ToArray())
            {
                var note = Notes.FirstOrDefault(n => n.Id == window.Note.Id && !n.IsArchived);
                if (note is null) window.CloseWithoutSaving();
                else window.RefreshNote(note);
            }
        });
    }
}

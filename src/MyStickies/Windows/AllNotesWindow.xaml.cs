using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
using MyStickies.Data;
using MyStickies.Models;

namespace MyStickies.Windows;

/// <summary>메모 관리 창. 검색, 전체/활성/숨김 필터, 미리보기, 숨김/복원/삭제</summary>
public partial class AllNotesWindow : Window
{
    private readonly NoteStore _store;
    private readonly ICollectionView _view;
    private string _filter = "all";

    public AllNotesWindow(NoteStore store)
    {
        _store = store;
        InitializeComponent();

        _view = new CollectionViewSource { Source = store.Notes }.View;
        _view.SortDescriptions.Add(new SortDescription(nameof(Note.UpdatedAt), ListSortDirection.Descending));
        _view.Filter = Matches;
        _view.MoveCurrentToFirst();
        NoteList.ItemsSource = _view;

        _store.NoteChanged += OnNoteChanged;
        _store.Notes.CollectionChanged += OnNotesCollectionChanged;
        Closed += (_, _) =>
        {
            _store.NoteChanged -= OnNoteChanged;
            _store.Notes.CollectionChanged -= OnNotesCollectionChanged;
        };

        UpdateCount();
    }

    private bool Matches(object item)
    {
        if (item is not Note note) return false;
        if (_filter == "active" && note.IsArchived) return false;
        if (_filter == "archived" && !note.IsArchived) return false;

        var query = SearchBox.Text.Trim();
        return query.Length == 0
               || note.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
               || note.Body.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void Refresh()
    {
        var selected = NoteList.SelectedItem;
        _view.Refresh();
        if (selected is not null && _view.Contains(selected))
            NoteList.SelectedItem = selected;
        UpdateCount();
    }

    private void UpdateCount() => CountText.Text = $"{_view.Cast<object>().Count()}개";

    private void OnNoteChanged(Note note) => Dispatcher.BeginInvoke(Refresh);

    private void OnNotesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(UpdateCount);

    private void Filter_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string tag })
            _filter = tag;
        if (IsLoaded)
            Refresh();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Refresh();

    private void NewNote_Click(object sender, RoutedEventArgs e)
    {
        var note = _store.Add(DateTime.Now);
        Refresh();
        NoteList.SelectedItem = note;
    }

    private void Archive_Click(object sender, RoutedEventArgs e)
    {
        if (NoteList.SelectedItem is Note note)
            _store.Archive(note, DateTime.Now);
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (NoteList.SelectedItem is Note note)
            _store.Restore(note, DateTime.Now);
    }

    private const string ExportFilter = "Markdown (*.md)|*.md|텍스트 (*.txt)|*.txt";
    private const string ImportFilter = "메모 파일 (*.md;*.txt)|*.md;*.txt|모든 파일 (*.*)|*.*";

    /// <summary>파일 이름에 쓸 수 없는 문자를 제거한 제목</summary>
    private static string SafeFileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = new string(title.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return name.Length == 0 ? "메모" : name;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (NoteList.SelectedItem is not Note note) return;

        var dialog = new SaveFileDialog
        {
            Title = "메모 내보내기",
            Filter = ExportFilter,
            FileName = SafeFileName(note.Title),
            DefaultExt = ".md",
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != true) return;

        var content = dialog.FilterIndex == 2 ? NoteExporter.ToPlainText(note) : NoteExporter.ToMarkdown(note);
        WriteFile(dialog.FileName, content);
    }

    private void ExportAll_Click(object sender, RoutedEventArgs e)
    {
        if (_store.Notes.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Title = "전체 메모 내보내기",
            Filter = "Markdown (*.md)|*.md",
            FileName = $"MyStickies-{DateTime.Now:yyyyMMdd-HHmm}",
            DefaultExt = ".md",
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != true) return;

        WriteFile(dialog.FileName, NoteExporter.ToMarkdown(_store.Notes.OrderBy(n => n.CreatedAt)));
    }

    private void WriteFile(string path, string content)
    {
        try
        {
            File.WriteAllText(path, content, System.Text.Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"파일을 저장하지 못했습니다.\n{ex.Message}", "내보내기",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>.md/.txt 파일을 골라 각각 새 메모로 추가. 첫 줄 "# 제목"이 있으면 제목, 없으면 파일 이름</summary>
    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "메모 가져오기",
            Filter = ImportFilter,
            Multiselect = true,
        };
        if (dialog.ShowDialog(this) != true) return;

        Note? last = null;
        var failed = new List<string>();
        foreach (var file in dialog.FileNames)
        {
            try
            {
                var (title, body) = NoteExporter.Parse(File.ReadAllText(file), file);
                last = _store.Add(DateTime.Now, title, body);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed.Add(Path.GetFileName(file));
            }
        }

        Refresh();
        if (last is not null)
            NoteList.SelectedItem = last;
        if (failed.Count > 0)
            MessageBox.Show(this, "읽지 못한 파일: " + string.Join(", ", failed), "가져오기",
                MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (NoteList.SelectedItem is not Note note) return;

        var answer = MessageBox.Show(this, $"\"{note.Title}\" 메모를 영구 삭제할까요?", "삭제 확인",
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        _store.Delete(note);
        UpdateCount();
    }
}

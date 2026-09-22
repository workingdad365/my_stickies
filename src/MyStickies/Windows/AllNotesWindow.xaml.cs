using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Ellipse = System.Windows.Shapes.Ellipse;
using System.Windows.Threading;
using Microsoft.Win32;
using MyStickies.Converters;
using MyStickies.Data;
using MyStickies.Models;
using MyStickies.Localization;

namespace MyStickies.Windows;

/// <summary>메모 관리 창. 검색, 전체/활성/숨김 필터, 상세 보기와 편집, 색상 변경, 숨김/복원/삭제</summary>
public partial class AllNotesWindow : Window
{
    private static readonly SolidColorBrush SelectedRing = HexToBrushConverter.Brush("#8A3A3A48");

    /// <summary>선택되지 않은 색상 점의 테두리. 메모지와 같은 색인 점도 보이도록 옅은 흰색</summary>
    private static readonly SolidColorBrush IdleRing = HexToBrushConverter.Brush("#B0FFFFFF");

    private readonly NoteStore _store;
    private readonly ICollectionView _view;
    private string _filter = "all";

    /// <summary>상세 카드에서 편집 중인 노트. 선택이 바뀌거나 창이 닫히면 저장하고 종료</summary>
    private Note? _editing;
    private bool _refreshing;

    public AllNotesWindow(NoteStore store)
    {
        _store = store;
        InitializeComponent();
        Title = Strings.Get("ManageTitle", AppInfo.Name, AppInfo.Version);

        _view = new CollectionViewSource { Source = store.Notes }.View;
        _view.SortDescriptions.Add(new SortDescription(nameof(Note.UpdatedAt), ListSortDirection.Descending));
        _view.Filter = Matches;
        _view.MoveCurrentToFirst();
        NoteList.ItemsSource = _view;

        _store.NoteChanged += OnNoteChanged;
        Strings.LanguageChanged += RefreshLanguage;
        _store.Notes.CollectionChanged += OnNotesCollectionChanged;
        Closed += (_, _) =>
        {
            EndDetailEdit(save: true);
            Strings.LanguageChanged -= RefreshLanguage;
            _store.NoteChanged -= OnNoteChanged;
            _store.Notes.CollectionChanged -= OnNotesCollectionChanged;
        };

        BuildColorDots();
        NoteList.SelectionChanged += (_, _) =>
        {
            // 목록 재정렬 중 일시적인 선택 변경은 편집을 끊지 않음
            if (!_refreshing) EndDetailEdit(save: true);
            RefreshColorSelection();
        };

        UpdateCount();
    }

    /// <summary>팔레트 색상별 선택 점 생성. 클릭 시 색상을 바꾸고 바로 저장</summary>
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
                Stroke = IdleRing,
                StrokeThickness = 2,
                Cursor = Cursors.Hand,
                Tag = hex,
            };
            dot.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (NoteList.SelectedItem is not Note note || note.ColorHex == hex) return;
                note.ColorHex = hex;
                note.UpdatedAt = DateTime.Now;
                _store.Save(note);
                RefreshColorSelection();
            };
            DetailColorRow.Children.Add(dot);
        }
    }

    /// <summary>선택한 노트의 색상에 해당하는 점에 테두리 표시</summary>
    private void RefreshColorSelection()
    {
        var current = (NoteList.SelectedItem as Note)?.ColorHex;
        foreach (var child in DetailColorRow.Children)
        {
            if (child is not Ellipse dot) continue;
            dot.Stroke = (string?)dot.Tag == current ? SelectedRing : IdleRing;
        }
    }

    /// <summary>상세 카드 편집 시작. focusTitle이 true면 제목칸, 아니면 본문칸에 커서</summary>
    private void BeginDetailEdit(bool focusTitle)
    {
        if (_editing is not null || NoteList.SelectedItem is not Note note) return;
        _editing = note;

        DetailTitleBox.Text = note.Title;
        DetailBodyBox.Text = note.Body;
        ShowDetailEditors(true);

        var target = focusTitle ? DetailTitleBox : DetailBodyBox;
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            Keyboard.Focus(target);
            target.CaretIndex = target.Text.Length;
        });
    }

    /// <summary>상세 카드 편집 종료. save가 true면 입력 내용을 저장하고, 제목이 비었으면 기본 제목 적용</summary>
    private void EndDetailEdit(bool save)
    {
        if (_editing is null) return;
        var note = _editing;
        _editing = null;

        if (save)
        {
            var now = DateTime.Now;
            note.Title = DetailTitleBox.Text.Trim();
            note.Body = DetailBodyBox.Text;
            note.EnsureTitle(now);
            note.UpdatedAt = now;
            _store.Save(note);
        }

        ShowDetailEditors(false);
    }

    private void ShowDetailEditors(bool editing)
    {
        DetailTitle.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        DetailBodyScroll.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        DetailTitleBox.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        DetailBodyBox.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        DetailTimes.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        DetailEditActions.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DetailTitle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        BeginDetailEdit(focusTitle: true);
    }

    private void DetailBody_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_editing is not null) return;
        e.Handled = true;
        BeginDetailEdit(focusTitle: false);
    }

    private void DetailSave_Click(object sender, RoutedEventArgs e) => EndDetailEdit(save: true);

    private void DetailCancel_Click(object sender, RoutedEventArgs e) => EndDetailEdit(save: false);

    /// <summary>공통 단축키: Esc 취소, Ctrl+Enter 또는 Ctrl+S 저장. 처리했으면 true</summary>
    private bool HandleEditShortcut(KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            EndDetailEdit(save: false);
            return true;
        }
        if (ctrl && (e.Key == Key.Enter || e.Key == Key.S))
        {
            e.Handled = true;
            EndDetailEdit(save: true);
            return true;
        }
        return false;
    }

    private void DetailTitleBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (HandleEditShortcut(e)) return;

        // 제목칸에서 Enter는 본문칸으로 이동
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Keyboard.Focus(DetailBodyBox);
            DetailBodyBox.CaretIndex = DetailBodyBox.Text.Length;
        }
    }

    private void DetailBodyBox_KeyDown(object sender, KeyEventArgs e) => HandleEditShortcut(e);

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
        _refreshing = true;
        try
        {
            _view.Refresh();
            if (selected is not null && _view.Contains(selected))
                NoteList.SelectedItem = selected;
        }
        finally
        {
            _refreshing = false;
        }
        UpdateCount();
    }

    private void UpdateCount() => CountText.Text = Strings.Get("NotesCount", _view.Cast<object>().Count());

    private void RefreshLanguage()
    {
        Title = Strings.Get("ManageTitle", AppInfo.Name, AppInfo.Version);
        UpdateCount();
    }

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

    private static string ExportFilter => Strings.Get("ExportFilter");
    private static string ImportFilter => Strings.Get("ImportFilter");

    /// <summary>파일 이름에 쓸 수 없는 문자를 제거한 제목</summary>
    private static string SafeFileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = new string(title.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return name.Length == 0 ? Strings.Get("NoteFileName") : name;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (NoteList.SelectedItem is not Note note) return;

        var dialog = new SaveFileDialog
        {
            Title = Strings.Get("ExportNote"),
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
            Title = Strings.Get("ExportAll"),
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
            MessageBox.Show(this, Strings.Get("ExportFailed", ex.Message), Strings.Get("Export"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>.md/.txt 파일을 골라 각각 새 메모로 추가. 첫 줄 "# 제목"이 있으면 제목, 없으면 파일 이름</summary>
    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.Get("ImportNotes"),
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
            MessageBox.Show(this, Strings.Get("ImportFailed", string.Join(", ", failed)), Strings.Get("Import"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (NoteList.SelectedItem is not Note note) return;

        var answer = MessageBox.Show(this, Strings.Get("DeletePrompt", note.Title), Strings.Get("ConfirmDelete"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        if (_editing == note)
            EndDetailEdit(save: false);
        _store.Delete(note);
        UpdateCount();
    }
}

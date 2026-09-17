using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using MyStickies.Controls;
using MyStickies.Data;
using MyStickies.Interop;
using MyStickies.Layout;
using MyStickies.Models;
using MyStickies.Tray;
using MyStickies.Windows;

namespace MyStickies;

/// <summary>
/// 화면 우측 가장자리에 도킹되는 투명 창.
/// 휴면(책갈피) -> 팬아웃(탭 목록) -> 확장(노트 본문) -> 편집 상태 전환 담당.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly TimeSpan StaggerStep = TimeSpan.FromMilliseconds(30);
    private static readonly TimeSpan BookmarkFade = TimeSpan.FromMilliseconds(150);
    private const double PlusHiddenOffset = 80;

    private readonly DispatcherTimer _collapseTimer = new();
    private readonly DispatcherTimer _fullscreenTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _hiddenForFullscreen;
    private GlobalHotkeys? _hotkeys;

    /// <summary>해상도/배율/작업 표시줄 변경은 연속으로 여러 번 오므로 잠시 모아서 한 번만 재배치</summary>
    private readonly DispatcherTimer _relayoutTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

    // 드래그 재정렬 상태
    private const double DragThreshold = 8;
    private NoteTab? _pressedTab;
    private Point _pressPoint;
    private NoteTab? _draggingTab;
    private readonly AppSettings _settings;
    private readonly NoteStore _store;
    private bool _reloadPending;
    private TrayIcon? _tray;
    private AllNotesWindow? _allNotesWindow;
    private bool _fanned;
    private NoteTab? _expandedTab;
    private NoteTab? _editingTab;

    /// <summary>전체 노트 (보관된 노트 포함)</summary>
    public ObservableCollection<Note> Notes => _store.Notes;

    /// <summary>덱에 표시되는 노트. 활성 노트 중 최근 MaxDeckNotes 개만 유지</summary>
    public ObservableCollection<Note> DeckNotes { get; } = [];

    public MainWindow()
    {
        _settings = LoadOrAskSettings();
        ApplyDeckSettings();
        FontSettings.Apply(Application.Current.Resources, _settings.FontFamily, _settings.FontSize);
        _store = new NoteStore(new NoteRepository(NoteRepository.PathFor(_settings.DataDirectory)));

        InitializeComponent();
        DataContext = this;

        SyncDeckNotes();
        Notes.CollectionChanged += (_, _) => SyncDeckNotes();
        _store.NoteChanged += _ => SyncDeckNotes();
        _store.ExternalChangeDetected += () => Dispatcher.BeginInvoke(OnExternalChange);

        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            Collapse();
        };

        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            NativeMethods.MakeToolWindow(hwnd);
            InitHotkeys(HwndSource.FromHwnd(hwnd));
        };

        _fullscreenTimer.Tick += (_, _) => CheckFullscreen();
        _fullscreenTimer.Start();

        Loaded += (_, _) =>
        {
            PlaceWindow();
            InitTray();
            // 시작 인자 --all-notes: 메모 관리 창을 바로 염 (바로 가기, 검증용)
            if (Environment.GetCommandLineArgs().Contains("--all-notes"))
                ShowAllNotes();
            // 시작 인자 --settings: 설정 창을 바로 염 (검증용)
            if (Environment.GetCommandLineArgs().Contains("--settings"))
                ShowSettings();
        };
        _relayoutTimer.Tick += (_, _) =>
        {
            _relayoutTimer.Stop();
            PlaceWindow();
        };
        // 해상도/모니터 구성 변경, 작업 표시줄 위치·크기 변경(작업 영역), 모니터 배율(DPI) 변경 모두 재배치
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        DpiChanged += (_, _) => RequestRelayout();
        Closed += (_, _) =>
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _tray?.Dispose();
            _allNotesWindow?.Close();
            _store.Dispose();
            _fullscreenTimer.Stop();
            _hotkeys?.Dispose();
        };
    }

    /// <summary>전역 단축키 준비. 설정이 켜져 있으면 바로 등록</summary>
    private void InitHotkeys(HwndSource? source)
    {
        if (source is null) return;

        _hotkeys = new GlobalHotkeys(source);
        _hotkeys.ToggleDeck += () =>
        {
            if (_hiddenForFullscreen) return;
            if (_fanned) Collapse();
            else FanOut();
        };
        _hotkeys.NewNote += () =>
        {
            if (_hiddenForFullscreen) return;
            AddNoteAndEdit();
        };
        ApplyHotkeySetting();
    }

    private void ApplyHotkeySetting()
    {
        if (_hotkeys is null) return;
        if (_settings.GlobalHotkeys) _hotkeys.Register();
        else _hotkeys.Unregister();
    }

    /// <summary>새 메모를 만들고 덱을 펼친 뒤 그 메모의 편집 모드로 진입</summary>
    private void AddNoteAndEdit()
    {
        _editingTab?.EndEdit();
        var note = _store.Add(DateTime.Now);
        if (!_fanned) FanOut();

        // 새 탭 컨테이너가 생성되고 팬아웃 위치로 옮겨진 뒤 확장과 편집 시작
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            var tab = Tabs().FirstOrDefault(t => t.Note == note);
            if (tab is null) return;
            _collapseTimer.Stop();
            ExpandTab(tab);
            tab.BeginEdit(focusTitle: true);
        });
    }

    /// <summary>전체화면 앱이 같은 모니터 맨 앞에 있으면 덱 창을 숨기고, 끝나면 다시 표시</summary>
    private void CheckFullscreen()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        var fullscreen = _settings.HideOnFullscreen && _editingTab is null
                         && FullscreenDetector.IsForegroundFullscreenOnSameMonitor(hwnd);

        if (fullscreen && !_hiddenForFullscreen)
        {
            _hiddenForFullscreen = true;
            _collapseTimer.Stop();
            if (_fanned) Collapse();
            Hide();
        }
        else if (!fullscreen && _hiddenForFullscreen)
        {
            _hiddenForFullscreen = false;
            Show();
        }
    }

    /// <summary>설정 파일을 읽고, 없으면(최초 실행) 저장 폴더를 물어본 뒤 저장</summary>
    private static AppSettings LoadOrAskSettings()
    {
        var settings = AppSettings.Load(AppSettings.SettingsPath);
        if (settings is not null) return settings;

        settings = new AppSettings();
        var dialog = new DataLocationWindow(settings, firstRun: true);
        if (dialog.ShowDialog() == true)
            settings.DataDirectory = dialog.SelectedDirectory;
        settings.Save(AppSettings.SettingsPath);
        return settings;
    }

    /// <summary>덱 표시 개수, 시작 위치, 접힘 지연을 설정값으로 적용</summary>
    private void ApplyDeckSettings()
    {
        DeckGeometry.Configure(_settings.DeckMaxNotes, _settings.DeckTopPercent / 100.0);
        _collapseTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(_settings.CollapseDelayMs, 100, 3000));
    }

    /// <summary>설정 창: 자동 실행 여부와 메모 저장 폴더 변경. 새 폴더에 파일이 없으면 현재 메모 복사 여부를 물어봄</summary>
    private void ShowSettings()
    {
        var dialog = new DataLocationWindow(_settings, firstRun: false);
        if (dialog.ShowDialog() != true) return;

        // 자동 실행: 현재 실행 파일 경로를 HKCU Run 키에 등록/해제
        if (dialog.AutoStart != StartupRegistration.IsEnabled())
            StartupRegistration.SetEnabled(dialog.AutoStart);

        // 도킹 모니터와 덱 설정값: 즉시 재배치
        var layoutChanged =
            !string.Equals(dialog.SelectedMonitor, _settings.DockMonitor, StringComparison.OrdinalIgnoreCase)
            || dialog.DeckMaxNotes != _settings.DeckMaxNotes
            || dialog.DeckTopPercent != _settings.DeckTopPercent
            || dialog.CollapseDelayMs != _settings.CollapseDelayMs;
        if (layoutChanged)
        {
            _settings.DockMonitor = dialog.SelectedMonitor;
            _settings.DeckMaxNotes = dialog.DeckMaxNotes;
            _settings.DeckTopPercent = dialog.DeckTopPercent;
            _settings.CollapseDelayMs = dialog.CollapseDelayMs;
            _settings.Save(AppSettings.SettingsPath);
            ApplyDeckSettings();
            SyncDeckNotes();
            PlaceWindow();
        }

        if (dialog.HideOnFullscreen != _settings.HideOnFullscreen)
        {
            _settings.HideOnFullscreen = dialog.HideOnFullscreen;
            _settings.Save(AppSettings.SettingsPath);
            CheckFullscreen();
        }

        if (!string.Equals(dialog.FontFamilyName, _settings.FontFamily, StringComparison.OrdinalIgnoreCase)
            || dialog.NoteFontSize != _settings.FontSize)
        {
            _settings.FontFamily = dialog.FontFamilyName;
            _settings.FontSize = FontSettings.ClampSize(dialog.NoteFontSize);
            _settings.Save(AppSettings.SettingsPath);
            FontSettings.Apply(Application.Current.Resources, _settings.FontFamily, _settings.FontSize);
        }

        if (dialog.GlobalHotkeys != _settings.GlobalHotkeys)
        {
            _settings.GlobalHotkeys = dialog.GlobalHotkeys;
            _settings.Save(AppSettings.SettingsPath);
            ApplyHotkeySetting();
            if (_hotkeys is { Failed.Count: > 0 })
                MessageBox.Show($"다른 프로그램이 이미 사용 중인 단축키가 있어 등록하지 못했습니다: {string.Join(", ", _hotkeys.Failed)}",
                    "전역 단축키", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        var newDir = dialog.SelectedDirectory;
        if (string.Equals(Path.GetFullPath(newDir), Path.GetFullPath(_settings.DataDirectory), StringComparison.OrdinalIgnoreCase))
            return;

        _editingTab?.EndEdit();

        var newDb = NoteRepository.PathFor(newDir);
        if (!File.Exists(newDb) && Notes.Count > 0)
        {
            var answer = MessageBox.Show(
                "새 폴더에 메모 파일이 없습니다. 현재 메모를 새 위치로 복사할까요?\n\n" +
                "'아니요'를 누르면 안내 메모만 들어 있는 새 파일을 만듭니다.",
                "메모 저장 위치 변경", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel) return;
            if (answer == MessageBoxResult.Yes)
            {
                Directory.CreateDirectory(newDir);
                File.Copy(_store.DbPath, newDb);
            }
        }

        _store.SwitchTo(new NoteRepository(newDb));
        _settings.DataDirectory = newDir;
        _settings.Save(AppSettings.SettingsPath);
    }

    /// <summary>다른 PC 등 외부에서 DB 파일이 바뀜. 편집 중이면 편집이 끝난 뒤 다시 읽음</summary>
    private void OnExternalChange()
    {
        if (_editingTab is not null)
        {
            _reloadPending = true;
            return;
        }
        _reloadPending = false;
        _store.Reload();
    }

    /// <summary>메모 관리 창 열기. 이미 열려 있으면 앞으로 가져옴</summary>
    private void ShowAllNotes()
    {
        if (_allNotesWindow is null)
        {
            _allNotesWindow = new AllNotesWindow(_store);
            _allNotesWindow.Closed += (_, _) => _allNotesWindow = null;
            _allNotesWindow.Show();
        }
        else
        {
            if (_allNotesWindow.WindowState == WindowState.Minimized)
                _allNotesWindow.WindowState = WindowState.Normal;
            _allNotesWindow.Activate();
        }
    }

    /// <summary>트레이 아이콘 생성. 우클릭 메뉴로 새 메모 추가와 종료, 좌클릭으로 덱 펼치기</summary>
    private void InitTray()
    {
        var res = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
        if (res is null) return;

        _tray = new TrayIcon(res.Stream, $"My Stickies {AppInfo.Version}");
        _tray.AddNoteRequested += () =>
        {
            AddNote();
            if (!_fanned) FanOut();
        };
        _tray.AllNotesRequested += ShowAllNotes;
        _tray.SettingsRequested += ShowSettings;
        _tray.ExitRequested += () => Application.Current.Shutdown();
        _tray.Clicked += () =>
        {
            if (_fanned) Collapse();
            else FanOut();
        };
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(RequestRelayout);

    /// <summary>작업 표시줄 이동/자동 숨김 등 작업 영역 변경은 Desktop/General 범주로 통지됨</summary>
    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is UserPreferenceCategory.Desktop or UserPreferenceCategory.General)
            Dispatcher.BeginInvoke(RequestRelayout);
    }

    private void RequestRelayout()
    {
        _relayoutTimer.Stop();
        _relayoutTimer.Start();
    }

    /// <summary>설정된 모니터(기본: 주 모니터) 작업 영역의 우측 가장자리에 창 배치</summary>
    private void PlaceWindow()
    {
        var wa = MonitorInfo.Resolve(_settings.DockMonitor).WorkAreaDip;
        var (left, top, width, height) = DeckGeometry.WindowRect(wa.Left, wa.Top, wa.Width, wa.Height);
        Left = left;
        Top = top;
        Width = width;
        Height = height;

        // 노트 수가 아니라 최대 수용 개수 기준으로 계산하므로 덱과 추가 버튼 위치가 흔들리지 않음
        var deckTop = DeckGeometry.DeckTop(wa.Height, DeckGeometry.DeckBlockHeight);
        Deck.Margin = new Thickness(0, deckTop, 0, 0);
        Bookmark.Margin = new Thickness(0, deckTop, 0, 0);
        PlusButton.Margin = new Thickness(0, DeckGeometry.PlusTop(deckTop), 12, 0);

        HoverZone.Width = DeckGeometry.HoverZoneWidth;
        HoverZone.Height = DeckGeometry.DeckBlockHeight;
        HoverZone.Margin = new Thickness(0, deckTop, 0, 0);
    }

    /// <summary>활성 노트 중 최근 MaxDeckNotes 개만 덱에 남김. 기존 탭은 재생성하지 않고 차이만 반영</summary>
    private void SyncDeckNotes()
    {
        var target = Notes.Where(n => !n.IsArchived).TakeLast(DeckGeometry.MaxDeckNotes).ToList();

        for (var i = DeckNotes.Count - 1; i >= 0; i--)
        {
            if (!target.Contains(DeckNotes[i]))
            {
                if (_expandedTab?.DataContext == DeckNotes[i])
                    _expandedTab = null;
                if (_editingTab?.DataContext == DeckNotes[i])
                    _editingTab = null;
                DeckNotes.RemoveAt(i);
            }
        }

        // 전체 목록 순서대로 빠진 노트를 제자리에 끼워 넣어 실행 중과 재시작 후의 순서가 같도록 유지
        var inserted = false;
        for (var i = 0; i < target.Count; i++)
        {
            if (DeckNotes.Contains(target[i])) continue;
            DeckNotes.Insert(Math.Min(i, DeckNotes.Count), target[i]);
            inserted = true;
        }

        // 이미 있던 노트의 상대 순서도 전체 목록과 일치시킴 (재정렬 반영)
        for (var i = 0; i < target.Count; i++)
        {
            var current = DeckNotes.IndexOf(target[i]);
            if (current != i && current >= 0)
                DeckNotes.Move(current, i);
        }

        if (inserted)
            RevealNewTabs();
    }

    /// <summary>새로 생성된 탭 컨테이너는 휴면 위치에서 시작하므로, 팬아웃 중이면 팬아웃 위치로 즉시 이동</summary>
    private void RevealNewTabs()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (!_fanned) return;
            foreach (var tab in Tabs())
                if (Math.Abs(tab.RenderTransform.Value.OffsetX - DeckGeometry.DormantOffset) < 0.5)
                    tab.JumpTo(DeckGeometry.FannedOffset);
        });
    }

    private IEnumerable<NoteTab> Tabs()
    {
        for (var i = 0; i < TabList.Items.Count; i++)
        {
            if (TabList.ItemContainerGenerator.ContainerFromIndex(i) is not ContentPresenter presenter)
                continue;
            if (VisualTreeHelper.GetChildrenCount(presenter) == 0)
                continue;
            if (VisualTreeHelper.GetChild(presenter, 0) is NoteTab tab)
                yield return tab;
        }
    }

    private static DoubleAnimation Slide(double to, int ms, EasingMode mode, TimeSpan? delay = null) =>
        new(to, TimeSpan.FromMilliseconds(ms))
        {
            EasingFunction = new CubicEase { EasingMode = mode },
            BeginTime = delay ?? TimeSpan.Zero,
        };

    /// <summary>휴면 -> 팬아웃. 탭이 순차적으로 슬라이드되어 나옴</summary>
    private void FanOut()
    {
        _fanned = true;

        HoverZone.IsHitTestVisible = true;
        Bookmark.IsHitTestVisible = false;
        Bookmark.BeginAnimation(OpacityProperty, new DoubleAnimation(0, BookmarkFade));

        var index = 0;
        foreach (var tab in Tabs())
        {
            tab.SlideTo(DeckGeometry.FannedOffset, StaggerStep * index);
            index++;
        }

        PlusTranslate.BeginAnimation(TranslateTransform.XProperty,
            Slide(0, 260, EasingMode.EaseOut, StaggerStep * index));
    }

    /// <summary>팬아웃 -> 휴면. 전부 화면 밖으로 밀어내고 책갈피 표시. 편집 중에는 동작하지 않음</summary>
    private void Collapse()
    {
        if (_editingTab is not null) return;

        _fanned = false;
        HoverZone.IsHitTestVisible = false;

        if (_expandedTab is not null)
        {
            _expandedTab.SetExpanded(false);
            _expandedTab = null;
        }

        foreach (var tab in Tabs())
            tab.SlideTo(DeckGeometry.DormantOffset);

        PlusTranslate.BeginAnimation(TranslateTransform.XProperty,
            Slide(PlusHiddenOffset, 200, EasingMode.EaseIn));

        Bookmark.IsHitTestVisible = true;
        Bookmark.BeginAnimation(OpacityProperty,
            new DoubleAnimation(1, BookmarkFade) { BeginTime = TimeSpan.FromMilliseconds(180) });
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _collapseTimer.Stop();
        if (!_fanned)
            FanOut();
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_fanned && _editingTab is null && _draggingTab is null)
            _collapseTimer.Start();
    }

    /// <summary>누른 채 일정 거리 이상 움직이면 드래그 재정렬 시작, 드래그 중에는 커서 위치에 따라 순서 교체</summary>
    private void Window_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _pressedTab = null;
            return;
        }

        if (_draggingTab is null)
        {
            if (_pressedTab is null || _pressedTab.IsEditing) return;
            var delta = e.GetPosition(this) - _pressPoint;
            if (Math.Abs(delta.Y) < DragThreshold) return;
            BeginDrag(_pressedTab);
        }

        UpdateDrag(e.GetPosition(Deck).Y);
    }

    private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _pressedTab = null;
        if (_draggingTab is null) return;
        EndDrag();
        e.Handled = true;
    }

    private void BeginDrag(NoteTab tab)
    {
        _draggingTab = tab;
        _collapseTimer.Stop();

        // 드래그 중에는 모든 카드를 같은 높이로 두어 자리 계산이 단순하도록 확장 해제
        if (_expandedTab is not null)
        {
            _expandedTab.SetExpanded(false);
            _expandedTab.SlideTo(DeckGeometry.FannedOffset);
            _expandedTab = null;
        }
        tab.Opacity = 0.75;
        CaptureMouse();
    }

    /// <summary>커서 Y(덱 기준)가 다른 카드의 중앙을 넘으면 그 자리로 이동</summary>
    private void UpdateDrag(double cursorY)
    {
        if (_draggingTab?.Note is not { } note) return;
        var tabs = Tabs().ToList();
        var from = tabs.IndexOf(_draggingTab);
        if (from < 0) return;

        var to = from;
        for (var i = 0; i < tabs.Count; i++)
        {
            if (i == from) continue;
            var top = tabs[i].TranslatePoint(new Point(0, 0), Deck).Y;
            var mid = top + tabs[i].ActualHeight / 2;
            if (i < from && cursorY < mid) { to = Math.Min(to, i); }
            if (i > from && cursorY > mid) { to = Math.Max(to, i); }
        }
        if (to == from) return;

        var deckIndex = DeckNotes.IndexOf(note);
        var targetIndex = DeckNotes.IndexOf(tabs[to].Note!);
        if (deckIndex >= 0 && targetIndex >= 0)
            DeckNotes.Move(deckIndex, targetIndex);
    }

    private void EndDrag()
    {
        var tab = _draggingTab;
        _draggingTab = null;
        ReleaseMouseCapture();
        if (tab is null) return;

        tab.Opacity = 1;
        _store.Reorder(DeckNotes.ToList());
        if (!IsMouseOver)
            _collapseTimer.Start();
    }

    /// <summary>다른 창으로 포커스가 넘어가면 편집 종료</summary>
    private void Window_Deactivated(object sender, EventArgs e) => _editingTab?.EndEdit();

    private void Tab_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not NoteTab tab || !_fanned || _editingTab is not null || _draggingTab is not null) return;
        ExpandTab(tab);
    }

    /// <summary>지정 탭을 확장하고 이전에 확장된 탭은 팬아웃 상태로 되돌림</summary>
    private void ExpandTab(NoteTab tab)
    {
        if (_expandedTab is not null && _expandedTab != tab)
        {
            _expandedTab.SetExpanded(false);
            _expandedTab.SlideTo(DeckGeometry.FannedOffset);
        }

        _expandedTab = tab;
        tab.SlideTo(DeckGeometry.ExpandedOffset);
        tab.SetExpanded(true);
    }

    /// <summary>
    /// 편집 중 다른 스티커를 클릭하면 기존 편집을 종료하고 그 스티커를 확장.
    /// 이어지는 MouseLeftButtonUp에서 해당 스티커가 편집 모드로 진입
    /// </summary>
    private void Tab_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not NoteTab tab) return;

        // 드래그 재정렬 후보 기록 (편집 중인 카드는 텍스트 선택을 위해 제외)
        _pressedTab = tab.IsEditing ? null : tab;
        _pressPoint = e.GetPosition(this);

        if (_editingTab is null || _editingTab == tab) return;

        _editingTab.EndEdit();
        ExpandTab(tab);
    }

    private void Tab_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is not NoteTab tab || _expandedTab != tab || _editingTab == tab || _draggingTab is not null) return;

        _expandedTab = null;
        tab.SetExpanded(false);
        if (_fanned)
            tab.SlideTo(DeckGeometry.FannedOffset);
    }

    /// <summary>편집 시작: 접힘을 막고 키보드 입력을 받도록 창 활성화</summary>
    private void Tab_EditStarted(object sender, RoutedEventArgs e)
    {
        if (sender is not NoteTab tab) return;

        _collapseTimer.Stop();
        _editingTab = tab;
        Activate();
    }

    /// <summary>편집 종료: 저장한 경우에만 DB 반영 후, 마우스 위치에 따라 확장/팬아웃/휴면 상태로 복귀</summary>
    private void Tab_EditEnded(object sender, RoutedEventArgs e)
    {
        if (sender is not NoteTab tab) return;

        if (tab.Note is not null && e is EditEndedEventArgs { Saved: true })
            _store.Save(tab.Note);

        if (_editingTab == tab)
            _editingTab = null;

        if (!tab.IsMouseOver)
        {
            if (_expandedTab == tab)
                _expandedTab = null;
            tab.SetExpanded(false);
            if (_fanned)
                tab.SlideTo(DeckGeometry.FannedOffset);
        }

        if (!IsMouseOver)
            _collapseTimer.Start();

        if (_reloadPending)
            OnExternalChange();
    }

    /// <summary>숨김 처리: 편집 중이면 먼저 저장하고, 숨김 시각을 기록해 덱에서 제거</summary>
    private void Tab_ArchiveRequested(object sender, RoutedEventArgs e)
    {
        if (sender is not NoteTab tab || tab.Note is null) return;

        if (_editingTab == tab)
            tab.EndEdit();
        if (_expandedTab == tab)
            _expandedTab = null;

        _store.Archive(tab.Note, DateTime.Now);

        if (!IsMouseOver)
            _collapseTimer.Start();
    }

    private void Tab_DeleteRequested(object sender, RoutedEventArgs e)
    {
        if (sender is not NoteTab tab || tab.Note is null) return;

        // 편집 중 삭제는 입력 내용을 저장하지 않고 바로 제거
        if (_editingTab == tab)
            _editingTab = null;
        if (_expandedTab == tab)
            _expandedTab = null;

        _store.Delete(tab.Note);

        if (!IsMouseOver)
            _collapseTimer.Start();
    }

    private void PlusButton_Click(object sender, MouseButtonEventArgs e) => AddNote();

    /// <summary>기본 제목의 빈 노트 추가</summary>
    private void AddNote() => _store.Add(DateTime.Now);

    private void AllNotes_Click(object sender, RoutedEventArgs e) => ShowAllNotes();

    private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings();

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}

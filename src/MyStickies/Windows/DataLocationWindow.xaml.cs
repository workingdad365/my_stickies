using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MyStickies.Data;
using MyStickies.Interop;
using MyStickies.Layout;

namespace MyStickies.Windows;

/// <summary>
/// 설정 창. 메모 DB 파일을 둘 폴더와 자동 실행 여부.
/// 최초 실행 모드에서는 저장 폴더만 물어봄
/// </summary>
public partial class DataLocationWindow : Window
{
    /// <summary>확인 시 선택된 폴더</summary>
    public string SelectedDirectory { get; private set; }

    /// <summary>확인 시 자동 실행 체크 상태 (설정 모드에서만 의미 있음)</summary>
    public bool AutoStart => AutoStartBox.IsChecked == true;

    /// <summary>확인 시 선택된 도킹 모니터 장치 이름. 주 모니터면 null</summary>
    public string? SelectedMonitor =>
        MonitorBox.SelectedItem is MonitorInfo { IsPrimary: false } m ? m.DeviceName : null;

    public int DeckMaxNotes => MaxNotesBox.SelectedItem is int n ? n : DeckGeometry.DefaultMaxDeckNotes;
    public int DeckCenterPercent => (int)Math.Round(CenterPercentSlider.Value);
    public int CollapseDelayMs => (int)Math.Round(DelaySlider.Value);
    public bool HideOnFullscreen => HideOnFullscreenBox.IsChecked == true;
    public bool GlobalHotkeys => HotkeysBox.IsChecked == true;
    public bool CheckForUpdates => UpdateCheckBox.IsChecked == true;

    /// <summary>선택한 글꼴의 저장용 이름</summary>
    public string FontFamilyName =>
        FontBox.SelectedItem is FontSettings.FontChoice f ? f.Source : FontSettings.DefaultFamily;

    public int NoteFontSize => (int)Math.Round(FontSizeSlider.Value);

    public DataLocationWindow(AppSettings settings, bool firstRun)
    {
        SelectedDirectory = settings.DataDirectory;
        InitializeComponent();

        Title = firstRun ? "메모 저장 위치" : "설정";
        VersionText.Text = $"My Stickies {AppInfo.Version}";
        Intro.Text = firstRun
            ? "메모를 저장할 폴더를 정해 주세요. 기본 위치는 이 PC의 앱 데이터 폴더입니다. " +
              "Synology Drive 같은 동기화 폴더를 지정하면 여러 PC에서 같은 메모를 함께 쓸 수 있습니다. " +
              "나중에 트레이 메뉴의 설정에서 바꿀 수 있습니다."
            : "메모 파일(my_stickies.db)을 둘 폴더를 바꾸면 새 폴더에 이미 메모 파일이 있을 때 그 파일을 그대로 사용하고, " +
              "없으면 현재 메모를 복사할지 물어본 뒤 새로 만듭니다.";
        CancelButton.Content = firstRun ? "기본 위치 사용" : "취소";

        PathBox.Text = SelectedDirectory;
        UpdateStatus();

        StartupSection.Visibility = firstRun ? Visibility.Collapsed : Visibility.Visible;
        MonitorSection.Visibility = firstRun ? Visibility.Collapsed : Visibility.Visible;
        DeckSection.Visibility = firstRun ? Visibility.Collapsed : Visibility.Visible;
        FontSection.Visibility = firstRun ? Visibility.Collapsed : Visibility.Visible;
        if (!firstRun)
        {
            AutoStartBox.IsChecked = StartupRegistration.IsEnabled();
            UpdateAutoStartHint();

            var monitors = MonitorInfo.All();
            MonitorBox.ItemsSource = monitors;
            MonitorBox.SelectedItem = MonitorInfo.Resolve(settings.DockMonitor);

            MaxNotesBox.ItemsSource = Enumerable.Range(DeckGeometry.MinMaxDeckNotes,
                DeckGeometry.MaxMaxDeckNotes - DeckGeometry.MinMaxDeckNotes + 1).ToList();
            MaxNotesBox.SelectedItem = Math.Clamp(settings.DeckMaxNotes, DeckGeometry.MinMaxDeckNotes, DeckGeometry.MaxMaxDeckNotes);
            CenterPercentSlider.Value = Math.Clamp(settings.DeckCenterPercent, 10, 90);
            DelaySlider.Value = Math.Clamp(settings.CollapseDelayMs, 100, 2000);
            HideOnFullscreenBox.IsChecked = settings.HideOnFullscreen;
            HotkeysBox.IsChecked = settings.GlobalHotkeys;
            UpdateCheckBox.IsChecked = settings.CheckForUpdates;
            HotkeysHint.Text = Interop.GlobalHotkeys.Description + ". 다른 프로그램과 겹치면 끄세요.";

            var fonts = FontSettings.InstalledFonts();
            FontBox.ItemsSource = fonts;
            FontBox.SelectedItem =
                fonts.FirstOrDefault(f => string.Equals(f.Source, settings.FontFamily, StringComparison.OrdinalIgnoreCase))
                ?? fonts.FirstOrDefault(f => string.Equals(f.Source, FontSettings.DefaultFamily, StringComparison.OrdinalIgnoreCase));
            FontSizeSlider.Value = FontSettings.ClampSize(settings.FontSize);
            UpdateFontPreview();
        }
    }

    private void UpdateStatus()
    {
        var dbPath = NoteRepository.PathFor(SelectedDirectory);
        StatusText.Text = File.Exists(dbPath)
            ? "이 폴더에 기존 메모 파일이 있어 그대로 사용합니다."
            : "이 폴더에는 메모 파일이 없어 새로 만듭니다. 현재 메모를 복사하거나, 안내 메모만 든 새 파일로 시작할 수 있습니다.";
    }

    /// <summary>자동 실행에 등록될 경로 안내. 게시된 설치 폴더가 아니면 경고</summary>
    private void UpdateAutoStartHint()
    {
        var exe = StartupRegistration.CurrentExePath ?? "(알 수 없음)";
        if (AutoStartBox.IsChecked != true)
        {
            AutoStartHint.Text = "로그인할 때 My Stickies를 자동으로 시작합니다.";
            return;
        }

        AutoStartHint.Text = StartupRegistration.IsRunningFromInstallDirectory()
            ? $"등록 경로: {exe}"
            : $"현재 실행 파일이 설치 폴더가 아닙니다: {exe}\n" +
              "빌드 출력 폴더가 정리되면 자동 실행이 깨질 수 있습니다. " +
              "publish.ps1로 게시한 뒤 그 실행 파일에서 등록하는 것을 권장합니다.";
    }

    /// <summary>미리보기에 현재 선택한 글꼴과 크기 적용</summary>
    private void UpdateFontPreview()
    {
        if (FontPreviewBody is null) return;
        var size = FontSettings.ClampSize((int)Math.Round(FontSizeSlider.Value));
        var family = FontBox.SelectedItem is FontSettings.FontChoice f ? f.Family : new System.Windows.Media.FontFamily(FontSettings.DefaultFamily);
        FontPreviewTitle.FontFamily = family;
        FontPreviewBody.FontFamily = family;
        FontPreviewTitle.FontSize = FontSettings.TitleSize(size);
        FontPreviewBody.FontSize = size;
        FontPreviewBody.LineHeight = FontSettings.BodyLineHeight(size);
    }

    private void FontBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateFontPreview();

    private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateFontPreview();

    private void AutoStartBox_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
            UpdateAutoStartHint();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "메모 저장 폴더 선택",
            InitialDirectory = Directory.Exists(SelectedDirectory) ? SelectedDirectory : AppSettings.DefaultDataDirectory,
        };
        if (dialog.ShowDialog(this) != true) return;

        SelectedDirectory = dialog.FolderName;
        PathBox.Text = SelectedDirectory;
        UpdateStatus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(SelectedDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"폴더를 만들거나 접근할 수 없습니다.\n{ex.Message}", "메모 저장 위치",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}

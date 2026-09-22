using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using MyStickies.Data;
using MyStickies.Interop;
using MyStickies.Layout;
using MyStickies.Localization;

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
    public string SelectedLanguage => LanguageBox.SelectedValue as string ?? "ko";

    public DataLocationWindow(AppSettings settings, bool firstRun)
    {
        SelectedDirectory = settings.DataDirectory;
        InitializeComponent();

        LanguageBox.SelectedValue = Strings.Normalize(settings.Language);
        Title = Strings.Get(firstRun ? "StorageTitle" : "Settings");
        VersionText.Text = $"My Stickies {AppInfo.Version}";
        Intro.Text = Strings.Get(firstRun ? "StorageIntroFirst" : "StorageIntro");
        CancelButton.Content = Strings.Get(firstRun ? "UseDefaultFolder" : "Cancel");

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
            HotkeysHint.Text = Strings.Get("HotkeysHint", Interop.GlobalHotkeys.Description);

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
        StatusText.Text = Strings.Get(File.Exists(dbPath) ? "StorageExists" : "StorageMissing");
    }

    /// <summary>자동 실행에 등록될 경로 안내. 게시된 설치 폴더가 아니면 경고</summary>
    private void UpdateAutoStartHint()
    {
        var exe = StartupRegistration.CurrentExePath ?? Strings.Get("Unknown");
        if (AutoStartBox.IsChecked != true)
        {
            AutoStartHint.Text = Strings.Get("AutoStartHint");
            return;
        }

        AutoStartHint.Text = StartupRegistration.IsRunningFromInstallDirectory()
            ? Strings.Get("RegisteredPath", exe)
            : Strings.Get("AutoStartWarning", exe);
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
            Title = Strings.Get("ChooseStorage"),
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
            MessageBox.Show(this, Strings.Get("StorageError", ex.Message), Strings.Get("StorageTitle"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}

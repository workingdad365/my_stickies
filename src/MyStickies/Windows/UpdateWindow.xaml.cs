using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using MyStickies.Data;
using MyStickies.Update;
using MyStickies.Localization;

namespace MyStickies.Windows;

/// <summary>업데이트 대화상자에서 사용자가 고른 동작</summary>
public enum UpdateChoice
{
    /// <summary>이번에는 설치하지 않음. 다음 확인 때 다시 알림</summary>
    Later,

    /// <summary>이 버전은 다시 알리지 않음</summary>
    Skip,

    /// <summary>다운로드 후 설치기를 실행함. 호출 측은 앱을 종료해야 함</summary>
    Install,
}

/// <summary>새 버전 안내, 릴리스 노트 표시, 설치 파일 다운로드와 설치 실행</summary>
public partial class UpdateWindow : Window
{
    private readonly UpdateInfo _info;
    private CancellationTokenSource? _download;

    public UpdateChoice Choice { get; private set; } = UpdateChoice.Later;

    public UpdateWindow(UpdateInfo info)
    {
        _info = info;
        InitializeComponent();

        RefreshLanguage();
        Strings.LanguageChanged += RefreshLanguage;
        Closed += (_, _) => Strings.LanguageChanged -= RefreshLanguage;

        Closing += OnClosing;
    }

    private void RefreshLanguage()
    {
        Headline.Text = Strings.Get("UpdateHeadline", _info.Version);
        SubText.Text = Strings.Get("UpdateSubtext", AppInfo.Version);
        Notes.Text = string.IsNullOrWhiteSpace(_info.Notes) ? Strings.Get("NoReleaseNotes") : _info.Notes.Trim();
    }

    /// <summary>다운로드 중 창을 닫으면 다운로드를 취소하고 "나중에"로 처리</summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_download is null) return;
        _download.Cancel();
        Choice = UpdateChoice.Later;
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        Choice = UpdateChoice.Skip;
        Close();
    }

    private void Later_Click(object sender, RoutedEventArgs e)
    {
        Choice = UpdateChoice.Later;
        Close();
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        SetBusy(true);
        _download = new CancellationTokenSource();
        try
        {
            var progress = new Progress<double>(p =>
            {
                Progress.Value = p;
                ProgressText.Text = Strings.Get("DownloadProgress", p);
            });
            var path = await UpdateChecker.DownloadAsync(_info, progress, _download.Token);

            Strings.Bind(ProgressText, System.Windows.Controls.TextBlock.TextProperty, "RunInstaller");
            UpdateChecker.RunInstaller(path);

            Choice = UpdateChoice.Install;
            _download = null;
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            Choice = UpdateChoice.Later;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException
                                   or System.ComponentModel.Win32Exception)
        {
            _download = null;
            SetBusy(false);
            MessageBox.Show(this, Strings.Get("UpdateFailed", ex.Message), Strings.Get("Update"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SetBusy(bool busy)
    {
        InstallButton.IsEnabled = !busy;
        SkipButton.IsEnabled = !busy;
        Strings.Bind(LaterButton, System.Windows.Controls.ContentControl.ContentProperty, busy ? "Cancel" : "Later");
        ProgressPanel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (busy)
        {
            Progress.Value = 0;
            Strings.Bind(ProgressText, System.Windows.Controls.TextBlock.TextProperty, "DownloadPreparing");
        }
    }
}

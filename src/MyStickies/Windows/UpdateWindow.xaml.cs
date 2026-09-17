using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using MyStickies.Data;
using MyStickies.Update;

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

        Headline.Text = $"새 버전 v{info.Version}이 있습니다";
        SubText.Text = $"현재 버전 {AppInfo.Version}. 설치하면 앱이 잠시 종료된 뒤 자동으로 다시 실행됩니다.";
        Notes.Text = string.IsNullOrWhiteSpace(info.Notes) ? "릴리스 노트가 없습니다." : info.Notes.Trim();

        Closing += OnClosing;
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
                ProgressText.Text = $"다운로드 중... {p:P0}";
            });
            var path = await UpdateChecker.DownloadAsync(_info, progress, _download.Token);

            ProgressText.Text = "설치 프로그램을 실행합니다...";
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
            MessageBox.Show(this, $"업데이트를 설치하지 못했습니다.\n{ex.Message}", "업데이트",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SetBusy(bool busy)
    {
        InstallButton.IsEnabled = !busy;
        SkipButton.IsEnabled = !busy;
        LaterButton.Content = busy ? "취소" : "나중에";
        ProgressPanel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (busy)
        {
            Progress.Value = 0;
            ProgressText.Text = "다운로드 준비 중...";
        }
    }
}

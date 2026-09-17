using System.Windows;

namespace MyStickies;

public partial class App : Application
{
    private const string MutexName = "MyStickies.SingleInstance";
    private Mutex? _mutex;

    /// <summary>중복 실행 방지. 이미 떠 있는 인스턴스가 있으면 조용히 종료</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.Dispose();
        base.OnExit(e);
    }
}

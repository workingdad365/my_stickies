using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MyStickies.Converters;
using MyStickies.Layout;

namespace MyStickies.Tests;

/// <summary>테스트 프로세스에서 WPF Application 하나와 STA 스레드를 공유함</summary>
internal static class WpfTestHost
{
    private static readonly Lazy<Dispatcher> Host = new(Start);

    public static void Run(Action action) => Host.Value.Invoke(action);

    public static void Flush() => Dispatcher.CurrentDispatcher.Invoke(
        DispatcherPriority.ApplicationIdle, new Action(() => { }));

    private static Dispatcher Start()
    {
        using var ready = new ManualResetEventSlim();
        Dispatcher? dispatcher = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                application.Resources["HexToBrush"] = new HexToBrushConverter();
                foreach (var key in new[] { "NoteTitleBrush", "NoteBodyBrush", "NoteLabelBrush", "NoteDashBrush" })
                    application.Resources[key] = Brushes.Black;
                FontSettings.Apply(application.Resources, "Malgun Gothic", 14);
                dispatcher = Dispatcher.CurrentDispatcher;
            }
            catch (Exception ex) { failure = ex; }
            finally { ready.Set(); }
            if (failure is null) Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("WPF 테스트 초기화 시간 초과");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        return dispatcher!;
    }
}

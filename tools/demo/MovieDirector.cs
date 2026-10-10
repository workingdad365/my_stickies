using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MyStickies.Controls;
using MyStickies.Data;
using MyStickies.Models;
using MyStickies.Localization;

namespace MyStickies;

internal sealed class MovieText
{
    public required string FontFamily { get; init; }
    public required string Culture { get; init; }
    public required string Eyebrow { get; init; }
    public required string[] Headline { get; init; }
    public required string Tagline { get; init; }
    public required string[] StepTitles { get; init; }
    public required string[] StepDetails { get; init; }
    public required string Footer { get; init; }
    public required string Intro { get; init; }
    public required string[] NoteTitles { get; init; }
    public required string[] NoteBodies { get; init; }
    public required string Typed { get; init; }
}

// 임시 앱 복사본에만 포함되는 녹화 감독. 실제 창과 이벤트 처리기를 실행함.
internal static class MovieDirector
{
    internal static MovieText Copy { get; private set; } = null!;
    internal static string Language { get; private set; } = "ko";
    internal static string FileStem => Language == "ko" ? "my-stickies-demo" : $"my-stickies-demo-{Language}";

    internal static async void Start(App app)
    {
        var args = Environment.GetCommandLineArgs();
        try
        {
            Language = args[3];
            var catalog = JsonSerializer.Deserialize<Dictionary<string, MovieText>>(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "locales.json")))!;
            Copy = catalog[Language];
            if (Copy.Headline.Length != 2 || Copy.StepTitles.Length != 4 || Copy.StepDetails.Length != 4
                || Copy.NoteTitles.Length != 3 || Copy.NoteBodies.Length != 3)
                throw new InvalidOperationException("영상 번역 항목 수 불일치");
            if (!Fonts.SystemFontFamilies.Any(f => f.Source == Copy.FontFamily))
                throw new InvalidOperationException($"영상 글꼴 미설치: {Copy.FontFamily}");
            Directory.CreateDirectory(AppSettings.LocalDirectory);
            new AppSettings
            {
                DataDirectory = AppSettings.LocalDirectory,
                Language = Language, GlobalHotkeys = false, HideOnFullscreen = false,
                CheckForUpdates = false, DeckMaxNotes = 3, FontSize = 14, FontFamily = Copy.FontFamily,
            }.Save(AppSettings.SettingsPath);
            var repository = new NoteRepository(NoteRepository.DefaultPath);
            repository.Initialize();
            string[] colors = [NotePalette.Green, NotePalette.Blue, NotePalette.Purple];
            for (var i = 0; i < 3; i++)
                repository.Insert(new Note { Title = Copy.NoteTitles[i], Body = Copy.NoteBodies[i], ColorHex = colors[i], SortOrder = i });
            var window = new MainWindow { Topmost = false };
            if (Strings.Current.Language != Language) throw new InvalidOperationException("앱 UI 언어 적용 실패");
            app.MainWindow = window;
            window.Show();
            await window.RecordMovie(args[1], args[2]);
            window.Close();
            app.Shutdown();
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(args[1], $"render-error-{Language}.log"), error.ToString());
            app.Shutdown(1);
        }
    }
}

public partial class MainWindow
{
    private const int MovieWidth = 1920, MovieHeight = 1080, MovieFps = 30;
    private const double DesktopX = 610, DesktopY = 174, DesktopScale = 1.28;
    private static readonly Rect DesktopRect = new(DesktopX, DesktopY, 960 * DesktopScale, 640 * DesktopScale);
    private static MovieText Copy => MovieDirector.Copy;

    internal async Task RecordMovie(string output, string ffmpeg)
    {
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await Task.Delay(350);
        var note = Notes[0];
        var tab = Tabs().First(t => t.Note == note);
        var start = new ProcessStartInfo(ffmpeg)
        {
            UseShellExecute = false, RedirectStandardInput = true,
            CreateNoWindow = true,
        };
        foreach (var arg in new[] { "-hide_banner", "-loglevel", "error", "-y", "-f", "rawvideo", "-pixel_format", "bgra", "-video_size", "1920x1080", "-framerate", "30", "-i", "pipe:0", "-an", "-c:v", "libx264", "-preset", "medium", "-crf", "19", "-pix_fmt", "yuv420p", "-movflags", "+faststart", Path.Combine(output, MovieDirector.FileStem + ".mp4") })
            start.ArgumentList.Add(arg);
        using var encoder = Process.Start(start) ?? throw new InvalidOperationException("FFmpeg 실행 실패");
        var bytes = new byte[MovieWidth * MovieHeight * 4];
        var watch = Stopwatch.StartNew();
        BitmapSource? opening = null;
        var lastTyped = -1;
        var typed = Copy.Typed;
        var initialBody = note.Body;

        for (var frame = 0; frame < MovieFps * 15; frame++)
        {
            var time = frame / (double)MovieFps;
            _collapseTimer.Stop();
            if (frame == 51) FanOut();
            if (frame == 83) ExpandTab(tab);
            if (frame == 126) tab.BeginEdit(false);
            if (frame >= 135 && frame <= 192)
            {
                var count = Math.Min(typed.Length, (frame - 135) * typed.Length / 48);
                if (count != lastTyped)
                {
                    var box = (TextBox)tab.FindName("BodyBox");
                    box.Text = initialBody + typed[..count];
                    box.CaretIndex = box.Text.Length;
                    lastTyped = count;
                }
            }
            if (frame == 207)
            {
                tab.EndEdit();
                if (note.Body != initialBody + typed) throw new InvalidOperationException("편집 내용 검증 실패");
                if (new NoteRepository(_store.DbPath).LoadAll().First(n => n.Id == note.Id).Body != note.Body)
                    throw new InvalidOperationException("데모 DB 저장 검증 실패");
            }
            if (frame == 240)
            {
                tab.RaiseEvent(new RoutedEventArgs(NoteTab.PinRequestedEvent, tab));
                if (!_floatingNotes.ContainsKey(note.Id)) throw new InvalidOperationException("고정 창 생성 실패");
            }
            if (frame >= 258 && frame <= 294 && _floatingNotes.TryGetValue(note.Id, out var moving))
            {
                var p = Ease((frame - 258) / 36.0);
                moving.Left = -20000 + Lerp(612, 348, p);
                moving.Top = -20000 + Lerp(49, 204, p);
            }
            if (frame == 375)
            {
                var pinned = (NoteTab)_floatingNotes[note.Id].FindName("NoteView");
                pinned.RaiseEvent(new RoutedEventArgs(NoteTab.PinRequestedEvent, pinned));
                if (_floatingNotes.Count != 0 || !DeckNotes.Contains(note))
                    throw new InvalidOperationException("고정 해제 검증 실패");
            }
            UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                DrawMovieBackground(dc, time);
                dc.PushClip(new RectangleGeometry(DesktopRect, 24, 24));
                DrawDesktop(dc);
                DrawWindowContent(dc, Root, DesktopX + 600 * DesktopScale, DesktopY, 360, 640);
                foreach (var floating in _floatingNotes.Values)
                {
                    var content = (FrameworkElement)floating.Content;
                    DrawWindowContent(dc, content,
                        DesktopX + (floating.Left + 20000) * DesktopScale,
                        DesktopY + (floating.Top + 20000) * DesktopScale,
                        content.ActualWidth, content.ActualHeight);
                }
                DrawCursor(dc, time);
                dc.Pop();
                dc.DrawRoundedRectangle(null, new Pen(Brush("#324B45"), 1), DesktopRect, 24, 24);
                if (time > 13.8 && opening is not null)
                {
                    dc.PushOpacity(Ease((time - 13.8) / 0.8));
                    dc.DrawImage(opening, new Rect(0, 0, MovieWidth, MovieHeight));
                    dc.Pop();
                }
            }
            var bitmap = new RenderTargetBitmap(MovieWidth, MovieHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            if (frame == 0) { opening = bitmap; opening.Freeze(); }
            // 루프 마지막 구간은 첫 프레임을 그대로 사용하여 경계 오차를 제거함.
            var result = time >= 14.6 ? opening! : bitmap;
            result.CopyPixels(bytes, MovieWidth * 4, 0);
            await encoder.StandardInput.BaseStream.WriteAsync(bytes);
            if (frame is 0 or 105 or 183 or 306 or 449)
            {
                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(result));
                using var file = File.Create(Path.Combine(output, $"check-{MovieDirector.Language}-{frame:000}.png"));
                png.Save(file);
            }
            var wait = TimeSpan.FromSeconds((frame + 1) / (double)MovieFps) - watch.Elapsed;
            await Task.Delay(wait > TimeSpan.Zero ? wait : TimeSpan.FromMilliseconds(1));
        }
        encoder.StandardInput.Close();
        await encoder.WaitForExitAsync();
        if (encoder.ExitCode != 0) throw new InvalidOperationException("MP4 인코딩 실패");
        File.WriteAllText(Path.Combine(output, $"verification-{MovieDirector.Language}.log"),
            $"450 frames / 1920x1080 / 30 fps / 15 seconds\n앱 언어: {Strings.Current.Language}\n실제 앱 편집, SQLite 저장, 고정, 고정 해제 검증 통과\n");
    }

    private static void DrawWindowContent(DrawingContext dc, FrameworkElement element, double x, double y, double width, double height)
    {
        if (width <= 0 || height <= 0) return;
        var image = new RenderTargetBitmap((int)Math.Ceiling(width * DesktopScale), (int)Math.Ceiling(height * DesktopScale),
            96 * DesktopScale, 96 * DesktopScale, PixelFormats.Pbgra32);
        image.Render(element);
        dc.DrawImage(image, new Rect(x, y, width * DesktopScale, height * DesktopScale));
    }

    private static void DrawMovieBackground(DrawingContext dc, double time)
    {
        dc.DrawRectangle(Brush("#F4F3ED"), null, new Rect(0, 0, MovieWidth, MovieHeight));
        dc.DrawRoundedRectangle(Brush("#243F35"), null, new Rect(76, 70, 42, 42), 12, 12);
        dc.DrawRoundedRectangle(Brush(NotePalette.Yellow), null, new Rect(87, 80, 8, 23), 3, 3);
        dc.DrawRoundedRectangle(Brush(NotePalette.Green), null, new Rect(100, 80, 8, 16), 3, 3);
        Text(dc, "My Stickies", 132, 69, 28, "#243F35", true);
        Text(dc, Copy.Eyebrow, 1510, 83, 15, "#65776C");
        Text(dc, Copy.Headline[0], 76, 234, 54, "#243F35", true);
        Text(dc, Copy.Headline[1], 76, 310, 54, "#243F35", true);
        Text(dc, Copy.Tagline, 80, 416, 24, "#65776C");
        var step = time < 4.2 ? 0 : time < 8 ? 1 : time < 12.5 ? 2 : 3;
        dc.DrawLine(new Pen(Brush("#C9D2C7"), 1), new Point(80, 646), new Point(498, 646));
        Text(dc, $"0{step + 1}", 80, 683, 18, "#72816E", true);
        Text(dc, Copy.StepTitles[step], 80, 727, 29, "#243F35", true);
        Text(dc, Copy.StepDetails[step], 80, 777, 19, "#65776C");
        for (var i = 0; i < 4; i++)
            dc.DrawRoundedRectangle(Brush(i == step ? "#365C46" : "#D6DDCF"), null, new Rect(80 + i * 42, 844, 30, 4), 2, 2);
        Text(dc, Copy.Footer, 80, 975, 17, "#65776C");
        Text(dc, Copy.Intro, 610, 130, 18, "#65776C");
    }

    private static void DrawDesktop(DrawingContext dc)
    {
        dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(30, 58, 51), Color.FromRgb(100, 133, 105), 65), null, DesktopRect);
        dc.PushTransform(new TranslateTransform(DesktopX, DesktopY));
        dc.DrawEllipse(Brush("#BBC8A1"), null, new Point(276, 229), 83, 83);
        dc.DrawGeometry(Brush("#779680"), null, Geometry.Parse("M 0,550 C 260,210 660,380 1230,135 L 1230,820 L 0,820 Z"));
        dc.DrawGeometry(Brush("#526F61"), null, Geometry.Parse("M 0,650 C 320,370 560,570 1230,350 L 1230,820 L 0,820 Z"));
        dc.DrawGeometry(Brush("#375649"), null, Geometry.Parse("M 0,720 C 390,550 730,750 1230,565 L 1230,820 L 0,820 Z"));
        dc.DrawGeometry(Brush("#29483C"), null, Geometry.Parse("M 0,785 C 370,650 710,810 1230,705 L 1230,820 L 0,820 Z"));
        dc.Pop();
    }

    private static void DrawCursor(DrawingContext dc, double time)
    {
        (double t, double x, double y)[] keys =
        [
            (0, 715, 505), (0.65, 715, 505), (1.65, 950, 305), (2.1, 950, 305),
            (2.7, 923, 110), (3.15, 923, 110), (3.9, 739, 174), (4.3, 739, 174),
            (6.8, 760, 213), (7.9, 887, 80), (8.25, 887, 80),
            (8.6, 632, 132), (9.8, 368, 287), (10.5, 368, 287),
            (11.4, 615, 235), (12.45, 615, 235), (13.4, 715, 505), (15, 715, 505),
        ];
        var index = 0;
        while (index < keys.Length - 2 && time > keys[index + 1].t) index++;
        var a = keys[index]; var b = keys[index + 1];
        var p = Ease((time - a.t) / (b.t - a.t));
        var x = DesktopX + Lerp(a.x, b.x, p) * DesktopScale;
        var y = DesktopY + Lerp(a.y, b.y, p) * DesktopScale;
        foreach (var click in new[] { 4.2, 8.0, 12.5 })
        {
            var age = time - click;
            if (age is >= 0 and < 0.45)
            {
                dc.PushOpacity(1 - age / 0.45);
                dc.DrawEllipse(null, new Pen(Brush("#FBE27A"), 3), new Point(x, y), 8 + age * 60, 8 + age * 60);
                dc.Pop();
            }
        }
        dc.PushTransform(new TranslateTransform(x, y));
        dc.DrawGeometry(Brush("#FFFFFF"), new Pen(Brush("#243F35"), 1.5),
            Geometry.Parse("M 0,0 L 2,27 L 9,20 L 15,32 L 20,29 L 14,18 L 25,17 Z"));
        dc.Pop();
    }

    private static SolidColorBrush Brush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    private static double Ease(double value) { var t = Math.Clamp(value, 0, 1); return t * t * (3 - 2 * t); }
    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    private static void Text(DrawingContext dc, string text, double x, double y, double size, string color, bool bold = false)
    {
        var formatted = new FormattedText(text, CultureInfo.GetCultureInfo(Copy.Culture), FlowDirection.LeftToRight,
            new Typeface(new FontFamily(Copy.FontFamily), FontStyles.Normal,
                bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal), size, Brush(color), 1);
        // 번역 길이에 따라 왼쪽 문구 영역 안에서 글꼴 크기를 조절함.
        var maxWidth = x < 600 ? 440 : MovieWidth - x - 76;
        if (formatted.Width > maxWidth) formatted.SetFontSize(size * maxWidth / formatted.Width);
        dc.DrawText(formatted, new Point(x, y));
    }
}

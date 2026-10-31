using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MyStickies.Controls;
using MyStickies.Data;
using MyStickies.Localization;
using MyStickies.Models;
using MyStickies.Windows;

namespace MyStickies.Tests;

public class CopyOnDragTextBoxTests
{
    [Fact]
    public void StationaryClick_WhenTextMoves_DoesNotCopyAndRestoresCaret()
    {
        WpfTestHost.Run(() =>
        {
            var box = new RecordingTextBox { Text = "움직이는 메모의 본문" };
            box.Select(2, 0);
            RaiseMouseButton(box, Mouse.MouseDownEvent, MouseButton.Left);

            // 카드 이동으로 같은 화면 좌표 아래의 문자가 달라진 상황을 재현함
            box.RenderTransform = new TranslateTransform(-200, 0);
            var move = new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                { RoutedEvent = Mouse.PreviewMouseMoveEvent };
            box.RaiseEvent(move);
            Assert.True(move.Handled);
            box.Select(2, 6);
            RaiseMouseButton(box, Mouse.MouseUpEvent, MouseButton.Left);

            Assert.Empty(box.Copies);
            Assert.Equal(2, box.SelectionStart);
            Assert.Equal(0, box.SelectionLength);
        });
    }

    [Fact]
    public void SlightPointerJitter_DoesNotCopyOrKeepAnAccidentalSelection()
    {
        WpfTestHost.Run(() =>
        {
            var box = new RecordingTextBox { Text = "조금 흔들린 클릭" };
            RaiseMouseButton(box, Mouse.MouseDownEvent, MouseButton.Left);
            box.ScreenPosition += new Vector(SystemParameters.MinimumHorizontalDragDistance / 2,
                SystemParameters.MinimumVerticalDragDistance / 2);
            box.SelectAll();
            RaiseMouseButton(box, Mouse.MouseUpEvent, MouseButton.Left);

            Assert.Empty(box.Copies);
            Assert.Equal(0, box.SelectionLength);
        });
    }

    [Fact]
    public void DragReturningToItsStart_StillCopiesTheFinalSelection()
    {
        WpfTestHost.Run(() =>
        {
            var box = new RecordingTextBox { Text = "드래그한 본문" };
            var start = box.ScreenPosition;
            RaiseMouseButton(box, Mouse.MouseDownEvent, MouseButton.Left);
            box.ScreenPosition += new Vector(80, 0);
            var move = new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                { RoutedEvent = Mouse.PreviewMouseMoveEvent };
            box.RaiseEvent(move);
            Assert.False(move.Handled);
            box.ScreenPosition = start;
            box.Select(0, 4);
            RaiseMouseButton(box, Mouse.MouseUpEvent, MouseButton.Left);

            Assert.Equal("드래그한", Assert.Single(box.Copies));
        });
    }

    [Fact]
    public void CollapsedAndOpeningCard_DoNotAcceptBodyMouseInput()
    {
        WpfTestHost.Run(() =>
        {
            var card = new NoteTab { DataContext = new Note { Title = "제목", Body = "본문" } };
            var body = (FrameworkElement)card.FindName("BodyPanel");
            Assert.False(body.IsHitTestVisible);

            card.SlideTo(0);
            card.SetExpanded(true);
            Assert.False(body.IsHitTestVisible);
        });
    }

    [Fact]
    public void OpeningCard_EnablesBodyAfterBothAnimationsEvenWhenEditingChangesHeight()
    {
        WpfTestHost.Run(() =>
        {
            var card = new NoteTab { DataContext = new Note { Title = "제목", Body = "본문" } };
            var body = (FrameworkElement)card.FindName("BodyPanel");
            card.SlideTo(0, TimeSpan.FromMilliseconds(200));
            card.SetExpanded(true);
            card.BeginEdit(false);
            Assert.False(body.IsHitTestVisible);

            WaitUntil(() => body.IsHitTestVisible);
            Assert.Equal(0, card.RenderTransform.Value.OffsetX);
            Assert.Equal(1, body.Opacity);
            card.SlideTo(0);
            Assert.True(body.IsHitTestVisible);

            card.EndEdit(false);
            card.SetExpanded(false);
            Assert.False(body.IsHitTestVisible);
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LeftRelease_CopiesExactSelectionInEitherDirectionAndPreservesText(bool readOnly, bool backwards)
    {
        WpfTestHost.Run(() =>
        {
            const string content = "앞부분 한글\r\n  second line\t 끝부분";
            var box = new RecordingTextBox { Text = content, IsReadOnly = readOnly, AcceptsReturn = true };
            var start = 4;
            var length = content.Length - start - 4;
            box.Select(backwards ? start + length : start, 0);
            RaiseMouseButton(box, Mouse.MouseDownEvent, MouseButton.Left);
            box.Select(start, length);

            Assert.Empty(box.Copies);
            Assert.True(box.IsSelecting);
            box.ScreenPosition += new Vector(backwards ? -80 : 80, 0);
            RaiseMouseButton(box, Mouse.MouseUpEvent, MouseButton.Left);

            Assert.Equal(content.Substring(start, length), Assert.Single(box.Copies));
            Assert.Equal(content, box.Text);
            Assert.Equal(start, box.SelectionStart);
            Assert.Equal(length, box.SelectionLength);
            Assert.False(box.IsSelecting);
            Assert.Equal(Strings.Get("SelectionCopied"), Notice(box).Content);
        });
    }

    [Fact]
    public void ClicksKeyboardSelectionAndRightButton_DoNotOverwriteClipboard()
    {
        WpfTestHost.Run(() =>
        {
            var box = new RecordingTextBox { Text = "선택할 본문", IsReadOnly = true };
            var clicks = 0;
            box.ReadClicked += (_, _) => clicks++;

            RaiseMouseButton(box, Mouse.MouseDownEvent, MouseButton.Left);
            RaiseMouseButton(box, Mouse.MouseUpEvent, MouseButton.Left);
            Assert.Equal(1, clicks);

            box.SelectAll();
            RaiseMouseButton(box, Mouse.MouseUpEvent, MouseButton.Left);
            RaiseMouseButton(box, Mouse.MouseDownEvent, MouseButton.Right);
            box.Select(1, 2);
            RaiseMouseButton(box, Mouse.MouseUpEvent, MouseButton.Right);

            // 기존 선택을 그대로 누르거나 빈 선택으로 끝내는 경우도 복사하지 않음
            RaiseMouseButton(box, Mouse.MouseDownEvent, MouseButton.Left);
            RaiseMouseButton(box, Mouse.MouseUpEvent, MouseButton.Left);
            RaiseMouseButton(box, Mouse.MouseDownEvent, MouseButton.Left);
            box.Select(0, 0);
            box.ScreenPosition += new Vector(80, 0);
            RaiseMouseButton(box, Mouse.MouseUpEvent, MouseButton.Left);

            Assert.Empty(box.Copies);
            Assert.Equal(1, clicks);
        });
    }

    [Fact]
    public void ClipboardFailure_ShowsFailureAndAllowsTheNextSelection()
    {
        WpfTestHost.Run(() =>
        {
            var box = new RecordingTextBox { Text = "다시 선택할 본문", FailCopy = true };
            RaiseMouseButton(box, Mouse.MouseDownEvent, MouseButton.Left);
            box.Select(0, 2);
            box.ScreenPosition += new Vector(80, 0);
            RaiseMouseButton(box, Mouse.MouseUpEvent, MouseButton.Left);
            Assert.Empty(box.Copies);
            Assert.Equal(Strings.Get("SelectionCopyFailed"), Notice(box).Content);

            box.FailCopy = false;
            RaiseMouseButton(box, Mouse.MouseDownEvent, MouseButton.Left);
            box.Select(3, 3);
            box.ScreenPosition += new Vector(80, 0);
            RaiseMouseButton(box, Mouse.MouseUpEvent, MouseButton.Left);
            Assert.Equal("선택할", Assert.Single(box.Copies));
            Assert.Equal(Strings.Get("SelectionCopied"), Notice(box).Content);
        });
    }

    [Fact]
    public void UnloadedSelection_IsCancelled()
    {
        WpfTestHost.Run(() =>
        {
            var box = new RecordingTextBox { Text = "본문" };
            RaiseMouseButton(box, Mouse.MouseDownEvent, MouseButton.Left);
            box.SelectAll();
            box.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            RaiseMouseButton(box, Mouse.MouseUpEvent, MouseButton.Left);
            Assert.Empty(box.Copies);
            Assert.False(box.IsSelecting);
        });
    }

    [Fact]
    public void NoteViews_UseCopyableReadAndEditFieldsAndStillEnterEditingOnClick()
    {
        WpfTestHost.Run(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "mystickies_copy_" + Guid.NewGuid().ToString("N"));
            NoteStore? store = null;
            AllNotesWindow? manager = null;
            FloatingNoteWindow? floating = null;
            try
            {
                floating = new FloatingNoteWindow(new Note { Title = "제목", Body = "본문" });
                var card = (NoteTab)floating.FindName("NoteView");
                AssertFields(card, "TitleText", "BodyText", "TitleBox", "BodyBox");
                var body = (CopyOnDragTextBox)card.FindName("BodyText");
                RaiseMouseButton(body, Mouse.MouseDownEvent, MouseButton.Left);
                Assert.True(card.IsSelectingText);
                RaiseMouseButton(body, Mouse.MouseUpEvent, MouseButton.Left);
                Assert.True(card.IsEditing);
                Assert.False(card.IsSelectingText);
                Assert.Equal(Visibility.Visible, ((TextBox)card.FindName("BodyBox")).Visibility);
                card.EndEdit(false);

                store = new NoteStore(new NoteRepository(Path.Combine(directory, "notes.db")));
                manager = new AllNotesWindow(store);
                ((ListBox)manager.FindName("NoteList")).SelectedIndex = 0;
                WpfTestHost.Flush();
                AssertFields(manager, "DetailTitle", "DetailBody", "DetailTitleBox", "DetailBodyBox");
                var title = (CopyOnDragTextBox)manager.FindName("DetailTitle");
                RaiseMouseButton(title, Mouse.MouseDownEvent, MouseButton.Left);
                RaiseMouseButton(title, Mouse.MouseUpEvent, MouseButton.Left);
                Assert.Equal(Visibility.Visible, ((TextBox)manager.FindName("DetailTitleBox")).Visibility);
                Assert.Equal(Visibility.Collapsed, title.Visibility);
            }
            finally
            {
                floating?.CloseWithoutSaving();
                manager?.Close();
                store?.Dispose();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        });
    }

    private static void AssertFields(FrameworkElement view, string title, string body, string titleEditor, string bodyEditor)
    {
        Assert.True(Assert.IsType<CopyOnDragTextBox>(view.FindName(title)).IsReadOnly);
        Assert.True(Assert.IsType<CopyOnDragTextBox>(view.FindName(body)).IsReadOnly);
        Assert.False(Assert.IsType<CopyOnDragTextBox>(view.FindName(titleEditor)).IsReadOnly);
        Assert.False(Assert.IsType<CopyOnDragTextBox>(view.FindName(bodyEditor)).IsReadOnly);
    }

    private static ToolTip Notice(CopyOnDragTextBox box) =>
        (ToolTip)typeof(CopyOnDragTextBox).GetField("_copyNotice", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(box)!;

    private static void WaitUntil(Func<bool> condition)
    {
        var elapsed = Stopwatch.StartNew();
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) =>
        {
            if (condition() || elapsed.Elapsed > TimeSpan.FromSeconds(3)) frame.Continue = false;
        };
        timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
        Assert.True(condition(), "카드 애니메이션 완료 후 본문 입력이 활성화되어야 함");
    }

    private static void RaiseMouseButton(UIElement element, RoutedEvent routedEvent, MouseButton button) =>
        element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button)
        {
            RoutedEvent = routedEvent,
            // WPF 기본 선택 처리 후에도 수신하는 이벤트 경로를 검증함
            Handled = true,
        });

    private sealed class RecordingTextBox : CopyOnDragTextBox
    {
        public List<string> Copies { get; } = [];
        public bool FailCopy { get; set; }
        public Point ScreenPosition { get; set; } = new(100, 100);

        protected override Point GetScreenPosition(MouseEventArgs e) => ScreenPosition;

        protected override void WriteClipboardText(string text)
        {
            if (FailCopy) throw new ExternalException("Clipboard is busy");
            Copies.Add(text);
        }
    }
}

using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MyStickies.Controls;
using MyStickies.Layout;
using MyStickies.Models;
using MyStickies.Windows;

namespace MyStickies.Tests;

public class FloatingNoteWindowTests
{
    [Theory]
    [InlineData("CornerResize", 40, 60, 396, 276)]
    [InlineData("CornerResize", -40, -30, 316, 186)]
    [InlineData("BottomResize", 100, 60, 356, 276)]
    [InlineData("BottomResize", -100, -30, 356, 186)]
    [InlineData("CornerResize", -1000, -1000, 280, 166)]
    public void ResizeHandles_ChangeRequestedDimensionsAndNotifyPersistence(string handle,
        double dx, double dy, double width, double height)
    {
        WpfTestHost.Run(() =>
        {
            var window = new FloatingNoteWindow(new Note()) { Left = 0, Top = 0 };
            try
            {
                var thumb = (Thumb)window.FindName(handle);
                var notified = false;
                window.PlacementChanged += (_, _) => notified = true;
                thumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
                thumb.RaiseEvent(new DragDeltaEventArgs(dx, dy) { RoutedEvent = Thumb.DragDeltaEvent });
                thumb.RaiseEvent(new DragCompletedEventArgs(dx, dy, false) { RoutedEvent = Thumb.DragCompletedEvent });

                Assert.Equal(width, window.CustomWidth);
                Assert.Equal(height, window.CustomHeight);
                Assert.Equal(SizeToContent.Manual, window.SizeToContent);
                Assert.True(notified);
                Assert.False(window.IsEditing);
            }
            finally { window.CloseWithoutSaving(); }
        });
    }

    [Fact]
    public void RestoredSize_FillsWindowAndSurvivesEditingAndExternalRefresh()
    {
        WpfTestHost.Run(() =>
        {
            var note = new Note { Title = "크기 유지", Body = "본문" };
            var window = new FloatingNoteWindow(note);
            try
            {
                window.RestoreSize(480, 320);
                var card = (NoteTab)window.FindName("NoteView");
                var root = (FrameworkElement)window.Content;
                root.Measure(new Size(window.Width, window.Height));
                root.Arrange(new Rect(0, 0, window.Width, window.Height));
                Assert.Equal(464, card.ActualWidth);
                Assert.Equal(304, card.ActualHeight);
                Assert.False(card.HasAnimatedProperties);

                card.BeginEdit(false);
                ((TextBox)card.FindName("BodyBox")).Text = string.Join('\n', Enumerable.Repeat("긴 본문", 100));
                window.EndEdit();
                window.RefreshNote(new Note { Id = note.Id, Title = note.Title, Body = "외부 변경" });
                WpfTestHost.Flush();

                Assert.Equal(480, window.CustomWidth);
                Assert.Equal(320, window.CustomHeight);
                Assert.True(double.IsNaN(card.Height));
                Assert.False(card.HasAnimatedProperties);
                Assert.True(card.IsExpanded);
            }
            finally { window.CloseWithoutSaving(); }
        });
    }

    [Fact]
    public void RestoreSize_IgnoresMissingOrInvalidSizeAndClampsSmallSize()
    {
        WpfTestHost.Run(() =>
        {
            var window = new FloatingNoteWindow(new Note());
            try
            {
                foreach (var size in new (double?, double?)[]
                    { (null, null), (400, null), (double.NaN, 300), (400, double.PositiveInfinity), (-1, 300), (400, 0) })
                {
                    window.RestoreSize(size.Item1, size.Item2);
                    Assert.Null(window.CustomWidth);
                    Assert.Null(window.CustomHeight);
                    Assert.Equal(SizeToContent.WidthAndHeight, window.SizeToContent);
                }
                window.RestoreSize(1, 1);
                Assert.Equal(FloatingNoteGeometry.MinWidth, window.CustomWidth);
                Assert.Equal(FloatingNoteGeometry.MinHeight, window.CustomHeight);
            }
            finally { window.CloseWithoutSaving(); }
        });
    }

    [Fact]
    public void FloatingWindow_SavesOnDeactivationKeepsExpandedAndDiscardsDeletedDraft()
    {
        WpfTestHost.Run(() =>
        {
            FloatingNoteWindow? window = null;
            try
            {
                var note = new Note { Title = "고정 메모 검증", Body = "기존 본문" };
                window = new FloatingNoteWindow(note);
                var card = (NoteTab)window.FindName("NoteView");
                WpfTestHost.Flush();

                Assert.True(window.Topmost);
                Assert.False(window.ShowInTaskbar);
                Assert.True(card.IsExpanded);
                Assert.Equal(0, card.RenderTransform.Value.OffsetX);

                var unpinRequested = false;
                window.UnpinRequested += (_, _) => unpinRequested = true;
                card.RaiseEvent(new RoutedEventArgs(NoteTab.PinRequestedEvent, card));
                Assert.True(unpinRequested);

                var saved = false;
                window.EditEnded += (_, e) => saved = e.Saved;
                card.BeginEdit(focusTitle: false);
                ((TextBox)card.FindName("BodyBox")).Text = "저장할 본문";
                typeof(Window).GetMethod("OnDeactivated", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, [EventArgs.Empty]);

                Assert.True(saved);
                Assert.False(window.IsEditing);
                Assert.Equal("저장할 본문", note.Body);
                Assert.True(card.IsExpanded);
                Assert.True(window.Topmost);

                var reloaded = new Note { Id = note.Id, Title = note.Title, Body = "외부에서 바뀐 본문" };
                window.RefreshNote(reloaded);
                Assert.Same(reloaded, window.Note);

                card.BeginEdit(focusTitle: false);
                ((TextBox)card.FindName("BodyBox")).Text = "삭제 시 저장하면 안 되는 본문";
                window.CloseWithoutSaving();
                window = null;

                Assert.False(saved);
                Assert.Equal("외부에서 바뀐 본문", reloaded.Body);
            }
            finally
            {
                window?.CloseWithoutSaving();
            }
        });
    }
}

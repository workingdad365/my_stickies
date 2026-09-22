using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MyStickies.Controls;
using MyStickies.Converters;
using MyStickies.Layout;
using MyStickies.Models;
using MyStickies.Windows;

namespace MyStickies.Tests;

public class FloatingNoteWindowTests
{
    [Fact]
    public void FloatingWindow_SavesOnDeactivationKeepsExpandedAndDiscardsDeletedDraft()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Application? application = null;
            FloatingNoteWindow? window = null;
            try
            {
                application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                application.Resources["HexToBrush"] = new HexToBrushConverter();
                foreach (var key in new[] { "NoteTitleBrush", "NoteBodyBrush", "NoteLabelBrush", "NoteDashBrush" })
                    application.Resources[key] = Brushes.Black;
                FontSettings.Apply(application.Resources, "Malgun Gothic", 14);

                var note = new Note { Title = "고정 메모 검증", Body = "기존 본문" };
                window = new FloatingNoteWindow(note);
                var card = (NoteTab)window.FindName("NoteView");
                Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));

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
            catch (Exception ex) { failure = ex; }
            finally
            {
                window?.CloseWithoutSaving();
                application?.Shutdown();
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "고정 창 회귀 테스트가 제한 시간 안에 끝나야 함");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}

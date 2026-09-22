using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using MyStickies.Controls;
using MyStickies.Models;
using MyStickies.Windows;

namespace MyStickies.Tests;

public class FloatingNoteWindowTests
{
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

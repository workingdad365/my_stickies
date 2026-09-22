using System.Windows;
using MyStickies.Data;
using MyStickies.Layout;
using MyStickies.Models;

namespace MyStickies.Tests;

public class PinnedNoteTests
{
    [Fact]
    public void Restore_ExcludesArchivedDeletedAndOtherDatabaseNotes()
    {
        var active = new Note();
        var archived = new Note { ArchivedAt = DateTime.Now };
        const string database = @"C:\Notes\my_stickies.db";
        var states = new[]
        {
            new PinnedNoteState(database, active.Id, 100, 200),
            new PinnedNoteState(database, archived.Id, 100, 200),
            new PinnedNoteState(database, Guid.NewGuid(), 100, 200),
            new PinnedNoteState(@"D:\Other\my_stickies.db", active.Id, 300, 400),
        };

        var restored = Assert.Single(PinnedNoteState.Restorable(states, database, [active, archived]));
        Assert.Equal(active.Id, restored.NoteId);
        Assert.Equal(100, restored.Left);
        Assert.Equal(200, restored.Top);
    }

    [Fact]
    public void Restore_IgnoresCaseAndDuplicatesButRejectsInvalidCoordinates()
    {
        var note = new Note();
        const string database = @"C:\Notes\my_stickies.db";
        var states = new[]
        {
            new PinnedNoteState(database, note.Id, double.NaN, 200),
            new PinnedNoteState(database.ToUpperInvariant(), note.Id, -500, 100),
            new PinnedNoteState(database, note.Id, 200, 300),
        };

        var restored = Assert.Single(PinnedNoteState.Restorable(states, database, [note]));
        Assert.Equal(-500, restored.Left);
    }

    [Fact]
    public void Position_KeepsNegativeCoordinatesOnConnectedLeftMonitor()
    {
        var result = FloatingNoteGeometry.ClampPosition(-900, 100, 356, 496,
            [new Rect(0, 0, 1920, 1040), new Rect(-1280, 0, 1280, 984)]);
        Assert.Equal(new Point(-900, 100), result);
    }

    [Fact]
    public void Position_ReturnsDisconnectedMonitorWindowToVisibleScreen()
    {
        var area = new Rect(0, 40, 1920, 1000);
        var result = FloatingNoteGeometry.ClampPosition(-900, 1500, 356, 496, [area]);
        Assert.True(area.Contains(new Rect(result, new Size(356, 496))));
    }

    [Fact]
    public void Position_KeepsTopControlsReachableOnSmallScreen()
    {
        var result = FloatingNoteGeometry.ClampPosition(100, 100, 356, 496, [new Rect(0, 0, 320, 300)]);
        Assert.Equal(new Point(0, 0), result);
    }
}

using MyStickies.Localization;

namespace MyStickies.Models;

/// <summary>최초 실행 시 넣어 두는 안내 메모. 사용법을 간단히 설명하며 지워도 무방</summary>
public static class SampleNotes
{
    public static List<Note> Create()
    {
        var t = DateTime.Now;
        return
        [
            new()
            {
                Title = Strings.Get("SampleWelcomeTitle"),
                Body = Strings.Get("SampleWelcomeBody"),
                ColorHex = NotePalette.Blue, CreatedAt = t, UpdatedAt = t,
            },
            new()
            {
                Title = Strings.Get("SampleEditingTitle"),
                Body = Strings.Get("SampleEditingBody"),
                ColorHex = NotePalette.Green, CreatedAt = t.AddSeconds(1), UpdatedAt = t.AddSeconds(1),
            },
            new()
            {
                Title = Strings.Get("SampleOrganizingTitle"),
                Body = Strings.Get("SampleOrganizingBody"),
                ColorHex = NotePalette.Purple, CreatedAt = t.AddSeconds(2), UpdatedAt = t.AddSeconds(2),
            },
            new()
            {
                Title = Strings.Get("SampleSettingsTitle"),
                Body = Strings.Get("SampleSettingsBody"),
                ColorHex = NotePalette.Yellow, CreatedAt = t.AddSeconds(3), UpdatedAt = t.AddSeconds(3),
            },
        ];
    }
}

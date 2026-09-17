using System.Drawing;
using System.IO;
using MyStickies.Data;
using WF = System.Windows.Forms;

namespace MyStickies.Tray;

/// <summary>작업 표시줄 알림 영역 아이콘과 우클릭 메뉴</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly WF.NotifyIcon _icon;
    private readonly Icon _image;
    private readonly WF.ToolStripMenuItem _toggleItem;

    /// <summary>메뉴의 "새 메모" 선택</summary>
    public event Action? AddNoteRequested;

    /// <summary>메뉴의 "메모 관리" 선택</summary>
    public event Action? AllNotesRequested;

    /// <summary>메뉴의 "설정" 선택</summary>
    public event Action? SettingsRequested;

    /// <summary>메뉴의 "감추기" 또는 "보이기" 선택</summary>
    public event Action? ToggleVisibilityRequested;

    /// <summary>메뉴의 "종료" 선택</summary>
    public event Action? ExitRequested;

    /// <summary>아이콘 좌클릭</summary>
    public event Action? Clicked;

    public TrayIcon(Stream iconStream, string tooltip)
    {
        _image = new Icon(iconStream);

        var menu = new WF.ContextMenuStrip();
        menu.Items.Add(new WF.ToolStripMenuItem($"My Stickies {AppInfo.Version}") { Enabled = false });
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add("새 메모", null, (_, _) => AddNoteRequested?.Invoke());
        menu.Items.Add("메모 관리", null, (_, _) => AllNotesRequested?.Invoke());
        menu.Items.Add("설정", null, (_, _) => SettingsRequested?.Invoke());
        _toggleItem = new WF.ToolStripMenuItem("감추기", null, (_, _) => ToggleVisibilityRequested?.Invoke());
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add("종료", null, (_, _) => ExitRequested?.Invoke());

        _icon = new WF.NotifyIcon
        {
            Icon = _image,
            Text = tooltip,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == WF.MouseButtons.Left)
                Clicked?.Invoke();
        };
    }

    /// <summary>덱이 감춰진 상태에 맞춰 메뉴 문구를 "보이기" 또는 "감추기"로 바꿈</summary>
    public void SetHidden(bool hidden) => _toggleItem.Text = hidden ? "보이기" : "감추기";

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _image.Dispose();
    }
}

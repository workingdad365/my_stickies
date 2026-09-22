using System.Drawing;
using System.IO;
using MyStickies.Data;
using MyStickies.Localization;
using WF = System.Windows.Forms;

namespace MyStickies.Tray;

/// <summary>작업 표시줄 알림 영역 아이콘과 우클릭 메뉴</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly WF.NotifyIcon _icon;
    private readonly Icon _image;
    private readonly WF.ToolStripMenuItem _toggleItem;
    private readonly WF.ToolStripMenuItem _updateItem;
    private readonly List<(WF.ToolStripMenuItem Item, string Key)> _translatedItems = [];
    private bool _hidden;
    private string? _updateVersion;

    /// <summary>메뉴의 "새 메모" 선택</summary>
    public event Action? AddNoteRequested;

    /// <summary>메뉴의 "메모 관리" 선택</summary>
    public event Action? AllNotesRequested;

    /// <summary>메뉴의 "설정" 선택</summary>
    public event Action? SettingsRequested;

    /// <summary>메뉴의 "감추기" 또는 "보이기" 선택</summary>
    public event Action? ToggleVisibilityRequested;

    /// <summary>메뉴의 "업데이트 확인" 또는 "업데이트 설치" 선택, 또는 업데이트 풍선 알림 클릭</summary>
    public event Action? UpdateRequested;

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
        menu.Items.Add(CreateItem("NewNote", () => AddNoteRequested?.Invoke()));
        menu.Items.Add(CreateItem("ManageNotes", () => AllNotesRequested?.Invoke()));
        menu.Items.Add(CreateItem("Settings", () => SettingsRequested?.Invoke()));
        _toggleItem = new WF.ToolStripMenuItem(Strings.Get("HideDeck"), null, (_, _) => ToggleVisibilityRequested?.Invoke());
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new WF.ToolStripSeparator());
        _updateItem = new WF.ToolStripMenuItem(Strings.Get("CheckUpdates"), null, (_, _) => UpdateRequested?.Invoke());
        menu.Items.Add(_updateItem);
        menu.Items.Add(new WF.ToolStripSeparator());
        menu.Items.Add(CreateItem("Exit", () => ExitRequested?.Invoke()));

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
        _icon.BalloonTipClicked += (_, _) => UpdateRequested?.Invoke();
        Strings.LanguageChanged += RefreshLanguage;
    }

    private WF.ToolStripMenuItem CreateItem(string key, Action action)
    {
        var item = new WF.ToolStripMenuItem(Strings.Get(key), null, (_, _) => action());
        _translatedItems.Add((item, key));
        return item;
    }

    private void RefreshLanguage()
    {
        foreach (var (item, key) in _translatedItems) item.Text = Strings.Get(key);
        _toggleItem.Text = Strings.Get(_hidden ? "ShowDeck" : "HideDeck");
        _updateItem.Text = _updateVersion is null ? Strings.Get("CheckUpdates") : Strings.Get("InstallUpdate", _updateVersion);
    }

    /// <summary>새 버전이 대기 중이면 메뉴 문구를 "업데이트 vX 설치..."로, 없으면 "업데이트 확인..."으로</summary>
    public void SetUpdateAvailable(string? version)
    {
        _updateVersion = version;
        _updateItem.Text = version is null ? Strings.Get("CheckUpdates") : Strings.Get("InstallUpdate", version);
        _updateItem.Font = version is null
            ? null
            : new Font(_updateItem.Font ?? WF.Control.DefaultFont, FontStyle.Bold);
    }

    /// <summary>알림 영역 풍선 알림. 클릭하면 UpdateRequested 발생</summary>
    public void ShowBalloon(string title, string text) =>
        _icon.ShowBalloonTip(8000, title, text, WF.ToolTipIcon.Info);

    /// <summary>덱이 감춰진 상태에 맞춰 메뉴 문구를 "보이기" 또는 "감추기"로 바꿈</summary>
    public void SetHidden(bool hidden)
    {
        _hidden = hidden;
        _toggleItem.Text = Strings.Get(hidden ? "ShowDeck" : "HideDeck");
    }

    public void Dispose()
    {
        Strings.LanguageChanged -= RefreshLanguage;
        _icon.Visible = false;
        _icon.Dispose();
        _image.Dispose();
    }
}

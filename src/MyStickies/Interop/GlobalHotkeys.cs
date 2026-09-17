using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace MyStickies.Interop;

/// <summary>전역 단축키 등록. WM_HOTKEY를 받아 이벤트로 전달</summary>
internal sealed class GlobalHotkeys : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_NOREPEAT = 0x4000;

    private const int ToggleDeckId = 0x5A01;
    private const int NewNoteId = 0x5A02;
    private const uint VK_S = 0x53;
    private const uint VK_N = 0x4E;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private readonly IntPtr _hwnd;
    private bool _registered;

    /// <summary>Ctrl+Alt+S</summary>
    public event Action? ToggleDeck;

    /// <summary>Ctrl+Alt+N</summary>
    public event Action? NewNote;

    /// <summary>등록에 실패한 단축키 설명. 모두 성공하면 빈 목록</summary>
    public List<string> Failed { get; } = [];

    public const string Description = "Ctrl+Alt+S 덱 열기/닫기, Ctrl+Alt+N 새 메모";

    public GlobalHotkeys(HwndSource source)
    {
        _source = source;
        _hwnd = source.Handle;
        _source.AddHook(WndProc);
    }

    /// <summary>단축키 등록. 다른 앱이 이미 쓰는 조합은 Failed에 기록</summary>
    public void Register()
    {
        if (_registered) return;
        _registered = true;
        Failed.Clear();

        if (!RegisterHotKey(_hwnd, ToggleDeckId, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_S))
            Failed.Add("Ctrl+Alt+S");
        if (!RegisterHotKey(_hwnd, NewNoteId, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_N))
            Failed.Add("Ctrl+Alt+N");
    }

    public void Unregister()
    {
        if (!_registered) return;
        _registered = false;
        UnregisterHotKey(_hwnd, ToggleDeckId);
        UnregisterHotKey(_hwnd, NewNoteId);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_HOTKEY) return IntPtr.Zero;

        switch (wParam.ToInt32())
        {
            case ToggleDeckId:
                ToggleDeck?.Invoke();
                handled = true;
                break;
            case NewNoteId:
                NewNote?.Invoke();
                handled = true;
                break;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
    }
}

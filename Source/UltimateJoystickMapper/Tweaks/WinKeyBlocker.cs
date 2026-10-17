using System.Runtime.InteropServices;
using UltimateJoystickMapper.Diagnostics;
using UltimateJoystickMapper.Output;

namespace UltimateJoystickMapper.Tweaks;

/// <summary>
/// Swallows the Windows keys while the target game is the focused window, so a
/// stray press cannot drop the player to the desktop. Everywhere else the keys
/// work normally.
/// The low-level hook lives on its own thread with its own message loop: Windows
/// waits for a low-level hook on every key press system-wide, so it must never
/// share the busy UI thread.
/// </summary>
public sealed class WinKeyBlocker : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100, WmKeyUp = 0x0101, WmSysKeyDown = 0x0104, WmSysKeyUp = 0x0105;
    private const int WmQuit = 0x0012;
    private const uint VkLWin = 0x5B, VkRWin = 0x5C;

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct { public uint VkCode, ScanCode, Flags, Time; public IntPtr ExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Msg { public IntPtr Hwnd; public uint Message; public IntPtr WParam, LParam; public uint Time; public int X, Y; }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern int GetMessage(out Msg msg, IntPtr hwnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);

    private readonly ForegroundGate _gate;
    private readonly HookProc _proc; // kept alive so the GC never collects the callback
    private Thread? _thread;
    private uint _threadId;
    private IntPtr _hook;
    private bool _leftBlocked, _rightBlocked;

    public WinKeyBlocker(string targetProcessName)
    {
        _gate = new ForegroundGate(targetProcessName);
        _proc = OnKey;
    }

    public bool IsActive => _thread is not null;

    public void Start()
    {
        if (_thread is not null) return;
        using var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            _hook = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(null), 0);
            if (_hook == IntPtr.Zero) Logger.Warn($"Windows key lock could not be installed (error {Marshal.GetLastWin32Error()})");
            ready.Set();
            while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }
            if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }) { IsBackground = true, Name = "Windows key lock", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
        ready.Wait(2000);
    }

    public void Stop()
    {
        if (_thread is null) return;
        PostThreadMessage(_threadId, WmQuit, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(2000);
        _thread = null;
        _leftBlocked = _rightBlocked = false;
    }

    private IntPtr OnKey(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            if (data.VkCode is VkLWin or VkRWin)
            {
                var message = (int)wParam;
                var isDown = message is WmKeyDown or WmSysKeyDown;
                var isUp = message is WmKeyUp or WmSysKeyUp;
                ref var blocked = ref data.VkCode == VkLWin ? ref _leftBlocked : ref _rightBlocked;
                // A release is swallowed only when its press was, so the key can never stick.
                if (isDown && (blocked || _gate.IsTargetFocused())) { blocked = true; return 1; }
                if (isUp && blocked) { blocked = false; return 1; }
            }
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose() => Stop();
}

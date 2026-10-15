using System.Runtime.InteropServices;
using UltimateJoystickMapper.Diagnostics;

namespace UltimateJoystickMapper.Output;

/// <summary>
/// Some games decide which input device is "active" per-axis: once the GMK's
/// synthetic keyboard/controller output starts moving the character while
/// the physical mouse sits untouched, the game's own device-switching logic
/// (common in engines with combined mouse+gamepad support) can drop mouse
/// look entirely, while menus — which usually read input more generically —
/// keep working. Sending a real but net-zero mouse move (out one pixel, back
/// one pixel) produces a genuine WM_INPUT mouse-delta event that keeps the
/// mouse "alive" from the game's point of view, without visibly moving the
/// cursor. Only fires while movement output is actually happening and the
/// physical mouse has been still for a while, so it never fights a real
/// mouse look.
/// </summary>
public static class MouseKeepAlive
{
    private const uint INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_MOVE = 0x0001;

    [StructLayout(LayoutKind.Sequential)]
    public struct CursorPoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public MouseInput Mouse;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out CursorPoint point);

    public static bool TryGetCursorPos(out CursorPoint point) => GetCursorPos(out point);

    public static void Nudge()
    {
        var inputs = new[]
        {
            new Input { Type = INPUT_MOUSE, Mouse = new MouseInput { Dx = 2, Dy = 2, Flags = MOUSEEVENTF_MOVE } },
            new Input { Type = INPUT_MOUSE, Mouse = new MouseInput { Dx = -2, Dy = -2, Flags = MOUSEEVENTF_MOVE } },
        };
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        // Logged so a freeze can be diagnosed afterwards from the log file:
        // if "sent" is less than inputs.Length, Windows (or something
        // filtering synthetic input, e.g. anti-cheat) rejected the call —
        // that would mean the nudge never reached the game at all, which is
        // a very different problem than the nudge reaching it but not being
        // enough.
        if (sent < inputs.Length) Logger.Warn($"MouseKeepAlive.Nudge: SendInput only accepted {sent}/{inputs.Length} events (GetLastError={Marshal.GetLastWin32Error()})");
    }
}

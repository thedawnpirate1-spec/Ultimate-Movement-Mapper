using System.Runtime.InteropServices;
using System.Windows.Forms;
using UltimateJoystickMapper.Diagnostics;
using UltimateJoystickMapper.Mapping;

namespace UltimateJoystickMapper.Output;

/// <summary>
/// Injects the movement/action keys. Designed so a synthetic key can never
/// stay stuck down in Windows:
/// - Send never throws. SendInput is rejected while the secure desktop is
///   active (lock screen, Ctrl+Alt+Del, UAC) or when an elevated window has
///   focus. Previously that threw, the release in the catch block threw again,
///   and the read loop died with keys still held down system-wide.
/// - A key's tracked state only changes when Windows actually accepted the
///   event, so a failed key-up is retried on the next call instead of being
///   forgotten.
/// - <see cref="HasKeysDown"/> lets a watchdog release everything when the
///   read loop stalls or stops.
/// </summary>
public sealed class KeyboardOutput : IDisposable
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventScanCode = 0x0008;
    private const uint MapVkToVscEx = 4;

    private readonly object _stateLock = new();
    private bool _forwardDown;
    private bool _leftDown;
    private bool _backwardDown;
    private bool _rightDown;
    private bool _actionDown;
    private Keys _actionKey = Keys.None;
    private Keys _forwardKey = Keys.O;
    private Keys _leftKey = Keys.K;
    private Keys _backwardKey = Keys.L;
    private Keys _rightKey = Keys.OemSemicolon;
    private int _consecutiveFailures;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    // INPUT's native union is 32 bytes on 64-bit Windows because MOUSEINPUT is
    // larger than KEYBDINPUT. Fixing the union size makes sizeof(INPUT) 40.
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    public bool HasKeysDown
    {
        get { lock (_stateLock) return _forwardDown || _leftDown || _backwardDown || _rightDown || _actionDown; }
    }

    public void Apply(MovementKeys desired, Keys forwardKey, Keys leftKey, Keys backwardKey, Keys rightKey)
    {
        lock (_stateLock)
        {
            SetMappedState(ref _forwardKey, ref _forwardDown, forwardKey, desired.Forward);
            SetMappedState(ref _leftKey, ref _leftDown, leftKey, desired.Left);
            SetMappedState(ref _backwardKey, ref _backwardDown, backwardKey, desired.Backward);
            SetMappedState(ref _rightKey, ref _rightDown, rightKey, desired.Right);
        }
    }

    private void SetMappedState(ref Keys activeKey, ref bool current, Keys requestedKey, bool desired)
    {
        if (current && activeKey != requestedKey)
        {
            // Keep tracking the old key until its release is accepted.
            if (!Send(activeKey, keyUp: true)) return;
            current = false;
        }

        activeKey = requestedKey;
        SetState(activeKey, ref current, desired);
    }

    public void ApplyAction(Keys key, bool pressed)
    {
        lock (_stateLock)
        {
            if (_actionDown && _actionKey != key)
            {
                if (!Send(_actionKey, keyUp: true)) return;
                _actionDown = false;
            }

            _actionKey = key;
            SetState(key, ref _actionDown, pressed);
        }
    }

    public void ReleaseAllInputs()
    {
        lock (_stateLock)
        {
            SetState(_forwardKey, ref _forwardDown, false);
            SetState(_leftKey, ref _leftDown, false);
            SetState(_backwardKey, ref _backwardDown, false);
            SetState(_rightKey, ref _rightDown, false);
            SetState(_actionKey, ref _actionDown, false);
        }
    }

    private void SetState(Keys key, ref bool current, bool desired)
    {
        if (current == desired) return;
        if (key == Keys.None)
        {
            // An unbound key has nothing to press; scan code 0 must never be injected.
            current = false;
            return;
        }
        if (Send(key, keyUp: !desired)) current = desired;
    }

    private bool Send(Keys key, bool keyUp)
    {
        var scanCode = MapVirtualKey((uint)(key & Keys.KeyCode), MapVkToVscEx);
        if (scanCode == 0) return true; // Not a physical key; treat as handled so it is never retried forever.

        var flags = KeyEventScanCode | (keyUp ? KeyEventKeyUp : 0u);
        // Arrow keys, Insert/Delete/Home/End/PgUp/PgDn, right Ctrl/Alt etc.
        // have an 0xE0 prefix. Without the extended flag Windows sees the
        // numpad key instead, and a down/up pair can end up on different keys.
        // MapVirtualKey does not report the prefix for arrow keys (VK_UP -> 0x48,
        // the Numpad 8 code), so those keys are also matched explicitly.
        if ((scanCode & 0xFF00) == 0xE000 || IsExtendedKey(key)) flags |= KeyEventExtendedKey;

        var input = new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = 0,
                    ScanCode = (ushort)(scanCode & 0xFF),
                    Flags = flags,
                    Time = 0,
                    ExtraInfo = IntPtr.Zero
                }
            }
        };

        if (SendInput(1, [input], Marshal.SizeOf<Input>()) == 1)
        {
            if (_consecutiveFailures > 0)
            {
                Logger.Info($"Keyboard output accepted again after {_consecutiveFailures} rejected events");
                _consecutiveFailures = 0;
            }
            return true;
        }

        // Log only the first rejection of a streak so a locked screen doesn't flood the log.
        if (_consecutiveFailures++ == 0)
            Logger.Warn($"SendInput rejected {key} {(keyUp ? "up" : "down")} (GetLastError={Marshal.GetLastWin32Error()}); retrying until Windows accepts it");
        return false;
    }

    private static bool IsExtendedKey(Keys key) => (key & Keys.KeyCode) switch
    {
        Keys.Up or Keys.Down or Keys.Left or Keys.Right or
        Keys.Insert or Keys.Delete or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or
        Keys.RControlKey or Keys.RMenu or Keys.Divide or Keys.LWin or Keys.RWin or Keys.Apps => true,
        _ => false
    };

    public void Dispose() => ReleaseAllInputs();
}

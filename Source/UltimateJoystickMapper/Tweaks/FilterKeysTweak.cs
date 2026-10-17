using System.Runtime.InteropServices;

namespace UltimateJoystickMapper.Tweaks;

/// <summary>
/// Windows Filter Keys allows a shorter key-repeat delay and a faster repeat
/// rate than the normal keyboard settings. Changes are applied for the current
/// session only (no SPIF_UPDATEINIFILE), so a crash can never make them stick
/// beyond the next sign-in; the original values are restored explicitly.
/// This only affects physical keyboards: the mapper's injected movement keys
/// are held without auto-repeat.
/// </summary>
public static class FilterKeysTweak
{
    private const uint SpiGetFilterKeys = 0x0032;
    private const uint SpiSetFilterKeys = 0x0033;
    private const uint SpifSendChange = 0x0002;

    private const uint FilterKeysOn = 0x0001;
    private const uint FilterKeysAvailable = 0x0002;
    private const uint HotkeyActive = 0x0004;
    private const uint ConfirmHotkey = 0x0008;
    private const uint Indicator = 0x0020;
    private const uint ClickOn = 0x0040;

    [StructLayout(LayoutKind.Sequential)]
    private struct FilterKeys
    {
        public uint Size;
        public uint Flags;
        public uint WaitMs;
        public uint DelayMs;
        public uint RepeatMs;
        public uint BounceMs;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, ref FilterKeys value, uint winIni);

    // Below these values a normal key press already repeats, so typing produces
    // duplicated letters (for example 10 ms / 1 ms).
    public const int MinDelayMs = 100;
    public const int MinRepeatMs = 10;

    public static FilterKeysState Read()
    {
        var value = new FilterKeys { Size = (uint)Marshal.SizeOf<FilterKeys>() };
        if (!SystemParametersInfo(SpiGetFilterKeys, value.Size, ref value, 0))
            throw new InvalidOperationException($"Reading Filter Keys failed (error {Marshal.GetLastWin32Error()}).");
        return new FilterKeysState(value.Flags, value.WaitMs, value.DelayMs, value.RepeatMs, value.BounceMs);
    }

    public static void Apply(int delayMs, int repeatMs)
    {
        var current = Read();
        // No acceptance delay or bounce time, otherwise short taps would be ignored.
        // The Shift-hold hotkey, its confirmation and the tray indicator are switched off
        // so holding Shift in game cannot toggle Filter Keys or show a dialog.
        var flags = (current.Flags & ~(HotkeyActive | ConfirmHotkey | Indicator | ClickOn)) | FilterKeysOn | FilterKeysAvailable;
        Write(new FilterKeysState(flags, 0, (uint)Math.Clamp(delayMs, MinDelayMs, 2000), (uint)Math.Clamp(repeatMs, MinRepeatMs, 500), 0));
    }

    public static void Restore(FilterKeysState original) => Write(original);

    public static bool IsOn(FilterKeysState state) => (state.Flags & FilterKeysOn) != 0;

    private static void Write(FilterKeysState state)
    {
        var value = new FilterKeys
        {
            Size = (uint)Marshal.SizeOf<FilterKeys>(),
            Flags = state.Flags,
            WaitMs = state.WaitMs,
            DelayMs = state.DelayMs,
            RepeatMs = state.RepeatMs,
            BounceMs = state.BounceMs
        };
        if (!SystemParametersInfo(SpiSetFilterKeys, value.Size, ref value, SpifSendChange))
            throw new InvalidOperationException($"Changing Filter Keys failed (error {Marshal.GetLastWin32Error()}).");
    }
}

public sealed record FilterKeysState(uint Flags, uint WaitMs, uint DelayMs, uint RepeatMs, uint BounceMs);

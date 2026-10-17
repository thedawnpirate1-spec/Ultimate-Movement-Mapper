using System.Runtime.InteropServices;

namespace UltimateJoystickMapper.Tweaks;

/// <summary>
/// Switches off the Windows accessibility shortcuts that open a dialog in the
/// middle of a game: Sticky Keys (Shift five times), Toggle Keys (hold NumLock)
/// and the Filter Keys shortcut (hold right Shift). Only the shortcut flags are
/// touched, never whether the features themselves are on. Changes are session-only.
/// </summary>
public static class AccessibilityHotkeys
{
    private const uint SpiGetFilterKeys = 0x0032, SpiSetFilterKeys = 0x0033;
    private const uint SpiGetToggleKeys = 0x0034, SpiSetToggleKeys = 0x0035;
    private const uint SpiGetStickyKeys = 0x003A, SpiSetStickyKeys = 0x003B;
    private const uint SpifSendChange = 0x0002;
    // HOTKEYACTIVE and CONFIRMHOTKEY share these values in STICKYKEYS, TOGGLEKEYS and FILTERKEYS.
    private const uint ShortcutFlags = 0x0004 | 0x0008;

    [StructLayout(LayoutKind.Sequential)]
    private struct FlagsOnly { public uint Size; public uint Flags; }

    [StructLayout(LayoutKind.Sequential)]
    private struct FilterKeys { public uint Size, Flags, WaitMs, DelayMs, RepeatMs, BounceMs; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, ref FlagsOnly value, uint winIni);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, ref FilterKeys value, uint winIni);

    /// <summary>Current shortcut bits of Sticky, Toggle and Filter Keys.</summary>
    public static AccessibilityShortcutState Read() => new(
        ReadFlags(SpiGetStickyKeys) & ShortcutFlags,
        ReadFlags(SpiGetToggleKeys) & ShortcutFlags,
        ReadFilter().Flags & ShortcutFlags);

    public static bool AnyActive(AccessibilityShortcutState state) =>
        (state.Sticky | state.Toggle | state.Filter) != 0;

    public static void DisableShortcuts() => Write(new AccessibilityShortcutState(0, 0, 0));

    public static void Restore(AccessibilityShortcutState original) => Write(original);

    private static void Write(AccessibilityShortcutState shortcuts)
    {
        WriteFlags(SpiGetStickyKeys, SpiSetStickyKeys, shortcuts.Sticky);
        WriteFlags(SpiGetToggleKeys, SpiSetToggleKeys, shortcuts.Toggle);

        var filter = ReadFilter();
        filter.Flags = (filter.Flags & ~ShortcutFlags) | (shortcuts.Filter & ShortcutFlags);
        if (!SystemParametersInfo(SpiSetFilterKeys, filter.Size, ref filter, SpifSendChange))
            throw new InvalidOperationException($"Changing the Filter Keys shortcut failed (error {Marshal.GetLastWin32Error()}).");
    }

    private static uint ReadFlags(uint getAction)
    {
        var value = new FlagsOnly { Size = (uint)Marshal.SizeOf<FlagsOnly>() };
        if (!SystemParametersInfo(getAction, value.Size, ref value, 0))
            throw new InvalidOperationException($"Reading accessibility settings failed (error {Marshal.GetLastWin32Error()}).");
        return value.Flags;
    }

    private static void WriteFlags(uint getAction, uint setAction, uint shortcutBits)
    {
        var value = new FlagsOnly { Size = (uint)Marshal.SizeOf<FlagsOnly>(), Flags = ReadFlags(getAction) };
        value.Flags = (value.Flags & ~ShortcutFlags) | (shortcutBits & ShortcutFlags);
        if (!SystemParametersInfo(setAction, value.Size, ref value, SpifSendChange))
            throw new InvalidOperationException($"Changing accessibility settings failed (error {Marshal.GetLastWin32Error()}).");
    }

    private static FilterKeys ReadFilter()
    {
        var value = new FilterKeys { Size = (uint)Marshal.SizeOf<FilterKeys>() };
        if (!SystemParametersInfo(SpiGetFilterKeys, value.Size, ref value, 0))
            throw new InvalidOperationException($"Reading Filter Keys failed (error {Marshal.GetLastWin32Error()}).");
        return value;
    }
}

public sealed record AccessibilityShortcutState(uint Sticky, uint Toggle, uint Filter);

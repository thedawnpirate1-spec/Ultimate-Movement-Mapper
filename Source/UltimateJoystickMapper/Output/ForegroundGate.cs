using System.Diagnostics;
using System.Runtime.InteropServices;

namespace UltimateJoystickMapper.Output;

/// <summary>
/// Answers "is the target game the focused window?" so synthetic keys only
/// reach the game. Without this gate the movement keys were typed into
/// whatever had focus — browser, chat, Discord — whenever the stick moved or
/// drifted past the dead-zone.
/// The answer is cached for a short interval because the read loop runs at
/// polling rate and a process lookup per sample would be wasteful.
/// </summary>
public sealed class ForegroundGate
{
    private const long CacheMs = 150;

    private readonly string _targetProcessName;
    private long _lastCheckTick;
    private bool _lastResult;
    private uint _lastProcessId;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    public ForegroundGate(string? targetProcessName)
    {
        // Accept "Game.exe" as well as "Game"; empty means "send everywhere" (old behavior).
        var name = (targetProcessName ?? string.Empty).Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        _targetProcessName = name;
    }

    public bool IsTargetFocused()
    {
        if (_targetProcessName.Length == 0) return true;

        var now = Environment.TickCount64;
        if (now - _lastCheckTick < CacheMs) return _lastResult;
        _lastCheckTick = now;
        _lastResult = QueryForeground();
        return _lastResult;
    }

    private bool QueryForeground()
    {
        try
        {
            var window = GetForegroundWindow();
            if (window == IntPtr.Zero) return false;
            GetWindowThreadProcessId(window, out var processId);
            if (processId == 0) return false;
            // Process.GetProcessById snapshots every process on the system, so only
            // resolve the name when focus actually moved to a different process.
            if (processId == _lastProcessId) return _lastResult;
            _lastProcessId = processId;
            using var process = Process.GetProcessById((int)processId);
            return string.Equals(process.ProcessName, _targetProcessName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Process exited between the two calls, or access denied — treat as "not the game".
            return false;
        }
    }
}

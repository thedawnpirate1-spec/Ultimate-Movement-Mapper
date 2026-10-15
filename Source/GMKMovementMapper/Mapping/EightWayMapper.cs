namespace GMKMovementMapper.Mapping;

public readonly record struct StickSample(double X, double Y);

public readonly record struct MovementKeys(bool Forward, bool Left, bool Backward, bool Right)
{
    public static MovementKeys None => new(false, false, false, false);
}

public sealed class EightWayMapper
{
    // A raw stick rarely sits at a mathematically perfect angle when pushed
    // to full deflection — there's always a little mechanical noise on the
    // other axis. If that noise straddles a 45° sector boundary, the sector
    // computed from the raw angle alone flips back and forth every frame,
    // which sends rapid, physically-impossible alternating key presses
    // (e.g. W / W+D / W / W+D...). Games' anti-cheat can — and apparently
    // does, for at least one user — react to that pattern by suspending
    // input. Hysteresis fixes it at the source: once a sector is selected,
    // the angle has to move solidly past its boundary (not just touch it)
    // before the sector is allowed to change, so boundary-straddling noise
    // no longer flips the output.
    private const double HysteresisDegrees = 6.0;

    private readonly MovementProfile _profile;
    private int? _lastSector;

    public EightWayMapper(MovementProfile profile) => _profile = profile;

    public MovementKeys Map(StickSample input)
    {
        var x = Math.Clamp(input.X, -1.0, 1.0);
        var y = Math.Clamp(input.Y, -1.0, 1.0);
        if (_profile.InvertY) y = -y;

        var magnitude = Math.Sqrt(x * x + y * y);
        if (magnitude <= _profile.Deadzone)
        {
            _lastSector = null;
            return MovementKeys.None;
        }

        // Radial dead-zone compensation keeps the first usable movement responsive while
        // retaining a full-strength output at the edge of the stick.
        var usable = Math.Clamp((magnitude - _profile.Deadzone) /
                                Math.Max(0.001, _profile.MaxZone - _profile.Deadzone), 0.0, 1.0);
        if (usable <= 0.0)
        {
            _lastSector = null;
            return MovementKeys.None;
        }

        var angle = Math.Atan2(y, x) * 180.0 / Math.PI;
        if (angle < 0) angle += 360.0;

        var sector = SelectSector(angle);
        return sector switch
        {
            0 => new MovementKeys(false, false, false, true),   // east
            1 => new MovementKeys(true, false, false, true),    // north-east
            2 => new MovementKeys(true, false, false, false),   // north / W
            3 => new MovementKeys(true, true, false, false),    // north-west
            4 => new MovementKeys(false, true, false, false),   // west / A
            5 => new MovementKeys(false, true, true, false),    // south-west
            6 => new MovementKeys(false, false, true, false),   // south / S
            _ => new MovementKeys(false, false, true, true)     // south-east
        };
    }

    private int SelectSector(double angle)
    {
        // Fixed, equal 45-degree sectors: four cardinal directions and four diagonals.
        var raw = NormalizeSector((int)Math.Floor((angle + 22.5) / 45.0));

        if (_lastSector is int last && Math.Abs(AngleDifference(angle, last * 45.0)) <= 22.5 + HysteresisDegrees)
        {
            return last; // still within the sticky zone around the current sector — keep it
        }

        _lastSector = raw;
        return raw;
    }

    private static double AngleDifference(double angle, double center)
    {
        var diff = (angle - center + 540.0) % 360.0 - 180.0; // normalized to (-180, 180]
        return diff;
    }

    private static int NormalizeSector(int value)
    {
        value %= 8;
        return value < 0 ? value + 8 : value;
    }

}

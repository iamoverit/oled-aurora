using System;

namespace OledAurora.Core;

// A monotonic clock prevents wall-clock corrections from delaying or repeating a cycle.
// Keyboard and mouse activity deliberately do not affect this schedule.
public sealed class CycleSchedule
{
    private TimeSpan _interval;
    private TimeSpan _next;

    public CycleSchedule(TimeSpan now, TimeSpan interval) => Restart(now, interval);

    public void Restart(TimeSpan now, TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(interval));
        _interval = interval;
        _next = now + interval;
    }

    public bool Tick(TimeSpan now, bool blocked)
    {
        if (blocked) { _next = now + _interval; return false; }
        if (now < _next) return false;
        _next = now + _interval;
        return true;
    }

    public TimeSpan Remaining(TimeSpan now) => _next > now ? _next - now : TimeSpan.Zero;
}

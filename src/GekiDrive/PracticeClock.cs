using System;

namespace GekiDrive
{
    // Source milliseconds, independent of render FPS. Changing rate preserves position.
    internal sealed class PracticeClock
    {
        private double source, anchor, rate = 1;
        private bool paused;
        internal double Position(double now) { return source + (paused ? 0 : Math.Max(0, now - anchor) * rate); }
        internal void Start(double position, double now, double speed) { source = position; anchor = now; rate = Clamp(speed); paused = false; }
        internal void SetSpeed(double speed, double now) { source = Position(now); anchor = now; rate = Clamp(speed); }
        internal void SetPaused(bool value, double now) { source = Position(now); anchor = now; paused = value; }
        internal static double Clamp(double speed) { return double.IsNaN(speed) || double.IsInfinity(speed) ? 1 : Math.Max(0.5, Math.Min(2, speed)); }
    }
}

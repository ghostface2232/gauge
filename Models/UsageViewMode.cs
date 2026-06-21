namespace Gauge.Models;

/// <summary>
/// How each tool card renders its usage windows. <see cref="Bar"/> is the horizontal
/// progress-bar row (the original layout); <see cref="Gauge"/> draws a circular gauge per
/// window. App-wide — chosen once in settings and applied to every card.
/// </summary>
public enum UsageViewMode
{
    Bar = 0,
    Gauge = 1,
}

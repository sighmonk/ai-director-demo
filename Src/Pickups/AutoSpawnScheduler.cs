using System;

/// <summary>
/// Pure gate for ambient health pack spawning: enforces the active-pack cap and the spawn interval
/// without needing a scene tree.
/// </summary>
public sealed class AutoSpawnScheduler
{
    private double _nextAllowedTime;

    public AutoSpawnScheduler(int maxActive, double intervalSeconds, double firstSpawnDelaySeconds = 0)
    {
        MaxActive = Math.Max(0, maxActive);
        IntervalSeconds = Math.Max(0, intervalSeconds);
        _nextAllowedTime = Math.Max(0, firstSpawnDelaySeconds);
    }

    public int MaxActive { get; }

    public double IntervalSeconds { get; }

    public double NextAllowedTime => _nextAllowedTime;

    public bool ShouldSpawn(double now, int activeCount)
        => activeCount < MaxActive && now >= _nextAllowedTime;

    public void NotifySpawned(double now) => _nextAllowedTime = now + IntervalSeconds;
}

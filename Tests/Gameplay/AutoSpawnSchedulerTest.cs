using GdUnit4;
using static GdUnit4.Assertions;

namespace ProjectKitchen.Tests.Gameplay;

/// <summary>
/// Cap and interval gate used by the ambient health pack spawner.
/// </summary>
[TestSuite]
public partial class AutoSpawnSchedulerTest
{
    [TestCase]
    public void ShouldSpawn_BeforeFirstDelay_ReturnsFalse()
    {
        var scheduler = new AutoSpawnScheduler(maxActive: 3, intervalSeconds: 15, firstSpawnDelaySeconds: 10);

        AssertBool(scheduler.ShouldSpawn(now: 9.9, activeCount: 0)).IsFalse();
        AssertBool(scheduler.ShouldSpawn(now: 10.0, activeCount: 0)).IsTrue();
    }

    [TestCase]
    public void ShouldSpawn_AtCap_ReturnsFalse()
    {
        var scheduler = new AutoSpawnScheduler(maxActive: 2, intervalSeconds: 5);

        AssertBool(scheduler.ShouldSpawn(now: 100, activeCount: 1)).IsTrue();
        AssertBool(scheduler.ShouldSpawn(now: 100, activeCount: 2)).IsFalse();
        AssertBool(scheduler.ShouldSpawn(now: 100, activeCount: 3)).IsFalse();
    }

    [TestCase]
    public void ShouldSpawn_ZeroCap_NeverSpawns()
    {
        var scheduler = new AutoSpawnScheduler(maxActive: 0, intervalSeconds: 1);

        AssertBool(scheduler.ShouldSpawn(now: 1000, activeCount: 0)).IsFalse();
    }

    [TestCase]
    public void NotifySpawned_DelaysTheNextSpawnByTheInterval()
    {
        var scheduler = new AutoSpawnScheduler(maxActive: 5, intervalSeconds: 15);

        scheduler.NotifySpawned(now: 20);

        AssertFloat((float)scheduler.NextAllowedTime).IsEqual(35f);
        AssertBool(scheduler.ShouldSpawn(now: 34.9, activeCount: 1)).IsFalse();
        AssertBool(scheduler.ShouldSpawn(now: 35.0, activeCount: 1)).IsTrue();
    }

    [TestCase]
    public void ShouldSpawn_AfterAPackIsCollected_AllowsSpawningAgain()
    {
        var scheduler = new AutoSpawnScheduler(maxActive: 1, intervalSeconds: 10);

        scheduler.NotifySpawned(now: 10);
        AssertBool(scheduler.ShouldSpawn(now: 25, activeCount: 1)).IsFalse();
        AssertBool(scheduler.ShouldSpawn(now: 25, activeCount: 0)).IsTrue();
    }
}

using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Ambient health pack spawning plus a spawn entry point for Director actions. Placement only uses
/// floor tiles that are reachable from the player.
/// </summary>
public partial class PickupManager : Node
{
    public static PickupManager Instance => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<PickupManager>("PickupManager");

    [ExportCategory("Health Packs")]
    [Export] public PackedScene? HealthPackScene;
    [Export] public int MaxActiveHealthPacks = 3;
    [Export] public float SpawnInterval = 15f;
    [Export] public float FirstSpawnDelay = 10f;
    [Export] public bool AmbientSpawningEnabled = true;

    [ExportGroup("Placement")]
    [Export] public float MinDistanceFromPlayer = 200f;
    [Export] public float MaxDistanceFromPlayer = 500f;
    [Export] public int PlacementAttempts = 30;
    /// <summary>Seed for placement sampling (0 = time-based).</summary>
    [Export] public ulong Seed = 0;

    private readonly List<HealthPack> _active = new List<HealthPack>();
    private AutoSpawnScheduler _scheduler = null!;
    private Random _random = new Random();
    private double _clock;

    public int ActiveCount
    {
        get
        {
            Prune();
            return _active.Count;
        }
    }

    public IReadOnlyList<HealthPack> ActiveHealthPacks
    {
        get
        {
            Prune();
            return _active;
        }
    }

    public override void _Ready()
    {
        ulong seed = CalibrationOptions.Current != null ? (ulong)CalibrationOptions.Current.Seed : Seed;
        _random = seed != 0 ? new Random((int)seed) : new Random();
        _scheduler = new AutoSpawnScheduler(MaxActiveHealthPacks, SpawnInterval, FirstSpawnDelay);
        GD.Print($"[PickupManager] Ready (max {MaxActiveHealthPacks} packs, every {SpawnInterval}s)");
    }

    public override void _Process(double delta)
    {
        _clock += delta;

        if (!AmbientSpawningEnabled || HealthPackScene == null) return;
        if (!_scheduler.ShouldSpawn(_clock, ActiveCount)) return;

        if (TrySpawnNearPlayer(MinDistanceFromPlayer, MaxDistanceFromPlayer, 0f))
        {
            _scheduler.NotifySpawned(_clock);
        }
    }

    /// <summary>
    /// Spawns a pack on a reachable floor tile in the distance ring around the player, preferring
    /// tiles at least <paramref name="avoidEnemyRadius"/> away from every living enemy. Returns
    /// false and places nothing when the cap is reached or no tile qualifies.
    /// </summary>
    public bool TrySpawnNearPlayer(float minDistance, float maxDistance, float avoidEnemyRadius)
    {
        if (HealthPackScene == null)
        {
            GD.PrintErr("[PickupManager] HealthPackScene is not assigned.");
            return false;
        }

        if (ActiveCount >= MaxActiveHealthPacks)
        {
            GD.Print("[PickupManager] Health pack cap reached, spawn skipped.");
            return false;
        }

        var enemyManager = EnemyManager.Instance;
        var player = GameManager.Instance?.Player;
        if (enemyManager == null || player == null)
        {
            GD.Print("[PickupManager] Missing enemy manager or player, spawn skipped.");
            return false;
        }

        Vector2 playerPosition = player.GlobalPosition;

        // Preferred pass stays out of enemy aggro range; the fallback accepts any reachable floor.
        if (avoidEnemyRadius > 0f && TrySpawnInRing(playerPosition, minDistance, maxDistance, avoidEnemyRadius))
        {
            return true;
        }

        return TrySpawnInRing(playerPosition, minDistance, maxDistance, 0f);
    }

    /// <summary>
    /// Spawns a pack at an exact position (used for reward rooms). Emits HealthPackSpawned.
    /// </summary>
    public HealthPack? SpawnHealthPackAt(Vector2 globalPosition)
    {
        if (HealthPackScene == null)
        {
            GD.PrintErr("[PickupManager] HealthPackScene is not assigned.");
            return null;
        }

        var pack = HealthPackScene.Instantiate<HealthPack>();
        if (pack == null)
        {
            GD.PrintErr("[PickupManager] Could not instantiate the health pack scene.");
            return null;
        }

        pack.GlobalPosition = globalPosition;
        GetTree().Root.AddChild(pack);

        _active.Add(pack);
        pack.TreeExited += () => _active.Remove(pack);

        SignalManager.Instance?.EmitSignal(nameof(SignalManager.HealthPackSpawned), globalPosition);
        return pack;
    }

    private bool TrySpawnInRing(Vector2 playerPosition, float minDistance, float maxDistance, float avoidEnemyRadius)
    {
        var enemyManager = EnemyManager.Instance;
        if (enemyManager == null) return false;

        int tileSize = enemyManager.TileSize;
        if (tileSize <= 0) return false;

        Vector2I playerCell = enemyManager.WorldToCell(playerPosition);
        var candidates = SampleFloorCells(enemyManager, playerPosition, minDistance, maxDistance, tileSize);

        bool accepted = HealthPackPlacement.TrySelectCell(
            candidates,
            playerCell,
            minDistance,
            maxDistance,
            tileSize,
            cell => IsCellUsable(enemyManager, cell, playerPosition, avoidEnemyRadius),
            _random,
            out Vector2I selectedCell);

        if (!accepted)
        {
            GD.Print("[PickupManager] No valid floor tile for a health pack.");
            return false;
        }

        return SpawnHealthPackAt(enemyManager.CellCenterGlobal(selectedCell)) != null;
    }

    private List<Vector2I> SampleFloorCells(
        EnemyManager enemyManager,
        Vector2 playerPosition,
        float minDistance,
        float maxDistance,
        int tileSize)
    {
        var candidates = new List<Vector2I>(PlacementAttempts);
        for (int i = 0; i < PlacementAttempts; i++)
        {
            float angle = (float)(_random.NextDouble() * Mathf.Tau);
            float distance = Mathf.Lerp(minDistance, maxDistance, (float)_random.NextDouble());
            Vector2 sample = playerPosition + (Vector2.FromAngle(angle) * distance);

            if (!enemyManager.TryGetRandomFloorTileNear(sample, tileSize * 2f, out Vector2I cell)) continue;
            if (!candidates.Contains(cell)) candidates.Add(cell);
        }

        return candidates;
    }

    private static bool IsCellUsable(
        EnemyManager enemyManager,
        Vector2I cell,
        Vector2 playerPosition,
        float avoidEnemyRadius)
    {
        if (!enemyManager.IsFloorCell(cell)) return false;
        if (!enemyManager.IsFloorReachable(playerPosition, enemyManager.CellCenterGlobal(cell))) return false;

        if (avoidEnemyRadius <= 0f) return true;

        Vector2 cellCenter = enemyManager.CellCenterGlobal(cell);
        foreach (var enemy in enemyManager.ActiveEnemies)
        {
            if (!GodotObject.IsInstanceValid(enemy)) continue;
            if (enemy.GlobalPosition.DistanceTo(cellCenter) < avoidEnemyRadius) return false;
        }

        return true;
    }

    private void Prune() => _active.RemoveAll(pack => !GodotObject.IsInstanceValid(pack) || pack.IsQueuedForDeletion());
}

using Godot;
using System;

public partial class SignalManager : Node
{
    public static SignalManager Instance => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<SignalManager>("SignalManager");

    // DEBUG UI SIGNALS
    [Signal] public delegate void DifficultyStateChangedEventHandler(string newStateName);
    [Signal] public delegate void PerformanceDataUpdatedEventHandler(float averageAccuracy, float totalDamageTaken, double totalSessionTime, double timeSinceLastHit, int totalKills);
    [Signal] public delegate void DirectorActionExecutedEventHandler(string actionName, float cost);
    [Signal] public delegate void PerformanceMetricsUpdatedEventHandler(float expectedPerformance, float currentPlayerPower, float ratio, float benefitPoints, float negativePoints, float killRate, float damageRate, float isFlailing);
    [Signal] public delegate void DebugUiVisibilityChangedEventHandler(bool visible);

    // GAMEPLAY SIGNALS
    [Signal] public delegate void EnemyKilledEventHandler();
    [Signal] public delegate void PlayerTookDamageEventHandler(float amount);
    [Signal] public delegate void PlayerHealedEventHandler(float amount);
    [Signal] public delegate void WeaponFiredEventHandler();
    [Signal] public delegate void WeaponHitEventHandler();
    [Signal] public delegate void HealthPackSpawnedEventHandler(Vector2 position);
    [Signal] public delegate void HealthPackCollectedEventHandler(float amount);

    // DUNGEON SIGNALS
    [Signal] public delegate void DungeonChangedEventHandler();
    [Signal] public delegate void DungeonRoomCreatedEventHandler(string kind, Vector2I tilePosition, Vector2I tileSize, int enemyCount);
    
}

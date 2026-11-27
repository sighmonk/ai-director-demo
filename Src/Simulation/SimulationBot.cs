using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Deterministic simulated player: picks the nearest enemy in perception range, follows the game's
/// A* navigation, keeps a preferred distance, aims with a profile-dependent error and fires at a
/// profile-dependent cadence. It only reads public gameplay state and never touches the Director.
/// </summary>
public partial class SimulationBot : Node
{
    [Export] public float PerceptionRadius = 700f;
    [Export] public float WaypointTolerance = 16f;

    public Player? Player { get; private set; }

    public BotSkillProfile Profile { get; private set; } = BotSkillProfile.Average;

    public Enemy? Target => _target;

    private readonly ScriptedPlayerInput _input = new();
    private RandomNumberGenerator _rng = new();
    private Enemy? _target;
    private Vector2[] _path = Array.Empty<Vector2>();
    private int _pathIndex;
    private Vector2 _pathTargetPosition;
    private double _reactionTimer;
    private double _fireTimer;
    private double _strafeTimer;
    private float _strafeSign = 1f;
    private float _aimErrorRadians;
    private bool _targetVisible;
    private Vector2 _lastPosition;
    private double _stuckTimer;
    private double _escapeTimer;
    private Vector2 _escapeDirection;
    private double _progressTimer;
    private float _bestProgress = float.MaxValue;
    private Enemy? _progressTarget;
    private PhysicsRayQueryParameters2D? _losQuery;
    private Godot.Collections.Array<Rid>? _losExclude;

    public void Bind(Player player, BotSkillProfile profile, ulong seed)
    {
        Player = player;
        Profile = profile;
        _rng = new RandomNumberGenerator { Seed = seed == 0 ? 1UL : seed };
        player.InputSource = _input;
        _path = Array.Empty<Vector2>();
        _pathIndex = 0;
        _pathTargetPosition = Vector2.Zero;
        _reactionTimer = 0;
        _fireTimer = 0;
        _strafeTimer = 0;
        _target = null;
        _lastPosition = player.GlobalPosition;
        _stuckTimer = 0;
        _escapeTimer = 0;
        _escapeDirection = Vector2.Zero;
        _progressTimer = 0;
        _bestProgress = float.MaxValue;
        _progressTarget = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Player == null || !IsInstanceValid(Player)) return;

        _input.Clear();
        BotSkillProfile profile = EffectiveProfile();

        _reactionTimer -= delta;
        _fireTimer -= delta;
        _strafeTimer -= delta;

        if (_reactionTimer <= 0)
        {
            _target = FindTarget(profile);
            _reactionTimer = profile.ReactionTime;
            _aimErrorRadians = Mathf.DegToRad(_rng.RandfRange(-1f, 1f) * profile.AimErrorDegrees);
            _targetVisible = _target != null && IsInstanceValid(_target) && HasLineOfSight(_target);
        }

        if (_target == null || !IsInstanceValid(_target))
        {
            _target = null;
            _targetVisible = false;
            _path = Array.Empty<Vector2>();
            _pathIndex = 0;
            _input.MoveDirection = Vector2.Zero;
            return;
        }

        // Follow a path snapshot cell by cell; only rebuild it when the target moves.
        UpdatePath();

        if (Player.GlobalPosition.DistanceTo(_lastPosition) < 0.5f) _stuckTimer += delta;
        else _stuckTimer = 0;
        _lastPosition = Player.GlobalPosition;

        float progressDistance = _target.GlobalPosition.DistanceTo(Player.GlobalPosition);
        if (!ReferenceEquals(_progressTarget, _target))
        {
            _progressTarget = _target;
            _bestProgress = progressDistance;
            _progressTimer = 0;
        }
        else if (progressDistance < _bestProgress - 8f)
        {
            _bestProgress = progressDistance;
            _progressTimer = 0;
        }
        else
        {
            _progressTimer += delta;
        }

        if (_stuckTimer > 0.5 || _progressTimer > 1.5)
        {
            _stuckTimer = 0;
            _progressTimer = 0;
            _bestProgress = progressDistance;
            if (!RecoverToFloor()) StartEscape();
        }

        if (_escapeTimer > 0)
        {
            // Wedged in a corner or orbiting a wall: commit to one direction for long enough to
            // actually leave the spot (per-frame A* directions used to cancel each other out).
            _escapeTimer -= delta;
            _input.MoveDirection = _escapeDirection * profile.MoveSpeedFactor;
            _input.ShootRequested = false;
            return;
        }

        Vector2 toTarget = _target.GlobalPosition - Player.GlobalPosition;
        float distance = toTarget.Length();
        Vector2 direction = distance > 0.001f ? toTarget / distance : Vector2.Zero;

        if (_strafeTimer <= 0)
        {
            _strafeSign = _rng.Randf() < 0.5f ? -1f : 1f;
            _strafeTimer = 1.5;
        }

        Vector2 pathDirection = NextWaypointDirection(direction);
        Vector2 threat = ThreatVector(profile);
        Vector2 desire;
        bool inDanger = profile.DangerRadius > 0f && NearestEnemyDistance() < profile.DangerRadius;
        if (inDanger)
        {
            // Immediate retreat overrides target keeping and path following.
            desire = threat.LengthSquared() > 0.01f ? threat : -direction;
        }
        else if (!_targetVisible)
        {
            // While navigating to an enemy the bot follows the path exactly; mixing in crowd
            // avoidance here made it oscillate between two routes at a cell boundary.
            desire = pathDirection;
        }
        else if (distance < profile.PreferredDistanceMin)
        {
            desire = -direction + (threat * 1.5f);
        }
        else if (distance > profile.PreferredDistanceMax)
        {
            desire = pathDirection + threat;
        }
        else if (profile.StrafeWhileInBand)
        {
            desire = (new Vector2(-direction.Y, direction.X) * _strafeSign) + (threat * 1.5f);
        }
        else
        {
            desire = threat;
        }

        // Path waypoints are consecutive A* cells, so MoveAndSlide can follow them without steering;
        // rotating the direction here made the bot circle around corners.
        _input.MoveDirection = ChooseOpenDirection(desire) * profile.MoveSpeedFactor;
        _input.AimPosition = Player.GlobalPosition + direction.Rotated(_aimErrorRadians).Normalized() * 200f;

        if (_fireTimer <= 0 && _targetVisible && distance <= profile.PerceptionRadius)
        {
            _input.ShootRequested = true;
            _fireTimer = profile.FireInterval;
        }
    }

    private void StartEscape()
    {
        _escapeTimer = 0.5;
        float angle = _rng.RandfRange(0f, Mathf.Tau);
        // Prefer a direction with open space; fall back to the seeded random angle.
        Vector2 random = Vector2.FromAngle(angle);
        Vector2 open = ChooseOpenDirection(random);
        _escapeDirection = open.LengthSquared() > 0.0001f ? open : random;
    }

    private bool RecoverToFloor()
    {
        var enemies = GetNodeOrNull<EnemyManager>("/root/EnemyManager");
        if (enemies == null || enemies.TileSize <= 0) return false;
        if (enemies.IsFloorTile(Player!.GlobalPosition)) return false;

        Vector2I cell = enemies.WorldToCell(Player.GlobalPosition);
        for (int radius = 1; radius <= 3; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var candidate = cell + new Vector2I(dx, dy);
                    if (!enemies.IsFloorCell(candidate)) continue;

                    Player.GlobalPosition = enemies.CellCenterGlobal(candidate);
                    _path = Array.Empty<Vector2>();
                    _pathIndex = 0;
                    GD.Print($"[SimulationBot] Recovered from a wall cell to {candidate}.");
                    return true;
                }
            }
        }

        return false;
    }

    private Vector2 NextWaypointDirection(Vector2 fallback)
    {
        while (_pathIndex < _path.Length &&
               Player!.GlobalPosition.DistanceTo(_path[_pathIndex]) < WaypointTolerance)
        {
            _pathIndex++;
        }

        if (_pathIndex >= _path.Length) return fallback;

        Vector2 offset = _path[_pathIndex] - Player!.GlobalPosition;
        return offset.LengthSquared() > 0.0001f ? offset.Normalized() : fallback;
    }

    private void UpdatePath()
    {
        if (_target == null || !IsInstanceValid(_target))
        {
            _path = Array.Empty<Vector2>();
            _pathIndex = 0;
            return;
        }

        bool needsRefresh = _path.Length == 0
            || _pathIndex >= _path.Length
            || _target.GlobalPosition.DistanceTo(_pathTargetPosition) > 24f;
        if (!needsRefresh) return;

        _pathTargetPosition = _target.GlobalPosition;
        var enemies = GetNodeOrNull<EnemyManager>("/root/EnemyManager");
        _path = enemies?.GetPath(Player!.GlobalPosition, _target.GlobalPosition) ?? Array.Empty<Vector2>();

        _pathIndex = 0;
        while (_pathIndex < _path.Length &&
               Player.GlobalPosition.DistanceTo(_path[_pathIndex]) < WaypointTolerance)
        {
            _pathIndex++;
        }
    }

    private BotSkillProfile EffectiveProfile()
    {
        float sessionTime = GetNodeOrNull<GameManager>("/root/GameManager")?.SessionTime ?? 0f;
        if (Profile.SandbagUntilSeconds > 0f && sessionTime < Profile.SandbagUntilSeconds)
        {
            return BotSkillProfile.Degrade(Profile);
        }

        return Profile;
    }

    private float NearestEnemyDistance()
    {
        var enemies = GetNodeOrNull<EnemyManager>("/root/EnemyManager");
        if (enemies == null) return float.MaxValue;

        float nearest = float.MaxValue;
        foreach (Enemy enemy in enemies.ActiveEnemies)
        {
            if (!IsInstanceValid(enemy)) continue;
            nearest = Math.Min(nearest, Player!.GlobalPosition.DistanceTo(enemy.GlobalPosition));
        }

        return nearest;
    }

    private Vector2 ChooseOpenDirection(Vector2 desire)
    {
        Vector2 direction = desire.Normalized();
        if (direction.LengthSquared() < 0.0001f) return Vector2.Zero;

        float[] offsets = { 0f, 45f, -45f, 90f, -90f, 135f, -135f, 180f };
        Vector2 best = direction;
        float bestClearance = -1f;

        foreach (float degrees in offsets)
        {
            Vector2 candidate = direction.Rotated(Mathf.DegToRad(degrees));
            float clearance = 0f;
            foreach (float probe in new[] { 20f, 50f, 90f })
            {
                if (Player!.TestMove(Player.GlobalTransform, candidate * probe)) break;
                clearance = probe;
            }

            if (clearance <= bestClearance) continue;
            bestClearance = clearance;
            best = candidate;
        }

        // No clear direction: let MoveAndSlide slide along the wall instead of stopping.
        return bestClearance >= 20f ? best : direction;
    }

    private Enemy? FindTarget(BotSkillProfile profile)
    {
        var enemies = GetNodeOrNull<EnemyManager>("/root/EnemyManager");
        if (enemies == null) return null;

        Enemy? nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Enemy enemy in enemies.ActiveEnemies)
        {
            if (!IsInstanceValid(enemy)) continue;

            float distance = Player!.GlobalPosition.DistanceTo(enemy.GlobalPosition);
            if (distance > profile.PerceptionRadius) continue;
            if (distance >= nearestDistance) continue;

            nearest = enemy;
            nearestDistance = distance;
        }

        return nearest;
    }

    private Vector2 ThreatVector(BotSkillProfile profile)
    {
        if (profile.ThreatRadius <= 0f || profile.ThreatAvoidance <= 0f) return Vector2.Zero;

        var enemies = GetNodeOrNull<EnemyManager>("/root/EnemyManager");
        if (enemies == null) return Vector2.Zero;

        Vector2 push = Vector2.Zero;
        foreach (Enemy enemy in enemies.ActiveEnemies)
        {
            if (!IsInstanceValid(enemy)) continue;

            Vector2 offset = Player!.GlobalPosition - enemy.GlobalPosition;
            float distance = offset.Length();
            if (distance < 0.001f || distance > profile.ThreatRadius) continue;

            push += offset / distance * (1f - (distance / profile.ThreatRadius));
        }

        return push * profile.ThreatAvoidance;
    }

    private bool HasLineOfSight(Enemy enemy)
    {
        return HasClearLine(enemy.GlobalPosition);
    }

    private bool HasClearLine(Vector2 point)
    {
        Player player = Player!;
        _losQuery ??= PhysicsRayQueryParameters2D.Create(player.GlobalPosition, point);
        _losExclude ??= new Godot.Collections.Array<Rid> { player.GetRid() };

        // Reuse the query object: allocating one per reaction tick produced thousands of native
        // arrays whose finalizers could crash the process during shutdown.
        _losQuery.From = player.GlobalPosition;
        _losQuery.To = point;
        _losQuery.CollideWithAreas = false;
        _losQuery.CollideWithBodies = true;
        _losQuery.Exclude = _losExclude;

        // Enemies are areas, so only a wall (a body) can block the shot.
        return player.GetWorld2D().DirectSpaceState.IntersectRay(_losQuery).Count == 0;
    }
}

using Godot;
using System;

/// <summary>
/// Source of movement, aim and shoot input for the player. Swapped by the simulation bot so the
/// same controller can be driven by a device or by a seeded policy.
/// </summary>
public interface IPlayerInput
{
    Vector2 MoveDirection { get; }

    /// <summary>Aim point in global coordinates.</summary>
    Vector2 AimPosition { get; }

    /// <summary>True for the single physics frame in which the player should fire.</summary>
    bool ShootRequested { get; }
}

/// <summary>Keyboard and mouse input, reproducing the original player behaviour.</summary>
public sealed class DevicePlayerInput : IPlayerInput
{
    private readonly Func<Vector2> _aimPosition;

    public DevicePlayerInput(Func<Vector2> aimPosition)
    {
        _aimPosition = aimPosition;
    }

    public Vector2 MoveDirection => Input.GetVector("MoveLeft", "MoveRight", "MoveUp", "MoveDown");

    public Vector2 AimPosition => _aimPosition();

    public bool ShootRequested => Input.IsActionJustPressed("PlayerShoot");
}

/// <summary>Input written frame by frame by the simulation bot.</summary>
public sealed class ScriptedPlayerInput : IPlayerInput
{
    public Vector2 MoveDirection { get; set; }

    public Vector2 AimPosition { get; set; }

    public bool ShootRequested { get; set; }

    public void Clear()
    {
        MoveDirection = Vector2.Zero;
        ShootRequested = false;
    }
}

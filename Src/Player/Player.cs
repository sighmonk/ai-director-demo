using Godot;
using System;

public partial class Player : CharacterBody2D
{
	private PackedScene playerBulletScene;
	[Export] private float speed = 200;
    private HealthComponent _healthComponent;
    private IPlayerInput? _inputSource;

    /// <summary>
    /// Movement, aim and shoot input. Defaults to the device; the simulation bot replaces it with a
    /// scripted source.
    /// </summary>
    public IPlayerInput InputSource
    {
        get => _inputSource ??= new DevicePlayerInput(() => GetGlobalMousePosition());
        set => _inputSource = value;
    }
	
	public override void _Ready()
	{
        GameManager.Instance.RegisterPlayer(this);

		playerBulletScene = GD.Load<PackedScene>("res://Src/Projectiles/PlayerBullet/PlayerBullet.tscn");
        _healthComponent = GetNodeOrNull<HealthComponent>("HealthComponent");
        
        if (_healthComponent != null)
        {
            _healthComponent.HasInvincibilityFrames = true;
            _healthComponent.HealthChanged += OnHealthChanged;
        }
	}

    private void OnHealthChanged(float newHealth, float changeAmount)
    {
        if (changeAmount < 0)
        {
            SignalManager.Instance.EmitSignal(nameof(SignalManager.PlayerTookDamage), Mathf.Abs(changeAmount));
        }
        else if (changeAmount > 0)
        {
            SignalManager.Instance.EmitSignal(nameof(SignalManager.PlayerHealed), changeAmount);
        }
    }
	
	public override void _PhysicsProcess(double delta)
	{
		Vector2 inputDirection = InputSource.MoveDirection;
		
		Velocity = inputDirection * speed;
		
		if (InputSource.ShootRequested)
		{
			Shoot(InputSource.AimPosition);
		}

		MoveAndSlide();
	}

	private void Shoot(Vector2 aimPosition)
	{
		Vector2 offset = aimPosition - GlobalPosition;
		if (playerBulletScene == null || offset.LengthSquared() < 0.0001f) return;

		PlayerBullet bullet = playerBulletScene.Instantiate<PlayerBullet>();
		bullet.GlobalPosition = GlobalPosition;
		bullet.Direction = offset.Normalized();

		GetParent().AddChild(bullet);
		SignalManager.Instance.EmitSignal(nameof(SignalManager.WeaponFired));
	}
}

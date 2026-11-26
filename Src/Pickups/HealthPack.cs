using Godot;

/// <summary>
/// Collectable that heals the player exactly once. Enemies are <see cref="Area2D"/> nodes and are
/// ignored: only a <see cref="Player"/> body can pick the pack up.
/// </summary>
public partial class HealthPack : Area2D
{
    public const string GroupName = "HealthPickups";

    [Export] public float HealAmount = 25f;
    [Export] public float CollectEffectDuration = 0.2f;

    private bool _collected;

    public bool IsCollected => _collected;

    public override void _Ready()
    {
        AddToGroup(GroupName);
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_collected || body is not Player player) return;

        var health = player.GetNodeOrNull<HealthComponent>("HealthComponent");
        if (health == null)
        {
            GD.PrintErr("[HealthPack] Player has no HealthComponent, pickup ignored.");
            return;
        }

        _collected = true;
        health.Heal(HealAmount);
        SignalManager.Instance?.EmitSignal(nameof(SignalManager.HealthPackCollected), HealAmount);
        PlayCollectEffect();
    }

    private void PlayCollectEffect()
    {
        // Stop detecting bodies first; the tween only removes the visual.
        SetDeferred(Area2D.PropertyName.Monitoring, false);
        SetDeferred(Area2D.PropertyName.Monitorable, false);

        var tween = CreateTween();
        tween.TweenProperty(this, "scale", Scale * 1.6f, CollectEffectDuration);
        tween.Parallel().TweenProperty(this, "modulate:a", 0f, CollectEffectDuration);
        tween.TweenCallback(Callable.From(QueueFree));
    }
}

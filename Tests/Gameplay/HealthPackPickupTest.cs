using GdUnit4;
using static GdUnit4.Assertions;
using Godot;
using System.Threading.Tasks;

namespace ProjectKitchen.Tests.Gameplay;

/// <summary>
/// Runtime behaviour of the health pack: one heal per pickup and never above max HP.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class HealthPackPickupTest
{
    private HealthPack? _pack;
    private Player? _player;

    [AfterTest]
    public void Teardown()
    {
        if (GodotObject.IsInstanceValid(_pack)) _pack!.Free();
        if (GodotObject.IsInstanceValid(_player)) _player!.Free();
        _pack = null;
        _player = null;
    }

    private async Task<(HealthPack pack, Player player, HealthComponent health)> SpawnOverlapping(float healAmount, float currentHealth)
    {
        var tree = (SceneTree)Engine.GetMainLoop();

        _pack = GD.Load<PackedScene>("res://Src/Pickups/HealthPack.tscn").Instantiate<HealthPack>();
        _pack.HealAmount = healAmount;
        tree.Root.AddChild(_pack);

        _player = GD.Load<PackedScene>("res://Src/Player/Player.tscn").Instantiate<Player>();
        tree.Root.AddChild(_player);

        var health = _player.GetNode<HealthComponent>("HealthComponent");
        health.CurrentHealth = currentHealth;

        _pack.GlobalPosition = new Vector2(200, 200);
        _player.GlobalPosition = new Vector2(200, 200);

        // The physics server needs a couple of ticks to report the overlap for newly added nodes.
        for (int i = 0; i < 10 && !_pack.IsCollected; i++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        }

        return (_pack, _player, health);
    }

    [TestCase]
    public async Task Pickup_HealsExactlyOnce()
    {
        var (pack, _, health) = await SpawnOverlapping(healAmount: 40f, currentHealth: 50f);

        AssertBool(pack.IsCollected).IsTrue();
        AssertFloat(health.CurrentHealth).IsEqual(90f);

        // The pack is already consumed; further physics frames must not heal again.
        var tree = (SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
        await tree.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);

        AssertFloat(health.CurrentHealth).IsEqual(90f);
    }

    [TestCase]
    public async Task Pickup_NeverExceedsMaxHealth()
    {
        var (_, _, health) = await SpawnOverlapping(healAmount: 500f, currentHealth: 50f);

        AssertFloat(health.CurrentHealth).IsEqual(health.MaxHealth);
        AssertFloat(health.CurrentHealth).IsEqual(100f);
    }

    [TestCase]
    public async Task Pickup_DoesNotDamageThePlayer()
    {
        var (_, _, health) = await SpawnOverlapping(healAmount: 10f, currentHealth: 60f);

        AssertFloat(health.CurrentHealth).IsEqual(70f);
    }
}

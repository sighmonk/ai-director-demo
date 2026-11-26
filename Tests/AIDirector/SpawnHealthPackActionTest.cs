using GdUnit4;
using static GdUnit4.Assertions;
using Godot;

namespace ProjectKitchen.Tests.AIDirector;

/// <summary>
/// Wiring and failure behaviour of the health-pack Director action.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class SpawnHealthPackActionTest
{
    [TestCase]
    public void Action_IsRegisteredUnderDirectorActionsAsBenefit()
    {
        var scene = GD.Load<PackedScene>("res://Src/AI Director/Director.tscn");
        var director = scene.Instantiate();

        var action = director.GetNodeOrNull<SpawnHealthPack>("Actions/SpawnHealthPack");

        AssertObject(action).IsNotNull();
        AssertObject(action!.ActionType).IsEqual(ActionType.Benefit);
        AssertFloat(action.Cost).IsEqual(10f);
        AssertFloat(action.Cooldown).IsEqual(5f);
        AssertFloat(action.Weight).IsEqual(1f);

        director.Free();
    }

    [TestCase]
    public void TryExecute_WithoutAPlaceableTile_ReturnsFalse()
    {
        var action = new SpawnHealthPack();
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(action);

        bool executed = action.TryExecute(0.0);

        AssertBool(executed).IsFalse();
        // The failure must leave the action ready.
        AssertBool(action.CanExecute(action.Cost, 0.0)).IsTrue();

        action.Free();
    }
}

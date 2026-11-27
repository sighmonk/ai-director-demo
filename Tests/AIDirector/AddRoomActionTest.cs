using GdUnit4;
using static GdUnit4.Assertions;
using Godot;

namespace ProjectKitchen.Tests.AIDirector;

/// <summary>
/// Wiring, defaults and session limit of the runtime room Director actions.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class AddRoomActionTest
{
    [TestCase]
    public void DirectorScene_HasRewardAndThreatRoomActions()
    {
        var scene = GD.Load<PackedScene>("res://Src/AI Director/Director.tscn");
        var director = scene.Instantiate();

        var reward = director.GetNodeOrNull<AddRewardRoom>("Actions/AddRewardRoom");
        var threat = director.GetNodeOrNull<AddThreatRoom>("Actions/AddThreatRoom");

        AssertObject(reward).IsNotNull();
        AssertObject(threat).IsNotNull();
        AssertObject(reward!.ActionType).IsEqual(ActionType.Benefit);
        AssertObject(threat!.ActionType).IsEqual(ActionType.Negative);
        AssertInt(reward.RoomSideMin).IsEqual(5);
        AssertInt(reward.RoomSideMax).IsEqual(10);
        AssertInt(threat.EnemiesPerRoom).IsGreater(0);

        director.Free();
    }

    [TestCase]
    public void CanExecute_RejectsWhenTheSessionLimitIsReached()
    {
        var reward = new AddRewardRoom
        {
            Cost = 1f,
            Cooldown = 0f,
            MaxRoomsAddedPerSession = 0
        };

        AssertBool(reward.CanExecute(100f, 10.0)).IsFalse();

        reward.Free();
    }

    [TestCase]
    public void CanExecute_AllowsTheFirstRoomWithinTheLimit()
    {
        var threat = new AddThreatRoom
        {
            Cost = 1f,
            Cooldown = 0f,
            MaxRoomsAddedPerSession = 1
        };

        AssertBool(threat.CanExecute(100f, 10.0)).IsTrue();

        threat.Free();
    }

    [TestCase]
    public void TryExecute_WithoutADungeon_ReturnsFalseAndStaysReady()
    {
        var reward = new AddRewardRoom
        {
            Cost = 1f,
            Cooldown = 5f,
            MaxRoomsAddedPerSession = 2
        };
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(reward);

        bool executed = reward.TryExecute(0.0);

        AssertBool(executed).IsFalse();
        AssertBool(reward.CanExecute(100f, 0.0)).IsTrue();
        AssertInt(reward.RoomsAdded).IsEqual(0);

        reward.Free();
    }
}

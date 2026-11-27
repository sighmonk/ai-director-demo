using GdUnit4;
using static GdUnit4.Assertions;
using Godot;

namespace ProjectKitchen.Tests.AIDirector;

/// <summary>
/// Wiring, defaults and session limit of the close-passage Director action.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class ClosePassageActionTest
{
    [TestCase]
    public void DirectorScene_HasAClosePassageAction()
    {
        var scene = GD.Load<PackedScene>("res://Src/AI Director/Director.tscn");
        var director = scene.Instantiate();

        var action = director.GetNodeOrNull<ClosePassage>("Actions/ClosePassage");

        AssertObject(action).IsNotNull();
        AssertObject(action!.ActionType).IsEqual(ActionType.Negative);
        AssertInt(action.MaxClosuresPerSession).IsEqual(1);
        AssertFloat(action.ReopenAfterSeconds).IsEqual(30f);

        director.Free();
    }

    [TestCase]
    public void CanExecute_RejectsWhenTheSessionLimitIsReached()
    {
        var action = new ClosePassage
        {
            Cost = 1f,
            Cooldown = 0f,
            MaxClosuresPerSession = 0
        };

        AssertBool(action.CanExecute(100f, 10.0)).IsFalse();

        action.Free();
    }

    [TestCase]
    public void TryExecute_WithoutADungeon_ReturnsFalseAndStaysReady()
    {
        var action = new ClosePassage
        {
            Cost = 1f,
            Cooldown = 5f,
            MaxClosuresPerSession = 1
        };
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(action);

        bool executed = action.TryExecute(0.0);

        AssertBool(executed).IsFalse();
        AssertBool(action.CanExecute(100f, 0.0)).IsTrue();
        AssertInt(action.Closures).IsEqual(0);

        action.Free();
    }
}

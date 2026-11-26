using GdUnit4;
using static GdUnit4.Assertions;
using Godot;

namespace ProjectKitchen.Tests.AIDirector;

/// <summary>
/// Action whose success can be switched per test, mirroring actions that can reject execution
/// (for example when no free floor tile exists).
/// </summary>
public partial class OutcomeTestAction : DirectorAction
{
    public bool Succeeds { get; set; } = true;
    public int Attempts { get; private set; }
    public int Successes { get; private set; }

    protected override bool OnExecute()
    {
        Attempts++;
        if (!Succeeds) return false;

        Successes++;
        return true;
    }
}

/// <summary>
/// Success/failure cost accounting: a Director action must only spend points and start its cooldown
/// when it actually had an effect.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class DirectorActionExecutionTest
{
    private OutcomeTestAction _action = null!;

    [BeforeTest]
    public void Setup()
    {
        _action = new OutcomeTestAction { Cost = 10f, Cooldown = 5f, Weight = 1f };
    }

    [AfterTest]
    public void Teardown()
    {
        _action?.Free();
        _action = null!;
    }

    [TestCase]
    public void TryExecute_Success_ReturnsTrue()
    {
        AssertBool(_action.TryExecute(3.0)).IsTrue();
        AssertInt(_action.Successes).IsEqual(1);
    }

    [TestCase]
    public void TryExecute_Failure_ReturnsFalseAndStaysAvailable()
    {
        _action.Succeeds = false;

        AssertBool(_action.TryExecute(3.0)).IsFalse();
        AssertInt(_action.Attempts).IsEqual(1);
        AssertInt(_action.Successes).IsEqual(0);

        // A failed attempt must not consume the cooldown.
        AssertBool(_action.CanExecute(20f, 3.0)).IsTrue();
    }

    [TestCase]
    public void TryExecute_Success_StartsCooldown()
    {
        AssertBool(_action.TryExecute(3.0)).IsTrue();

        AssertBool(_action.CanExecute(20f, 4.0)).IsFalse();
        AssertBool(_action.CanExecute(20f, 8.0)).IsTrue();
    }

    [TestCase]
    public void TryExecuteAction_Success_SpendsCost()
    {
        var director = new Director();
        float spent = 0f;

        bool executed = director.TryExecuteAction(_action, 3.0, cost => spent = cost);

        AssertBool(executed).IsTrue();
        AssertFloat(spent).IsEqual(10f);
        director.Free();
    }

    [TestCase]
    public void TryExecuteAction_Failure_DoesNotSpendCost()
    {
        var director = new Director();
        _action.Succeeds = false;
        float spent = 0f;

        bool executed = director.TryExecuteAction(_action, 3.0, cost => spent = cost);

        AssertBool(executed).IsFalse();
        AssertFloat(spent).IsEqual(0f);
        director.Free();
    }

    [TestCase]
    public void TryExecuteAction_NullAction_ReturnsFalse()
    {
        var director = new Director();

        bool executed = director.TryExecuteAction(null, 3.0, _ => { });

        AssertBool(executed).IsFalse();
        director.Free();
    }

    [TestCase]
    public void TryExecuteAction_WithoutSpendCallback_DoesNotThrow()
    {
        var director = new Director();

        AssertBool(director.TryExecuteAction(_action, 3.0)).IsTrue();
        director.Free();
    }
}

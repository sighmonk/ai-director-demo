using GdUnit4;
using static GdUnit4.Assertions;
using Godot;
using System.Collections.Generic;

namespace ProjectKitchen.Tests.AIDirector;

/// <summary>
/// Tests for the action policy abstraction. The Director filters candidates; the policy only
/// picks one of them, so these tests exercise <see cref="RulePolicy"/> directly with a scripted
/// random source.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class ActionPolicyTest
{
    private List<WeightedTestAction> _actions = null!;

    [BeforeTest]
    public void Setup()
    {
        _actions = new List<WeightedTestAction>();
    }

    [AfterTest]
    public void Teardown()
    {
        foreach (var action in _actions)
        {
            action?.Free();
        }
        _actions.Clear();
        _actions = null!;
    }

    private WeightedTestAction CreateAction(float weight, ActionType type = ActionType.Negative)
    {
        var action = new WeightedTestAction
        {
            ActionType = type,
            Cost = 10f,
            Weight = weight,
            Cooldown = 5f
        };
        _actions.Add(action);
        return action;
    }

    private List<DirectorAction> CandidatesOf(List<WeightedTestAction> actions)
    {
        var candidates = new List<DirectorAction>(actions.Count);
        foreach (var action in actions) candidates.Add(action);
        return candidates;
    }

    private static ActionSelectionContext Context(List<DirectorAction> candidates, string state = "Reduce")
    {
        return new ActionSelectionContext(candidates, ActionType.Benefit, 50f, 12.5, state);
    }

    #region Selection

    [TestCase]
    public void SelectAction_EmptyCandidates_ReturnsNull()
    {
        var policy = new RulePolicy(() => 0.5f);

        var selected = policy.SelectAction(Context(new List<DirectorAction>()));

        AssertObject(selected).IsNull();
    }

    [TestCase]
    public void SelectAction_AllZeroWeights_ReturnsNull()
    {
        var zeroA = CreateAction(0f);
        var zeroB = CreateAction(0f);
        var policy = new RulePolicy(() => 0.5f);

        var selected = policy.SelectAction(Context(CandidatesOf(new List<WeightedTestAction> { zeroA, zeroB })));

        AssertObject(selected).IsNull();
    }

    [TestCase]
    public void SelectAction_ZeroWeightAction_IsNeverSelected()
    {
        var zero = CreateAction(0f);
        var normal = CreateAction(1f);
        var candidates = CandidatesOf(new List<WeightedTestAction> { zero, normal });

        // The lowest possible sample would still land on a zero-weight action if it were kept.
        var policy = new RulePolicy(() => 0f);

        var selected = policy.SelectAction(Context(candidates));

        AssertObject(selected).IsSame(normal);
    }

    [TestCase]
    public void SelectAction_NegativeWeightAction_IsIgnored()
    {
        var negative = CreateAction(-5f);
        var normal = CreateAction(1f);
        var candidates = CandidatesOf(new List<WeightedTestAction> { negative, normal });
        var policy = new RulePolicy(() => 0.99f);

        var selected = policy.SelectAction(Context(candidates));

        AssertObject(selected).IsSame(normal);
    }

    [TestCase]
    public void SelectAction_SampleAtLowerBound_SelectsFirstWeightedAction()
    {
        var first = CreateAction(1f);
        var second = CreateAction(1f);
        var policy = new RulePolicy(() => 0f);

        var selected = policy.SelectAction(Context(CandidatesOf(new List<WeightedTestAction> { first, second })));

        AssertObject(selected).IsSame(first);
    }

    [TestCase]
    public void SelectAction_SampleAtUpperBound_SelectsLastWeightedAction()
    {
        var first = CreateAction(1f);
        var second = CreateAction(1f);
        var policy = new RulePolicy(() => 1f);

        var selected = policy.SelectAction(Context(CandidatesOf(new List<WeightedTestAction> { first, second })));

        AssertObject(selected).IsSame(second);
    }

    [TestCase]
    public void SelectAction_SampleMapsToWeightBucket()
    {
        // Total weight 4: first action covers [0, 1], second covers (1, 4].
        var light = CreateAction(1f);
        var heavy = CreateAction(3f);
        var candidates = CandidatesOf(new List<WeightedTestAction> { light, heavy });

        AssertObject(new RulePolicy(() => 0.2f).SelectAction(Context(candidates))).IsSame(light);
        AssertObject(new RulePolicy(() => 0.3f).SelectAction(Context(candidates))).IsSame(heavy);
    }

    [TestCase]
    public void SelectAction_SameSeed_ProducesSameSequence()
    {
        var low = CreateAction(1f);
        var high = CreateAction(3f);
        var candidates = CandidatesOf(new List<WeightedTestAction> { low, high });

        var randomA = new System.Random(1234);
        var randomB = new System.Random(1234);
        var policyA = new RulePolicy(() => (float)randomA.NextDouble());
        var policyB = new RulePolicy(() => (float)randomB.NextDouble());

        for (int i = 0; i < 25; i++)
        {
            var pickA = policyA.SelectAction(Context(candidates));
            var pickB = policyB.SelectAction(Context(candidates));
            AssertObject(pickA).IsNotNull();
            AssertObject(pickA).IsSame(pickB!);
        }
    }

    [TestCase]
    public void SelectAction_PrefersHeavierWeight()
    {
        // Deterministic sweep of the unit interval: 3:1 weights must split 25/75.
        var low = CreateAction(1f);
        var high = CreateAction(3f);
        var candidates = CandidatesOf(new List<WeightedTestAction> { low, high });
        int lowCount = 0;
        int highCount = 0;

        for (int i = 0; i < 100; i++)
        {
            float sample = (i + 0.5f) / 100f;
            var pick = new RulePolicy(() => sample).SelectAction(Context(candidates));
            if (pick == low) lowCount++;
            else if (pick == high) highCount++;
        }

        AssertInt(lowCount).IsEqual(25);
        AssertInt(highCount).IsEqual(75);
    }

    #endregion

    #region Context

    [TestCase]
    public void Context_ExposesSelectionInputs()
    {
        var action = CreateAction(1f, ActionType.Benefit);

        var context = new ActionSelectionContext(
            new List<DirectorAction> { action },
            ActionType.Benefit,
            42f,
            7.5,
            "Intensify");

        AssertInt(context.Candidates.Count).IsEqual(1);
        AssertObject(context.ActionType).IsEqual(ActionType.Benefit);
        AssertFloat(context.AvailablePoints).IsEqual(42f);
        AssertFloat((float)context.CurrentTime).IsEqual(7.5f);
        AssertString(context.StateName).IsEqual("Intensify");
    }

    [TestCase]
    public void RulePolicy_Name_IsRule()
    {
        AssertString(new RulePolicy().Name).IsEqual("Rule");
    }

    #endregion
}

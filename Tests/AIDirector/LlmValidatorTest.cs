using GdUnit4;
using static GdUnit4.Assertions;
using Godot;
using System.Collections.Generic;

namespace ProjectKitchen.Tests.AIDirector;

/// <summary>
/// Schema, catalog and eligibility validation shared by both LLM arms.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class LlmValidatorTest
{
    private LlmActionCatalog _catalog = null!;
    private LlmActionValidator _validator = null!;
    private LlmTestAction _healthPack = null!;
    private LlmTestAction _buff = null!;

    [BeforeTest]
    public void Setup()
    {
        _healthPack = new LlmTestAction { Name = "SpawnHealthPack", ActionType = ActionType.Benefit, Cost = 10f, Cooldown = 5f };
        _buff = new LlmTestAction { Name = "BuffEnemies", ActionType = ActionType.Negative, Cost = 10f, Cooldown = 5f };

        _catalog = new LlmActionCatalog(new DirectorAction[] { _healthPack, _buff });
        _validator = new LlmActionValidator(_catalog);
    }

    [AfterTest]
    public void Teardown()
    {
        _healthPack?.Free();
        _buff?.Free();
    }

    private ActionSelectionContext Context(
        IReadOnlyList<DirectorAction>? candidates = null,
        float points = 50f,
        double time = 10.0,
        string state = "Reduce")
    {
        return new ActionSelectionContext(
            candidates ?? new List<DirectorAction> { _healthPack, _buff },
            ActionType.Benefit,
            points,
            time,
            state);
    }

    [TestCase]
    public void Validate_AcceptsACatalogActionThatIsEligible()
    {
        LlmValidationResult result = _validator.Validate("{\"action\":\"SpawnHealthPack\"}", Context());

        AssertBool(result.IsValid).IsTrue();
        AssertString(result.ActionId).IsEqual("SpawnHealthPack");
    }

    [TestCase]
    public void Validate_RejectsMalformedResponses()
    {
        foreach (string response in new[]
                 {
                     "",
                     "not json",
                     "[]",
                     "{\"action\":1}",
                     "{\"action\":\"SpawnHealthPack\",\"extra\":true}",
                     "{\"other\":\"SpawnHealthPack\"}"
                 })
        {
            LlmValidationResult result = _validator.Validate(response, Context());
            AssertBool(result.IsValid).IsFalse();
            AssertObject(result.Reason).IsEqual(LlmRejectionReason.Malformed);
        }
    }

    [TestCase]
    public void Validate_RejectsAnUnknownId()
    {
        LlmValidationResult result = _validator.Validate("{\"action\":\"TeleportPlayer\"}", Context());

        AssertBool(result.IsValid).IsFalse();
        AssertObject(result.Reason).IsEqual(LlmRejectionReason.UnknownAction);
        AssertString(result.ActionId).IsEqual("TeleportPlayer");
    }

    [TestCase]
    public void Validate_RejectsACatalogActionThatIsNotACandidate()
    {
        var candidates = new List<DirectorAction> { _healthPack };

        LlmValidationResult result = _validator.Validate("{\"action\":\"BuffEnemies\"}", Context(candidates));

        AssertBool(result.IsValid).IsFalse();
        AssertObject(result.Reason).IsEqual(LlmRejectionReason.NotEligible);
    }

    [TestCase]
    public void Validate_ReportsCostWhenTheBudgetIsTooLow()
    {
        LlmValidationResult result = _validator.Validate("{\"action\":\"SpawnHealthPack\"}", Context(points: 5f));

        AssertBool(result.IsValid).IsFalse();
        AssertObject(result.Reason).IsEqual(LlmRejectionReason.Cost);
    }

    [TestCase]
    public void Validate_ReportsCooldown()
    {
        _healthPack.Execute(10.0);

        LlmValidationResult result = _validator.Validate("{\"action\":\"SpawnHealthPack\"}", Context(time: 12.0));

        AssertBool(result.IsValid).IsFalse();
        AssertObject(result.Reason).IsEqual(LlmRejectionReason.Cooldown);
    }

    [TestCase]
    public void Catalog_PrefixListsEveryActionAndStaysDeterministic()
    {
        string first = _catalog.BuildStaticPrefix();
        string second = _catalog.BuildStaticPrefix();

        AssertString(first).IsEqual(second);
        AssertBool(first.Contains("SpawnHealthPack")).IsTrue();
        AssertBool(first.Contains("BuffEnemies")).IsTrue();
        AssertBool(first.Contains("{\"action\":\"<id>\"}")).IsTrue();
    }
}

using GdUnit4;
using static GdUnit4.Assertions;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ProjectKitchen.Tests.AIDirector;

/// <summary>
/// Asynchronous policy behaviour with a fake model: cadence, deadline metric, retries, failure
/// accounting and the difference between the free (B) and bounded (C) arms.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class LlmPolicyTest
{
    private LlmTestAction _healthPack = null!;
    private LlmTestAction _buff = null!;
    private LlmActionCatalog _catalog = null!;
    private FakeClock _clock = null!;
    private string _telemetryPath = "";
    private LlmPolicyOptions _options = null!;

    [BeforeTest]
    public void Setup()
    {
        _healthPack = new LlmTestAction { Name = "SpawnHealthPack", ActionType = ActionType.Benefit, Cost = 10f, Cooldown = 5f, Weight = 1f };
        _buff = new LlmTestAction { Name = "BuffEnemies", ActionType = ActionType.Negative, Cost = 10f, Cooldown = 5f, Weight = 1f };
        _catalog = new LlmActionCatalog(new DirectorAction[] { _healthPack, _buff });
        _clock = new FakeClock();
        _telemetryPath = Path.Combine(Path.GetTempPath(), $"llm-policy-{Guid.NewGuid():N}.jsonl");
        _options = new LlmPolicyOptions { TelemetryPath = _telemetryPath };
    }

    [AfterTest]
    public void Teardown()
    {
        _healthPack?.Free();
        _buff?.Free();
        if (File.Exists(_telemetryPath)) File.Delete(_telemetryPath);
    }

    private ActionSelectionContext Context(double time = 1.0, string state = "Reduce")
    {
        return new ActionSelectionContext(
            new List<DirectorAction> { _healthPack, _buff },
            ActionType.Benefit,
            50f,
            time,
            state,
            new DirectorGameState(0.7, 0.5f, 4f, 12f, 7, time));
    }

    private static Func<LlmResponse> Respond(string content)
    {
        return () => new LlmResponse(content, "deepseek-chat", 1000, 500);
    }

    private static Func<LlmResponse> Fail()
    {
        return () => throw new LlmClientException("provider unavailable");
    }

    private LlmTelemetry NewTelemetry() => new(_options, "test-session");

    [TestCase]
    public void FreePolicy_ExecutesAValidProposalOnTheNextSelection()
    {
        var client = new FakeLlmClient(Respond("{\"action\":\"SpawnHealthPack\"}"));
        var policy = new LlmFreePolicy(client, _options, _catalog, NewTelemetry(), _clock.Read);

        AssertObject(policy.SelectAction(Context())).IsNull();
        DirectorAction? action = policy.SelectAction(Context(time: 1.1));

        AssertObject(action).IsSame(_healthPack);
        AssertInt(client.Calls).IsEqual(1);
    }

    [TestCase]
    public void Cadence_IssuesAtMostOneCallPerFourSeconds()
    {
        var client = new FakeLlmClient(
            Respond("{\"action\":\"SpawnHealthPack\"}"),
            Respond("{\"action\":\"SpawnHealthPack\"}"));
        var policy = new LlmFreePolicy(client, _options, _catalog, NewTelemetry(), _clock.Read);

        policy.SelectAction(Context(time: 1.0));
        policy.SelectAction(Context(time: 1.1)); // consumes
        policy.SelectAction(Context(time: 2.0)); // too early
        AssertInt(client.Calls).IsEqual(1);

        policy.SelectAction(Context(time: 5.0)); // 1.0 + 4.0
        AssertInt(client.Calls).IsEqual(2);
    }

    [TestCase]
    public void Maintain_DoesNotCallTheModel()
    {
        var client = new FakeLlmClient(Respond("{\"action\":\"SpawnHealthPack\"}"));
        var policy = new LlmFreePolicy(client, _options, _catalog, NewTelemetry(), _clock.Read);

        AssertObject(policy.SelectAction(Context(state: "Maintain"))).IsNull();
        AssertInt(client.Calls).IsEqual(0);
    }

    [TestCase]
    public void DeadlineMiss_IsRecordedButTheActionStillExecutes()
    {
        var clock = _clock;
        var client = new FakeLlmClient(() =>
        {
            clock.Advance(800);
            return new LlmResponse("{\"action\":\"SpawnHealthPack\"}", "deepseek-chat", 1000, 500);
        });
        var policy = new LlmFreePolicy(client, _options, _catalog, NewTelemetry(), clock.Read);

        policy.SelectAction(Context(time: 1.0));
        DirectorAction? action = policy.SelectAction(Context(time: 1.1));

        AssertObject(action).IsSame(_healthPack);
        string log = File.ReadAllText(_telemetryPath);
        AssertBool(log.Contains("\"deadline_miss\":true")).IsTrue();
        AssertBool(log.Contains("\"legal\":true")).IsTrue();
    }

    [TestCase]
    public void Retries_TwoFailuresThenSuccess()
    {
        var client = new FakeLlmClient(Fail(), Fail(), Respond("{\"action\":\"SpawnHealthPack\"}"));
        var policy = new LlmFreePolicy(client, _options, _catalog, NewTelemetry(), _clock.Read);

        policy.SelectAction(Context());
        DirectorAction? action = policy.SelectAction(Context(time: 1.1));

        AssertObject(action).IsSame(_healthPack);
        AssertInt(client.Calls).IsEqual(3);
        AssertBool(File.ReadAllText(_telemetryPath).Contains("\"retries\":2")).IsTrue();
    }

    [TestCase]
    public void FailedCalls_AreCountedAndFlagTheSession()
    {
        var client = new FakeLlmClient(Fail(), Fail(), Fail());
        var telemetry = NewTelemetry();
        var policy = new LlmFreePolicy(client, _options, _catalog, telemetry, _clock.Read);

        policy.SelectAction(Context());
        AssertObject(policy.SelectAction(Context(time: 1.1))).IsNull();

        LlmSessionSummary summary = telemetry.GetSummary();
        AssertInt(summary.Calls).IsEqual(1);
        AssertInt(summary.Failures).IsEqual(1);
        AssertBool(summary.IsFlagged).IsTrue();
        AssertBool(File.ReadAllText(_telemetryPath).Contains("\"failed\":true")).IsTrue();
    }

    [TestCase]
    public void FreePolicy_RecordsAnInvalidProposalWithoutFixingIt()
    {
        var client = new FakeLlmClient(Respond("{\"action\":\"TeleportPlayer\"}"));
        var telemetry = NewTelemetry();
        var policy = new LlmFreePolicy(client, _options, _catalog, telemetry, _clock.Read);

        policy.SelectAction(Context());
        AssertObject(policy.SelectAction(Context(time: 1.1))).IsNull();

        LlmSessionSummary summary = telemetry.GetSummary();
        AssertInt(summary.Proposed).IsEqual(1);
        AssertInt(summary.Legal).IsEqual(0);
        AssertBool(File.ReadAllText(_telemetryPath).Contains("\"rejection\":\"unknownaction\"")).IsTrue();
    }

    [TestCase]
    public void BoundedPolicy_FallsBackToTheRulePolicyOnRejection()
    {
        var client = new FakeLlmClient(Respond("{\"action\":\"TeleportPlayer\"}"));
        var policy = new LlmBoundedPolicy(
            client, _options, _catalog, NewTelemetry(), _clock.Read, new RulePolicy(() => 0f));

        policy.SelectAction(Context());
        DirectorAction? action = policy.SelectAction(Context(time: 1.1));

        AssertObject(action).IsSame(_healthPack);
    }

    [TestCase]
    public void BoundedPolicy_FallsBackWhenTheCallFails()
    {
        var client = new FakeLlmClient(Fail(), Fail(), Fail());
        var policy = new LlmBoundedPolicy(
            client, _options, _catalog, NewTelemetry(), _clock.Read, new RulePolicy(() => 0f));

        policy.SelectAction(Context());
        AssertObject(policy.SelectAction(Context(time: 1.1))).IsSame(_healthPack);
    }

    [TestCase]
    public void Telemetry_AccountsTokensAndCost()
    {
        _options = new LlmPolicyOptions
        {
            TelemetryPath = _telemetryPath,
            InputCostPerMillionTokensUsd = 1.0,
            OutputCostPerMillionTokensUsd = 2.0
        };
        var telemetry = NewTelemetry();
        var client = new FakeLlmClient(Respond("{\"action\":\"SpawnHealthPack\"}"));
        var policy = new LlmFreePolicy(client, _options, _catalog, telemetry, _clock.Read);

        policy.SelectAction(Context());
        policy.SelectAction(Context(time: 1.1));

        LlmSessionSummary summary = telemetry.GetSummary();
        AssertInt((int)summary.PromptTokens).IsEqual(1000);
        AssertInt((int)summary.CompletionTokens).IsEqual(500);
        AssertFloat((float)summary.CostUsd).IsBetween(0.0019f, 0.0021f);
    }

    [TestCase]
    public void Samples_ContainTheStaticPrefixAndTheState()
    {
        var client = new FakeLlmClient(Respond("{\"action\":\"SpawnHealthPack\"}"));
        var policy = new LlmFreePolicy(client, _options, _catalog, NewTelemetry(), _clock.Read);

        policy.SelectAction(Context());

        string log = File.ReadAllText(_telemetryPath);
        AssertBool(log.Contains("\"type\":\"state_sample\"")).IsTrue();
        AssertBool(log.Contains("Catalog:")).IsTrue();

        string sampleLine = File.ReadAllLines(_telemetryPath).First(line => line.Contains("\"type\":\"state_sample\""));
        using JsonDocument sample = JsonDocument.Parse(sampleLine);
        string stateJson = sample.RootElement.GetProperty("state").GetString() ?? "";
        AssertBool(stateJson.Contains("\"state\":\"Reduce\"")).IsTrue();
    }
}

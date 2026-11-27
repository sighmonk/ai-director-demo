using GdUnit4;
using static GdUnit4.Assertions;
using System;
using System.IO;
using System.Linq;

namespace ProjectKitchen.Tests.AIDirector;

/// <summary>
/// JSONL decision records, token/cost totals, the legal rate and the 5% failed-call flag.
/// </summary>
[TestSuite]
public partial class LlmTelemetryTest
{
    private string _path = "";
    private LlmPolicyOptions _options = null!;

    [BeforeTest]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"llm-telemetry-{Guid.NewGuid():N}.jsonl");
        _options = new LlmPolicyOptions
        {
            TelemetryPath = _path,
            InputCostPerMillionTokensUsd = 2.0,
            OutputCostPerMillionTokensUsd = 8.0
        };
    }

    [AfterTest]
    public void Teardown()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    private static void Record(LlmTelemetry telemetry, bool legal = true, bool failed = false, int prompt = 1000, int completion = 250)
    {
        telemetry.RecordDecision(
            "LlmFree", 4.0, "Reduce", "SpawnHealthPack", legal ? "SpawnHealthPack" : null, legal,
            legal ? LlmRejectionReason.None : LlmRejectionReason.UnknownAction,
            120.0, false, prompt, completion, 0, failed);
    }

    [TestCase]
    public void Decision_WritesJsonlAndUpdatesTotals()
    {
        var telemetry = new LlmTelemetry(_options, "s1");
        Record(telemetry);
        Record(telemetry, legal: false);

        LlmSessionSummary summary = telemetry.GetSummary();
        AssertInt(summary.Calls).IsEqual(2);
        AssertInt(summary.Proposed).IsEqual(2);
        AssertInt(summary.Legal).IsEqual(1);
        AssertFloat((float)summary.LegalRate).IsEqual(0.5f);
        AssertInt((int)summary.PromptTokens).IsEqual(2000);
        AssertInt((int)summary.CompletionTokens).IsEqual(500);

        // 2000 prompt tokens at 2 USD/M plus 500 completion tokens at 8 USD/M.
        AssertFloat((float)summary.CostUsd).IsBetween(0.0079f, 0.0081f);

        string[] lines = File.ReadAllLines(_path);
        AssertInt(lines.Length).IsEqual(2);
        AssertBool(lines[0].Contains("\"type\":\"decision\"")).IsTrue();
        AssertBool(lines[1].Contains("\"legal\":false")).IsTrue();
        AssertBool(lines[1].Contains("\"rejection\":\"unknownaction\"")).IsTrue();
    }

    [TestCase]
    public void FailedCallRate_AboveFivePercent_FlagsTheSession()
    {
        var flagged = new LlmTelemetry(_options, "flagged");
        for (int i = 0; i < 100; i++) Record(flagged, legal: false, failed: i < 6);
        AssertBool(flagged.GetSummary().IsFlagged).IsTrue();

        var clean = new LlmTelemetry(_options, "clean");
        for (int i = 0; i < 100; i++) Record(clean, legal: true, failed: i < 5);
        AssertBool(clean.GetSummary().IsFlagged).IsFalse();
    }

    [TestCase]
    public void Samples_AreCappedAtFifty()
    {
        var telemetry = new LlmTelemetry(_options, "samples");
        for (int i = 0; i < 60; i++)
        {
            telemetry.RecordSample("LlmBounded", i, $"prompt {i}", "{}");
        }

        string[] sampleLines = File.ReadAllLines(_path)
            .Where(line => line.Contains("\"type\":\"state_sample\""))
            .ToArray();
        AssertInt(sampleLines.Length).IsEqual(50);
    }

    [TestCase]
    public void WriteSummary_EmitsOneSessionRecord()
    {
        var telemetry = new LlmTelemetry(_options, "summary");
        Record(telemetry);
        telemetry.WriteSummary("LlmBounded");

        string[] lines = File.ReadAllLines(_path);
        AssertInt(lines.Length).IsEqual(2);
        AssertBool(lines[1].Contains("\"type\":\"session_summary\"")).IsTrue();
        AssertBool(lines[1].Contains("\"legal_rate\":1")).IsTrue();
        AssertBool(lines[1].Contains("\"flagged\":false")).IsTrue();
    }

    [TestCase]
    public void WithoutAPath_CountersStillWork()
    {
        var telemetry = new LlmTelemetry(new LlmPolicyOptions(), "no-path");
        Record(telemetry);
        AssertInt(telemetry.GetSummary().Calls).IsEqual(1);
    }
}

using GdUnit4;
using static GdUnit4.Assertions;

namespace ProjectKitchen.Tests.Simulation;

/// <summary>
/// Command line parsing for the headless calibration run.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class CalibrationOptionsTest
{
    [TestCase]
    public void Parse_WithoutAProfile_ReturnsNull()
    {
        AssertObject(CalibrationOptions.Parse(new[] { "--calibration-seed=4" })).IsNull();
        AssertObject(CalibrationOptions.Parse(null)).IsNull();
    }

    [TestCase]
    public void Parse_UnknownProfile_ReturnsNull()
    {
        AssertObject(CalibrationOptions.Parse(new[] { "--calibration-profile=wizard" })).IsNull();
    }

    [TestCase]
    public void Parse_ReadsProfileSeedSessionAndOutput()
    {
        var options = CalibrationOptions.Parse(new[]
        {
            "--calibration-profile=Expert",
            "--calibration-seed=7",
            "--calibration-seconds=120",
            "--calibration-out=C:\\tmp\\calibration.jsonl"
        });

        AssertObject(options).IsNotNull();
        AssertString(options!.ProfileName).IsEqual("expert");
        AssertInt(options.Seed).IsEqual(7);
        AssertInt(options.SessionSeconds).IsEqual(120);
        AssertString(options.OutputPath).IsEqual("C:\\tmp\\calibration.jsonl");
        AssertBool(options.StaticArm).IsTrue();
    }

    [TestCase]
    public void Parse_DefaultsToSeedOneAnd600Seconds()
    {
        var options = CalibrationOptions.Parse(new[]
        {
            "--calibration-profile=novice",
            "--calibration-out=C:\\tmp\\calibration.jsonl"
        });

        AssertObject(options).IsNotNull();
        AssertInt(options!.Seed).IsEqual(1);
        AssertInt(options.SessionSeconds).IsEqual(600);
    }
}

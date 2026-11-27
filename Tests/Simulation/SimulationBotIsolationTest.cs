using GdUnit4;
using static GdUnit4.Assertions;
using Godot;

namespace ProjectKitchen.Tests.Simulation;

/// <summary>
/// The bot must stay outside the Director: it may read public gameplay state only.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class SimulationBotIsolationTest
{
    [TestCase]
    public void BotSource_DoesNotReadDirectorData()
    {
        foreach (string path in new[] { "res://Src/Simulation/SimulationBot.cs", "res://Src/Simulation/BotSkillProfile.cs" })
        {
            string source = FileAccess.GetFileAsString(path);
            AssertBool(source.Length > 0).IsTrue();
            AssertBool(source.Contains("DirectorData")).IsFalse();
            AssertBool(source.Contains("PerformanceMetricsUpdated")).IsFalse();
        }
    }
}

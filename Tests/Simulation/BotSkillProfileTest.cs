using GdUnit4;
using static GdUnit4.Assertions;

namespace ProjectKitchen.Tests.Simulation;

/// <summary>
/// Skill profile ordering, presets and the sandbag degradation used by the calibration.
/// </summary>
[TestSuite]
public partial class BotSkillProfileTest
{
    [TestCase]
    public void FromName_MapsEveryCalibrationProfile()
    {
        AssertString(BotSkillProfile.FromName("novice")!.Name).IsEqual("novice");
        AssertString(BotSkillProfile.FromName("Average")!.Name).IsEqual("average");
        AssertString(BotSkillProfile.FromName("EXPERT")!.Name).IsEqual("expert");
        AssertString(BotSkillProfile.FromName("sandbagger")!.Name).IsEqual("sandbagger");
        AssertObject(BotSkillProfile.FromName("unknown")).IsNull();
        AssertObject(BotSkillProfile.FromName(null)).IsNull();
    }

    [TestCase]
    public void Presets_GetStrictlyBetterFromNoviceToExpert()
    {
        BotSkillProfile novice = BotSkillProfile.Novice;
        BotSkillProfile average = BotSkillProfile.Average;
        BotSkillProfile expert = BotSkillProfile.Expert;

        AssertFloat(novice.ReactionTime).IsGreater(average.ReactionTime);
        AssertFloat(average.ReactionTime).IsGreater(expert.ReactionTime);
        AssertFloat(novice.AimErrorDegrees).IsGreater(average.AimErrorDegrees);
        AssertFloat(average.AimErrorDegrees).IsGreater(expert.AimErrorDegrees);
        AssertFloat(novice.FireInterval).IsGreater(average.FireInterval);
        AssertFloat(average.FireInterval).IsGreater(expert.FireInterval);
        AssertFloat(novice.MoveSpeedFactor).IsLess(average.MoveSpeedFactor);
        AssertFloat(average.MoveSpeedFactor).IsLess(expert.MoveSpeedFactor);
    }

    [TestCase]
    public void Degrade_WorsensReactionAimAndMovement()
    {
        BotSkillProfile degraded = BotSkillProfile.Degrade(BotSkillProfile.Expert);

        AssertFloat(degraded.ReactionTime).IsGreater(BotSkillProfile.Expert.ReactionTime);
        AssertFloat(degraded.AimErrorDegrees).IsGreater(BotSkillProfile.Expert.AimErrorDegrees);
        AssertFloat(degraded.FireInterval).IsGreater(BotSkillProfile.Expert.FireInterval);
        AssertFloat(degraded.MoveSpeedFactor).IsLess(BotSkillProfile.Expert.MoveSpeedFactor);
        AssertFloat(degraded.ThreatAvoidance).IsLess(BotSkillProfile.Expert.ThreatAvoidance);
    }

    [TestCase]
    public void Sandbagger_PlaysBelowItsProfileForTheFirst150Seconds()
    {
        AssertFloat(BotSkillProfile.Sandbagger.SandbagUntilSeconds).IsEqual(150f);
        AssertFloat(BotSkillProfile.Expert.SandbagUntilSeconds).IsEqual(0f);
    }
}

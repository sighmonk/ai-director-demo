using Godot;
using System;

/// <summary>
/// Competence parameters of a simulated player. All profiles share one policy; only these values
/// differ, which keeps the calibration comparable across profiles.
/// </summary>
public sealed class BotSkillProfile
{
    public string Name { get; init; } = "average";

    /// <summary>Seconds between target re-evaluations, i.e. the reaction delay.</summary>
    public float ReactionTime { get; init; } = 0.5f;

    /// <summary>Maximum aiming error in degrees.</summary>
    public float AimErrorDegrees { get; init; } = 10f;

    public float PreferredDistanceMin { get; init; } = 80f;

    public float PreferredDistanceMax { get; init; } = 150f;

    /// <summary>Seconds between shots.</summary>
    public float FireInterval { get; init; } = 0.5f;

    /// <summary>Fraction of the player's top speed the bot is willing to use.</summary>
    public float MoveSpeedFactor { get; init; } = 0.6f;

    /// <summary>Whether the bot strafes instead of holding position while inside its distance band.</summary>
    public bool StrafeWhileInBand { get; init; } = true;

    /// <summary>Enemies closer than this radius push the bot away (0 disables crowd avoidance).</summary>
    public float ThreatRadius { get; init; }

    /// <summary>Strength of the crowd-avoidance push.</summary>
    public float ThreatAvoidance { get; init; }

    /// <summary>Enemies closer than this radius force an immediate retreat.</summary>
    public float DangerRadius { get; init; }

    public float PerceptionRadius { get; init; } = 700f;

    /// <summary>Seconds during which the sandbagger deliberately plays below its profile.</summary>
    public float SandbagUntilSeconds { get; init; }

    public static BotSkillProfile Novice => new()
    {
        Name = "novice",
        ReactionTime = 0.8f,
        AimErrorDegrees = 8f,
        PreferredDistanceMin = 0f,
        PreferredDistanceMax = 100f,
        FireInterval = 0.55f,
        MoveSpeedFactor = 0.55f,
        StrafeWhileInBand = false,
        ThreatRadius = 60f,
        ThreatAvoidance = 0.1f,
        DangerRadius = 10f
    };

    public static BotSkillProfile Average => new()
    {
        Name = "average",
        ReactionTime = 0.55f,
        AimErrorDegrees = 6f,
        PreferredDistanceMin = 0f,
        PreferredDistanceMax = 170f,
        FireInterval = 0.4f,
        MoveSpeedFactor = 0.65f,
        StrafeWhileInBand = false,
        ThreatRadius = 120f,
        ThreatAvoidance = 0.4f,
        DangerRadius = 90f
    };

    public static BotSkillProfile Expert => new()
    {
        Name = "expert",
        ReactionTime = 0.3f,
        AimErrorDegrees = 4.5f,
        PreferredDistanceMin = 0f,
        PreferredDistanceMax = 215f,
        FireInterval = 0.3f,
        MoveSpeedFactor = 0.7f,
        StrafeWhileInBand = false,
        ThreatRadius = 140f,
        ThreatAvoidance = 0.5f,
        DangerRadius = 120f
    };

    public static BotSkillProfile Sandbagger => new()
    {
        Name = "sandbagger",
        ReactionTime = 0.3f,
        AimErrorDegrees = 4.5f,
        PreferredDistanceMin = 0f,
        PreferredDistanceMax = 215f,
        FireInterval = 0.3f,
        MoveSpeedFactor = 0.7f,
        StrafeWhileInBand = false,
        ThreatRadius = 140f,
        ThreatAvoidance = 0.5f,
        DangerRadius = 120f,
        SandbagUntilSeconds = 150f
    };

    public static BotSkillProfile? FromName(string? name)
    {
        return name?.Trim().ToLowerInvariant() switch
        {
            "novice" => Novice,
            "average" => Average,
            "expert" => Expert,
            "sandbagger" => Sandbagger,
            _ => null
        };
    }

    /// <summary>
    /// Deliberately worse parameters: slower reactions, wilder aim, no distance keeping and a slower
    /// trigger. Used by the sandbagger before <see cref="SandbagUntilSeconds"/>.
    /// </summary>
    public static BotSkillProfile Degrade(BotSkillProfile profile)
    {
        return new BotSkillProfile
        {
            Name = profile.Name + "-sandbag",
            ReactionTime = Math.Max(profile.ReactionTime * 3f, 0.6f),
            AimErrorDegrees = Math.Max(profile.AimErrorDegrees * 3.5f, 14f),
            PreferredDistanceMin = 90f,
            PreferredDistanceMax = 150f,
            FireInterval = Math.Max(profile.FireInterval * 2f, 0.45f),
            MoveSpeedFactor = Math.Max(profile.MoveSpeedFactor * 0.7f, 0.2f),
            StrafeWhileInBand = false,
            ThreatRadius = 100f,
            ThreatAvoidance = 0.3f,
            DangerRadius = 80f,
            PerceptionRadius = profile.PerceptionRadius
        };
    }
}

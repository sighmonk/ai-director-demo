using System;

/// <summary>
/// Game state that the LLM policies put after the static prompt prefix. It is built by the
/// Director, so policies never reach into <c>DirectorData</c> themselves.
/// </summary>
public sealed class DirectorGameState
{
    public DirectorGameState(
        double ratio,
        float healthRatio,
        float killRate,
        float damageRate,
        int livingEnemies,
        double sessionTime)
    {
        Ratio = ratio;
        HealthRatio = healthRatio;
        KillRate = killRate;
        DamageRate = damageRate;
        LivingEnemies = livingEnemies;
        SessionTime = sessionTime;
    }

    public double Ratio { get; }

    public float HealthRatio { get; }

    public float KillRate { get; }

    public float DamageRate { get; }

    public int LivingEnemies { get; }

    public double SessionTime { get; }
}

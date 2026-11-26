using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Baseline policy: weighted random draw over the eligible actions, reproducing the
/// original Director behaviour. The random source is injectable so selection can be
/// seeded and replayed by the experiment runner.
/// </summary>
public sealed class RulePolicy : IActionPolicy
{
    private readonly Func<float> _sampleUnitInterval;

    /// <param name="sampleUnitInterval">Returns a value in [0, 1]; defaults to GD.Randf().</param>
    public RulePolicy(Func<float>? sampleUnitInterval = null)
    {
        _sampleUnitInterval = sampleUnitInterval ?? (() => GD.Randf());
    }

    public string Name => "Rule";

    public DirectorAction? SelectAction(ActionSelectionContext context)
    {
        if (context?.Candidates == null || context.Candidates.Count == 0) return null;

        var eligible = new List<DirectorAction>(context.Candidates.Count);
        float totalWeight = 0f;
        foreach (var candidate in context.Candidates)
        {
            if (candidate == null || candidate.Weight <= 0f) continue;
            eligible.Add(candidate);
            totalWeight += candidate.Weight;
        }

        if (eligible.Count == 0 || totalWeight <= 0f) return null;

        float roll = Mathf.Clamp(_sampleUnitInterval(), 0f, 1f) * totalWeight;
        float accumulated = 0f;
        foreach (var candidate in eligible)
        {
            accumulated += candidate.Weight;
            if (roll <= accumulated) return candidate;
        }

        // Floating-point drift when the sample lands on the upper bound.
        return eligible[eligible.Count - 1];
    }
}

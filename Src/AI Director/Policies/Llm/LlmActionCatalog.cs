using System;
using System.Collections.Generic;
using System.Text;

public sealed class LlmActionEntry
{
    public LlmActionEntry(string id, ActionType type, float cost, float cooldown, string description)
    {
        Id = id;
        Type = type;
        Cost = cost;
        Cooldown = cooldown;
        Description = description;
    }

    public string Id { get; }

    public ActionType Type { get; }

    public float Cost { get; }

    public float Cooldown { get; }

    public string Description { get; }
}

/// <summary>
/// The action catalog shared by both LLM arms. The catalog part of the prompt is static (it never
/// depends on the game state) so it can be sent first and cached by the provider.
/// </summary>
public sealed class LlmActionCatalog
{
    private readonly Dictionary<string, LlmActionEntry> _entries = new(StringComparer.Ordinal);

    public LlmActionCatalog(IEnumerable<DirectorAction> actions)
    {
        if (actions == null) return;

        foreach (DirectorAction action in actions)
        {
            if (action == null) continue;

            string id = action.Name;
            if (string.IsNullOrWhiteSpace(id)) continue;
            _entries[id] = new LlmActionEntry(id, action.ActionType, action.Cost, action.Cooldown, Describe(id));
        }
    }

    public IReadOnlyCollection<LlmActionEntry> Entries => _entries.Values;

    public bool Contains(string? id) => id != null && _entries.ContainsKey(id);

    public LlmActionEntry? Get(string? id)
    {
        return id != null && _entries.TryGetValue(id, out LlmActionEntry? entry) ? entry : null;
    }

    public string BuildStaticPrefix()
    {
        var builder = new StringBuilder();
        builder.AppendLine("You are the action-selection policy of a game difficulty director.");
        builder.AppendLine("Choose exactly one action from the catalog.");
        builder.AppendLine("Answer with a single JSON object and nothing else: {\"action\":\"<id>\"}.");
        builder.AppendLine("Never invent an id and never add an explanation.");
        builder.AppendLine();
        builder.AppendLine("Catalog:");

        var ids = new List<string>(_entries.Keys);
        ids.Sort(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            LlmActionEntry entry = _entries[id];
            string type = entry.Type == ActionType.Benefit ? "benefit" : "negative";
            builder.AppendLine($"- {entry.Id} ({type}, cost {entry.Cost:0.#}, cooldown {entry.Cooldown:0.#} s): {entry.Description}");
        }

        builder.AppendLine();
        builder.AppendLine("Rules:");
        builder.AppendLine("- Pick an action that is legal for the current state and point budget.");
        builder.AppendLine("- In Reduce, prefer benefit actions; in Intensify, prefer negative actions.");
        builder.AppendLine("- Do not repeat an action that is on cooldown.");
        return builder.ToString();
    }

    public static string Describe(string id)
    {
        return id switch
        {
            "IncreaseSpawnRate" => "make enemies spawn faster",
            "BuffEnemies" => "increase enemy speed, damage, scale and max HP",
            "SpawnHorde" => "spawn a horde of enemies around the player",
            "ReduceSpawnRate" => "slow enemy spawning",
            "DebuffEnemies" => "reduce enemy speed, damage, scale and max HP",
            "SpawnHealthPack" => "place a health pack on a reachable floor tile near the player",
            "AddRewardRoom" => "open a new room containing a health pack",
            "AddThreatRoom" => "open a new room containing extra enemies",
            "ClosePassage" => "seal one corridor passage for a while",
            _ => $"Director action {id}"
        };
    }
}

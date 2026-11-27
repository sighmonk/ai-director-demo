using Godot;
using System.Collections.Generic;

/// <summary>
/// Challenge action that seals one corridor passage for a while. Only passages that keep the whole
/// dungeon connected - and that do not belong to the player's room or strand an enemy - are used.
/// </summary>
public partial class ClosePassage : DirectorAction
{
    [Export(PropertyHint.Range, "0,10,1")] public int MaxClosuresPerSession = 1;
    /// <summary>Seconds after which the exact previous floor tiles are restored; 0 keeps the wall.</summary>
    [Export(PropertyHint.Range, "0,120,1")] public float ReopenAfterSeconds = 0f;
    /// <summary>Seed for the candidate pick (0 = always the first candidate).</summary>
    [Export] public ulong SelectionSeed = 0;

    public int Closures { get; private set; }

    private RandomNumberGenerator? _rng;

    public ClosePassage()
    {
        ActionType = ActionType.Negative;
    }

    public override bool CanExecute(float availablePoints, double currentTime)
    {
        return base.CanExecute(availablePoints, currentTime) && Closures < MaxClosuresPerSession;
    }

    protected override bool OnExecute()
    {
        var game = GetNodeOrNull<GameManager>("/root/GameManager");
        var mutator = game?.Dungeon?.Mutator;
        if (mutator == null)
        {
            GD.PrintErr($"[Director] {Name} rejected: no runtime dungeon mutator available.");
            return false;
        }

        List<DungeonPassage> passages = mutator.GetClosablePassages(avoidPlayerRoom: true, avoidStrandingEnemies: true);
        if (passages.Count == 0)
        {
            GD.Print($"[Director] {Name} rejected: no closable passage in the current dungeon.");
            return false;
        }

        DungeonPassage passage = Pick(passages);
        if (!mutator.TryClosePassage(passage, out List<Vector2I> closedTiles, out string failure))
        {
            GD.Print($"[Director] {Name} rejected passage {passage.Id}: {failure}");
            return false;
        }

        Closures++;
        double closedAt = game!.SessionTime;
        double reopenAt = ReopenAfterSeconds > 0f ? closedAt + ReopenAfterSeconds : 0.0;

        GD.Print($"[Director] {Name}: closed passage {passage.Id} ({closedTiles.Count} tiles, cost {Cost}).");
        SignalManager.Instance?.EmitSignal(
            nameof(SignalManager.DungeonPassageClosed),
            passage.Id,
            closedTiles.Count,
            closedAt,
            reopenAt);

        if (ReopenAfterSeconds > 0f) ScheduleReopen(mutator, passage.Id, closedTiles);
        return true;
    }

    private DungeonPassage Pick(List<DungeonPassage> passages)
    {
        if (SelectionSeed == 0) return passages[0];

        _rng ??= new RandomNumberGenerator { Seed = SelectionSeed };
        return passages[_rng.RandiRange(0, passages.Count - 1)];
    }

    private void ScheduleReopen(DungeonMutator mutator, int passageId, List<Vector2I> tiles)
    {
        var timer = new Timer
        {
            OneShot = true,
            WaitTime = ReopenAfterSeconds,
            Autostart = false
        };

        timer.Timeout += () =>
        {
            timer.QueueFree();
            if (!IsInstanceValid(mutator)) return;

            if (!mutator.ReopenPassage(tiles))
            {
                GD.PrintErr($"[Director] {Name}: could not reopen passage {passageId}.");
                return;
            }

            double reopenedAt = GetNodeOrNull<GameManager>("/root/GameManager")?.SessionTime ?? 0.0;
            GD.Print($"[Director] {Name}: reopened passage {passageId}.");
            SignalManager.Instance?.EmitSignal(
                nameof(SignalManager.DungeonPassageReopened),
                passageId,
                tiles.Count,
                reopenedAt);
        };

        AddChild(timer);
        timer.Start();
    }
}

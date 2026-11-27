using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Shared behaviour of the Director actions that carve a new room into the live dungeon. Candidate
/// search, validation and the session limit live here; subclasses decide what goes inside the room.
/// </summary>
public abstract partial class AddRoomAction : DirectorAction
{
    [Export] public int RoomSideMin = 5;
    [Export] public int RoomSideMax = 10;
    /// <summary>Minimum distance in tiles between the new room centre and the player.</summary>
    [Export(PropertyHint.Range, "0,40,1")] public int MinDistanceFromPlayer = 8;
    [Export(PropertyHint.Range, "0,10,1")] public int MaxRoomsAddedPerSession = 2;
    /// <summary>How many candidates are tried before the action gives up for this activation.</summary>
    [Export(PropertyHint.Range, "1,50,1")] public int MaxCandidateAttempts = 6;

    public int RoomsAdded { get; private set; }

    /// <summary>Short room kind used by the telemetry signal, for example "reward" or "threat".</summary>
    public abstract string Kind { get; }

    public override bool CanExecute(float availablePoints, double currentTime)
    {
        return base.CanExecute(availablePoints, currentTime) && RoomsAdded < MaxRoomsAddedPerSession;
    }

    protected abstract bool HasPrerequisites(out string failure);

    /// <summary>
    /// Fills the new room. <paramref name="model"/> already contains the room, so its floor cells can
    /// be queried. Returns the number of entities placed (used for telemetry).
    /// </summary>
    protected abstract int PlaceContents(DungeonWorldModel model, Rect2I room, Vector2I center);

    protected override bool OnExecute()
    {
        var game = GetNodeOrNull<GameManager>("/root/GameManager");
        var mutator = game?.Dungeon?.Mutator;
        if (mutator == null)
        {
            GD.PrintErr($"[Director] {Name} rejected: no runtime dungeon mutator available.");
            return false;
        }

        var model = mutator.RefreshModel();
        if (model == null)
        {
            GD.PrintErr($"[Director] {Name} rejected: the dungeon is not rendered yet.");
            return false;
        }

        if (!HasPrerequisites(out string missing))
        {
            GD.Print($"[Director] {Name} rejected: {missing}");
            return false;
        }

        var candidates = DungeonRoomPlacement.FindCandidates(
            model.Floors,
            model.Bounds,
            model.BlockedCells,
            model.PlayerCell,
            Math.Max(DungeonWorldModel.MinRoomSide, RoomSideMin),
            Math.Max(RoomSideMin, RoomSideMax),
            MinDistanceFromPlayer);

        if (candidates.Count == 0)
        {
            GD.Print($"[Director] {Name} rejected: no free area far enough from the player.");
            return false;
        }

        int attempts = Math.Min(MaxCandidateAttempts, candidates.Count);
        for (int i = 0; i < attempts; i++)
        {
            RoomCandidate candidate = candidates[i];
            if (!mutator.TryCreateRoom(candidate.Area, candidate.OpeningSide, out _, out string failure))
            {
                GD.Print($"[Director] {Name} candidate rejected at {candidate.Area.Position}: {failure}");
                continue;
            }

            RoomsAdded++;
            Rect2I room = model.LastCreatedRoom ?? candidate.Area;
            Vector2I center = model.LastCreatedRoomCenter;
            int placed = PlaceContents(model, room, center);

            GD.Print($"[Director] {Name}: {Kind} room {room.Size} at {room.Position}, {placed} entities (cost {Cost}).");
            SignalManager.Instance?.EmitSignal(
                nameof(SignalManager.DungeonRoomCreated),
                Kind,
                room.Position,
                room.Size,
                placed);
            return true;
        }

        GD.Print($"[Director] {Name} rejected: every candidate failed validation.");
        return false;
    }

    protected List<Vector2I> FloorCellsInside(DungeonWorldModel model, Rect2I room)
    {
        var cells = new List<Vector2I>(room.Size.X * room.Size.Y);
        for (int y = room.Position.Y; y < room.Position.Y + room.Size.Y; y++)
        {
            for (int x = room.Position.X; x < room.Position.X + room.Size.X; x++)
            {
                var cell = new Vector2I(x, y);
                if (model.IsFloor(cell)) cells.Add(cell);
            }
        }

        return cells;
    }
}

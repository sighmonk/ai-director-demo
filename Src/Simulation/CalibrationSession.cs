using Godot;
using System;
using System.IO;
using System.Text.Json;

/// <summary>
/// Runs one headless calibration session: binds the simulation bot to the spawned player, stops on
/// death or at the session limit and appends one JSONL record for the calibration report.
/// </summary>
public partial class CalibrationSession : Node
{
    private CalibrationOptions? _options;
    private Player? _player;
    private bool _finished;

    public bool IsActive => _options != null;

    public void Configure(CalibrationOptions options)
    {
        _options = options;
        SetProcess(true);
    }

    public void AttachPlayer(Player player)
    {
        if (_options == null) return;

        _player = player;
        BotSkillProfile profile = BotSkillProfile.FromName(_options.ProfileName)!;

        var bot = new SimulationBot { Name = "SimulationBot" };
        AddChild(bot);
        bot.Bind(player, profile, (ulong)_options.Seed);

        var health = player.GetNodeOrNull<HealthComponent>("HealthComponent");
        if (health != null) health.Died += OnPlayerDied;
    }

    public override void _Process(double delta)
    {
        if (_options == null || _finished || _player == null) return;

        GameManager? game = GetNodeOrNull<GameManager>("/root/GameManager");
        if (game == null) return;

        if (game.SessionTime >= _options.SessionSeconds)
        {
            Finish(censored: true);
        }
    }

    private void OnPlayerDied()
    {
        Finish(censored: false);
    }

    private void Finish(bool censored)
    {
        if (_options == null || _finished) return;
        _finished = true;

        GameManager? game = GetNodeOrNull<GameManager>("/root/GameManager");
        var performance = GetNodeOrNull<DataManager>("/root/DataManager")?.DirectorData?.Performance;

        double survived = game?.SessionTime ?? 0.0;
        var record = new
        {
            profile = _options.ProfileName,
            seed = _options.Seed,
            survived = Math.Round(survived, 3),
            session_seconds = _options.SessionSeconds,
            censored,
            kills = performance?.TotalKills ?? 0,
            damage = Math.Round(performance?.TotalDamageTaken ?? 0f, 2)
        };

        string directory = Path.GetDirectoryName(_options.OutputPath) ?? ".";
        Directory.CreateDirectory(directory);
        File.AppendAllText(_options.OutputPath, JsonSerializer.Serialize(record) + "\n");
        GD.Print($"[Calibration] {_options.ProfileName} seed {_options.Seed}: survived {survived:F1}s (censored: {censored}).");

        GetTree().Quit(0);
    }
}

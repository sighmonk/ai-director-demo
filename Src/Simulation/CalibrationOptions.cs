using Godot;
using System;
using System.IO;

/// <summary>
/// Command line configuration for the headless calibration run. Pass the options after <c>--</c>:
/// <c>godot --headless --path . --fixed-fps 60 -- --calibration-profile=novice
/// --calibration-seed=1 --calibration-seconds=600 --calibration-out=...</c>.
/// </summary>
public sealed class CalibrationOptions
{
    public const string ProfileArgument = "--calibration-profile";
    public const string SeedArgument = "--calibration-seed";
    public const string SecondsArgument = "--calibration-seconds";
    public const string OutputArgument = "--calibration-out";

    private CalibrationOptions(string profileName, int seed, int sessionSeconds, string outputPath)
    {
        ProfileName = profileName;
        Seed = seed;
        SessionSeconds = sessionSeconds;
        OutputPath = outputPath;
    }

    public string ProfileName { get; }

    public int Seed { get; }

    public int SessionSeconds { get; }

    public string OutputPath { get; }

    /// <summary>Calibration always measures the static arm S, so the Director stays disabled.</summary>
    public bool StaticArm => true;

    /// <summary>Options from the process command line, or null when this is not a calibration run.</summary>
    public static CalibrationOptions? Current { get; } = Parse(OS.GetCmdlineUserArgs());

    public static CalibrationOptions? Parse(string[]? arguments)
    {
        if (arguments == null) return null;

        string? profile = null;
        int seed = 1;
        int sessionSeconds = 600;
        string? output = null;

        foreach (string argument in arguments)
        {
            if (TryReadValue(argument, ProfileArgument, out string profileValue)) profile = profileValue;
            else if (TryReadValue(argument, SeedArgument, out string seedValue) && int.TryParse(seedValue, out int parsedSeed)) seed = parsedSeed;
            else if (TryReadValue(argument, SecondsArgument, out string secondsValue) && int.TryParse(secondsValue, out int parsedSeconds)) sessionSeconds = parsedSeconds;
            else if (TryReadValue(argument, OutputArgument, out string outputValue)) output = outputValue;
        }

        if (string.IsNullOrWhiteSpace(profile)) return null;
        if (BotSkillProfile.FromName(profile) == null)
        {
            GD.PrintErr($"[Calibration] Unknown profile '{profile}'.");
            return null;
        }

        if (seed <= 0) seed = 1;
        if (sessionSeconds <= 0) sessionSeconds = 600;

        string path = string.IsNullOrWhiteSpace(output)
            ? $"res://TestResults/calibration/{profile}-{seed}.jsonl"
            : output;

        if (!Path.IsPathRooted(path)) path = ProjectSettings.GlobalizePath(path);

        return new CalibrationOptions(profile.Trim().ToLowerInvariant(), seed, sessionSeconds, path);
    }

    private static bool TryReadValue(string argument, string name, out string value)
    {
        value = string.Empty;
        if (!argument.StartsWith(name, StringComparison.OrdinalIgnoreCase)) return false;

        int separator = argument.IndexOf('=');
        if (separator < 0 || separator == argument.Length - 1) return false;

        value = argument[(separator + 1)..];
        return value.Length > 0;
    }
}

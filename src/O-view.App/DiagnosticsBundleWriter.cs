using System.Text.Json;
using System.Text.Json.Serialization;
using OView.Core.Models;

namespace OView.App;

/// <summary>
/// Implements the "write a diagnostics bundle, with redaction" half of
/// <c>ISkinToShell.WriteDiagnosticsBundle()</c> (ADR-0007 D2 point 8, D6). Builds a
/// <see cref="DiagnosticsBundle"/> from the shell's own state (its settings, the most recent
/// snapshot), serializes it, and redacts the rendered text with
/// <see cref="DiagnosticsRedactor"/> before anything is written to disk — the file on disk is
/// never the unredacted form.
///
/// <para>Lives alongside the store directory (<see cref="StoreDirectoryResolver"/>) in its own
/// <c>diagnostics</c> subdirectory, written atomically (temp file, then replace) for the same
/// crash-mid-write reason <see cref="ShellSettingsStore"/> is.</para>
///
/// <para>Scope note: this type implements the writing and redaction only. Wiring it to a real
/// trigger (a tray menu item, a hotkey) is a later slice — ADR-0007 slice 8's own boundary.</para>
/// </summary>
public sealed class DiagnosticsBundleWriter
{
    private const string FileName = "diagnostics.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;
    private readonly IClock _clock;
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly string? _userName;
    private readonly Func<string> _getOsDescription;

    public DiagnosticsBundleWriter(
        string directory,
        IClock clock,
        Func<string, string?>? getEnvironmentVariable = null,
        string? userName = null,
        Func<string>? getOsDescription = null)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(clock);

        _directory = directory;
        _clock = clock;
        _getEnvironmentVariable = getEnvironmentVariable ?? Environment.GetEnvironmentVariable;
        _userName = userName ?? SafeGetUserName();
        _getOsDescription = getOsDescription ?? (() => System.Runtime.InteropServices.RuntimeInformation.OSDescription);
    }

    /// <summary>
    /// Builds, redacts, and writes one diagnostics bundle. Returns the path written to.
    /// </summary>
    public string Write(ShellSettings settings, UsageSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(snapshot);

        var bundle = new DiagnosticsBundle(
            GeneratedAtUtc: _clock.UtcNow,
            OsDescription: _getOsDescription(),
            StoreDirectory: _directory,
            AlertThresholdPercent: settings.AlertThresholdPercent,
            PollCadenceSeconds: (int)settings.PollCadence.TotalSeconds,
            AutoUpdateEnabled: settings.AutoUpdateEnabled,
            DataSourceKind: snapshot.DataSourceKind,
            LastIngestAt: snapshot.LastIngestAt,
            UsageLevel: snapshot.UsageLevel);

        var json = JsonSerializer.Serialize(bundle, SerializerOptions);
        var sensitiveValues = DiagnosticsRedactor.DefaultSensitiveValues(_getEnvironmentVariable, _userName);
        var redacted = DiagnosticsRedactor.Redact(json, sensitiveValues);

        var diagnosticsDirectory = Path.Combine(_directory, "diagnostics");
        Directory.CreateDirectory(diagnosticsDirectory);

        var path = Path.Combine(diagnosticsDirectory, FileName);
        var temp = path + ".tmp";
        File.WriteAllText(temp, redacted);
        File.Move(temp, path, overwrite: true);

        return path;
    }

    private static string? SafeGetUserName()
    {
        try
        {
            return Environment.UserName;
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
    }
}

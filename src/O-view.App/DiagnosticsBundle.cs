using OView.Core.Models;

namespace OView.App;

/// <summary>
/// The shape of one diagnostics bundle (ADR-0007 D2 point 8, D6's <c>WriteDiagnosticsBundle</c>).
/// Carries only what a reader needs to diagnose a report — process configuration, provider
/// state, the OS description — never raw vendor data (transcript content, environment variable
/// dumps) and never a pre-built sentence: the fields here are the same enum/scalar shapes the
/// Core-to-skin contract already uses (<see cref="DataSourceKind"/>, <see cref="UsageLevel"/>),
/// not prose. Redaction of PII-bearing values (the store directory's user-specific component)
/// is applied by <see cref="DiagnosticsBundleWriter"/> before this is written, not represented
/// as a field here.
/// </summary>
/// <param name="GeneratedAtUtc">When this bundle was produced.</param>
/// <param name="OsDescription">The OS/runtime description, for triage (e.g. "which Linux
/// distro"). Not a formatted sentence — <see cref="System.Runtime.InteropServices.RuntimeInformation.OSDescription"/>
/// verbatim.</param>
/// <param name="StoreDirectory">Where Core's stores and the shell's settings file live.
/// Redacted by the writer before this value is ever serialized.</param>
/// <param name="AlertThresholdPercent">The persisted behaviour setting (ADR-0007 D4).</param>
/// <param name="PollCadenceSeconds">The persisted poll cadence, in seconds.</param>
/// <param name="AutoUpdateEnabled">The persisted auto-update opt-in.</param>
/// <param name="DataSourceKind">The most recent snapshot's data source.</param>
/// <param name="LastIngestAt">The most recent snapshot's ingest time.</param>
/// <param name="UsageLevel">The most recent snapshot's threshold band.</param>
public sealed record DiagnosticsBundle(
    DateTimeOffset GeneratedAtUtc,
    string OsDescription,
    string StoreDirectory,
    int AlertThresholdPercent,
    int PollCadenceSeconds,
    bool AutoUpdateEnabled,
    DataSourceKind DataSourceKind,
    DateTimeOffset LastIngestAt,
    UsageLevel UsageLevel);

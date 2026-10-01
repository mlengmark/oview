using OView.Core.Models;

namespace OView.App.Tests;

/// <summary>
/// Covers <see cref="DiagnosticsBundleWriter"/> end to end against a temp directory: the bundle
/// lands where expected, round-trips the non-sensitive fields, and — the point of this slice —
/// a known-sensitive value (a fake OS user name baked into the store directory and the injected
/// environment) is absent from the written file. Every test uses its own temp directory, never a
/// real user profile path, same convention as <see cref="ShellSettingsStoreTests"/>.
/// </summary>
public class DiagnosticsBundleWriterTests : IDisposable
{
    private const string SensitiveUserName = "sensitive-test-user";

    private readonly string _rootDirectory;
    private readonly string _storeDirectory;

    public DiagnosticsBundleWriterTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), "oview-diagnostics-tests-" + Guid.NewGuid());
        _storeDirectory = Path.Combine(_rootDirectory, "Users", SensitiveUserName, "AppData", "Local", "O-view");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private DiagnosticsBundleWriter CreateWriter(IClock clock) => new(
        _storeDirectory,
        clock,
        getEnvironmentVariable: name => name switch
        {
            "USERNAME" => SensitiveUserName,
            "LOCALAPPDATA" => Path.Combine(_rootDirectory, "Users", SensitiveUserName, "AppData", "Local"),
            _ => null,
        },
        userName: SensitiveUserName,
        getOsDescription: () => "Test OS 1.0");

    private static UsageSnapshot Snapshot(DateTimeOffset lastIngestAt) => new(
        DataSourceKind.Live,
        lastIngestAt,
        new UsagePercent(55, UsageValueStatus.Real),
        new UsageInstant(lastIngestAt.AddHours(1), UsageValueStatus.Real),
        new UsagePercent(20, UsageValueStatus.Real),
        new UsageInstant(lastIngestAt.AddDays(1), UsageValueStatus.Real),
        UsageLevel.Amber);

    [Fact]
    public void Write_creates_the_bundle_file_under_a_diagnostics_subdirectory()
    {
        var writer = CreateWriter(new FakeClock(DateTimeOffset.UnixEpoch));

        var path = writer.Write(ShellSettings.Default, Snapshot(DateTimeOffset.UnixEpoch));

        Assert.True(File.Exists(path));
        Assert.Equal(
            Path.Combine(_storeDirectory, "diagnostics", "diagnostics.json"),
            path);
    }

    [Fact]
    public void Write_redacts_the_OS_user_name_from_the_store_directory_and_the_environment()
    {
        var writer = CreateWriter(new FakeClock(DateTimeOffset.UnixEpoch));

        var path = writer.Write(ShellSettings.Default, Snapshot(DateTimeOffset.UnixEpoch));
        var contents = File.ReadAllText(path);

        Assert.DoesNotContain(SensitiveUserName, contents);
        Assert.Contains(DiagnosticsRedactor.RedactedMarker, contents);
    }

    [Fact]
    public void Write_keeps_the_non_sensitive_fields_readable()
    {
        var generatedAt = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var writer = CreateWriter(new FakeClock(generatedAt));
        var settings = new ShellSettings(AlertThresholdPercent: 75, PollCadence: TimeSpan.FromSeconds(45), AutoUpdateEnabled: true);

        var path = writer.Write(settings, Snapshot(generatedAt));
        var contents = File.ReadAllText(path);

        Assert.Contains("\"alertThresholdPercent\": 75", contents);
        Assert.Contains("\"pollCadenceSeconds\": 45", contents);
        Assert.Contains("\"autoUpdateEnabled\": true", contents);
        Assert.Contains("\"dataSourceKind\": \"Live\"", contents);
        Assert.Contains("\"usageLevel\": \"Amber\"", contents);
        Assert.Contains("Test OS 1.0", contents);
    }

    [Fact]
    public void Write_overwrites_a_previously_written_bundle()
    {
        var writer = CreateWriter(new FakeClock(DateTimeOffset.UnixEpoch));
        writer.Write(ShellSettings.Default, Snapshot(DateTimeOffset.UnixEpoch));

        var secondGeneratedAt = DateTimeOffset.UnixEpoch.AddDays(1);
        var secondWriter = CreateWriter(new FakeClock(secondGeneratedAt));
        var path = secondWriter.Write(ShellSettings.Default, Snapshot(secondGeneratedAt));
        var contents = File.ReadAllText(path);

        Assert.Contains(secondGeneratedAt.ToString("yyyy-MM-dd"), contents);
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset utcNow) => UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; }
    }
}

using System.Net.Http;
using OView.App.Updates;
using OView.Core.Updates;

namespace OView.App.Tests.Updates;

public class ReleaseFeedTests
{
    private static readonly ReleaseAssetSelector Installer = ReleaseAssets.WindowsInstaller;

    [Fact]
    public async Task Repeated_checks_within_the_cooldown_window_do_not_re_fetch()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var transport = new FakeTransport(new ReleaseFeedResponse(429, "0", null, "3600", ""));
        var feed = new ReleaseFeed(transport, clock);

        var first = await feed.CheckAsync("0.4.0", Installer);
        Assert.Equal(UpdateOutcome.RateLimited, first.Outcome);
        Assert.Equal(1, transport.CallCount);

        // Still well inside the cooldown window the first response recorded.
        clock.UtcNow = clock.UtcNow.AddMinutes(10);
        var second = await feed.CheckAsync("0.4.0", Installer);

        Assert.Equal(UpdateOutcome.RateLimited, second.Outcome);
        Assert.Equal(first.RetryAfterUtc, second.RetryAfterUtc);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task A_check_after_the_cooldown_expires_fetches_again()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var transport = new FakeTransport(new ReleaseFeedResponse(429, "0", null, "60", ""));
        var feed = new ReleaseFeed(transport, clock);

        await feed.CheckAsync("0.4.0", Installer);
        Assert.Equal(1, transport.CallCount);

        clock.UtcNow = clock.UtcNow.AddMinutes(2);
        transport.NextResponse = new ReleaseFeedResponse(200, null, null, null, UpToDateReleaseJson("v0.4.0"));
        var result = await feed.CheckAsync("0.4.0", Installer);

        Assert.Equal(2, transport.CallCount);
        Assert.Equal(UpdateOutcome.UpToDate, result.Outcome);
    }

    [Fact]
    public async Task One_instance_is_the_single_cooldown_holder_for_every_caller()
    {
        // Simulates two skins sharing the one shell-constructed ReleaseFeed instance
        // (ADR-0007 D3): both calls go through the same holder, so the second caller sees
        // the cooldown the first caller's check recorded, without either skin knowing about
        // the other.
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var transport = new FakeTransport(new ReleaseFeedResponse(429, "0", null, "3600", ""));
        var feed = new ReleaseFeed(transport, clock);

        var fromFirstSkin = await feed.CheckAsync("0.4.0", Installer);
        var fromSecondSkin = await feed.CheckAsync("0.4.0", ReleaseAssets.None);

        Assert.Equal(UpdateOutcome.RateLimited, fromFirstSkin.Outcome);
        Assert.Equal(UpdateOutcome.RateLimited, fromSecondSkin.Outcome);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task A_successful_check_clears_a_previously_recorded_cooldown()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var transport = new FakeTransport(new ReleaseFeedResponse(429, "0", null, "60", ""));
        var feed = new ReleaseFeed(transport, clock);

        await feed.CheckAsync("0.4.0", Installer);

        clock.UtcNow = clock.UtcNow.AddMinutes(2);
        transport.NextResponse = new ReleaseFeedResponse(200, null, null, null, UpToDateReleaseJson("v0.4.0"));
        await feed.CheckAsync("0.4.0", Installer);

        // No cooldown left: an immediate next call fetches rather than reporting RateLimited.
        transport.NextResponse = new ReleaseFeedResponse(200, null, null, null, UpToDateReleaseJson("v0.4.0"));
        var result = await feed.CheckAsync("0.4.0", Installer);

        Assert.Equal(UpdateOutcome.UpToDate, result.Outcome);
        Assert.Equal(3, transport.CallCount);
    }

    [Fact]
    public async Task A_transport_failure_is_Unknown_and_never_throws()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
        var transport = new FakeTransport(throwOnFetch: true);
        var feed = new ReleaseFeed(transport, clock);

        var result = await feed.CheckAsync("0.4.0", Installer);

        Assert.Equal(UpdateOutcome.Unknown, result.Outcome);
    }

    private static string UpToDateReleaseJson(string tag) =>
        $$"""{ "tag_name": "{{tag}}", "draft": false, "prerelease": false, "assets": [] }""";

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTimeOffset utcNow) => UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class FakeTransport : IReleaseFeedTransport
    {
        private readonly bool _throwOnFetch;

        public FakeTransport(ReleaseFeedResponse? nextResponse = null, bool throwOnFetch = false)
        {
            NextResponse = nextResponse;
            _throwOnFetch = throwOnFetch;
        }

        public ReleaseFeedResponse? NextResponse { get; set; }

        public int CallCount { get; private set; }

        public Task<ReleaseFeedResponse> FetchLatestReleaseAsync(CancellationToken cancellation)
        {
            CallCount++;

            if (_throwOnFetch)
            {
                throw new HttpRequestException("simulated network failure");
            }

            return Task.FromResult(NextResponse!);
        }
    }
}

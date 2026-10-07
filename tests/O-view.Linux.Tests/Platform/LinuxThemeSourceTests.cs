using OView.App;
using OView.Linux.Platform;

namespace OView.Linux.Tests.Platform;

/// <summary>
/// ADR-0009 D6/slice 8: the Linux theme read and its live-change subscription. Proves
/// <see cref="LinuxThemeSource"/> against a fake <see cref="IDesktopThemePortal"/> — no real
/// session bus or portal is reachable from this test runner or CI, the same boundary
/// <see cref="NotificationHostMonitorTests"/> already recorded for ADR-0008 D6.
/// </summary>
public class LinuxThemeSourceTests
{
    [Fact]
    public void BeforeStartAsync_CurrentIsUnknown_NeverGuessed()
    {
        var subject = new LinuxThemeSource(new FakeDesktopThemePortal(initial: 1));

        Assert.Equal(ThemePreference.Unknown, subject.Current);
    }

    [Fact]
    public async Task StartAsync_runs_the_initial_read_off_the_calling_thread()
    {
        var callingThreadId = Environment.CurrentManagedThreadId;
        int? readThreadId = null;
        var portal = new FakeDesktopThemePortal(initial: 1, onRead: () => readThreadId = Environment.CurrentManagedThreadId);
        var subject = new LinuxThemeSource(portal);

        await subject.StartAsync(CancellationToken.None);

        Assert.NotNull(readThreadId);
        Assert.NotEqual(callingThreadId, readThreadId);
    }

    [Theory]
    [InlineData(1u, ThemePreference.Dark)]
    [InlineData(2u, ThemePreference.Light)]
    [InlineData(0u, ThemePreference.Unknown)]
    public async Task StartAsync_mapsTheInitialRead(uint reported, ThemePreference expected)
    {
        var subject = new LinuxThemeSource(new FakeDesktopThemePortal(initial: reported));

        await subject.StartAsync(CancellationToken.None);

        Assert.Equal(expected, subject.Current);
    }

    [Fact]
    public async Task AbsentPortal_NeverReadReportsUnknown()
    {
        var subject = new LinuxThemeSource(new FakeDesktopThemePortal(initial: null));

        await subject.StartAsync(CancellationToken.None);

        Assert.Equal(ThemePreference.Unknown, subject.Current);
    }

    [Fact]
    public async Task LiveSettingChangedSignalUpdatesCurrentAndRaisesChanged()
    {
        var portal = new FakeDesktopThemePortal(initial: 1);
        var subject = new LinuxThemeSource(portal);
        ThemePreference? raised = null;
        subject.Changed += (_, preference) => raised = preference;

        await subject.StartAsync(CancellationToken.None);
        Assert.Equal(ThemePreference.Dark, subject.Current);

        portal.RaiseColorSchemeChanged(2);

        Assert.Equal(ThemePreference.Light, subject.Current);
        Assert.Equal(ThemePreference.Light, raised);
    }

    [Fact]
    public async Task InitialReadNeverRaisesChanged()
    {
        var portal = new FakeDesktopThemePortal(initial: 1);
        var subject = new LinuxThemeSource(portal);
        var raised = false;
        subject.Changed += (_, _) => raised = true;

        await subject.StartAsync(CancellationToken.None);

        Assert.False(raised);
    }

    [Fact]
    public async Task AFailedSubscriptionKeepsTheInitialReadInsteadOfThrowing()
    {
        var portal = new FakeDesktopThemePortal(initial: 1, watchFailure: new InvalidOperationException("no bus"));
        var subject = new LinuxThemeSource(portal);

        await subject.StartAsync(CancellationToken.None);

        Assert.Equal(ThemePreference.Dark, subject.Current);
    }

    [Fact]
    public async Task DisposeAsync_disposesTheSubscriptionAndThePortal()
    {
        var portal = new FakeDesktopThemePortal(initial: 1);
        var subject = new LinuxThemeSource(portal);
        await subject.StartAsync(CancellationToken.None);

        await subject.DisposeAsync();

        Assert.True(portal.SubscriptionDisposed);
        Assert.True(portal.Disposed);
    }

    private sealed class FakeDesktopThemePortal : IDesktopThemePortal
    {
        private readonly uint? _initial;
        private readonly Action? _onRead;
        private readonly Exception? _watchFailure;
        private Action<uint>? _onChanged;
        private FakeSubscription? _subscription;

        public FakeDesktopThemePortal(uint? initial, Action? onRead = null, Exception? watchFailure = null)
        {
            _initial = initial;
            _onRead = onRead;
            _watchFailure = watchFailure;
        }

        public bool SubscriptionDisposed => _subscription?.Disposed ?? false;
        public bool Disposed { get; private set; }

        public Task<uint?> ReadColorSchemeAsync(CancellationToken cancellationToken)
        {
            _onRead?.Invoke();
            return Task.FromResult(_initial);
        }

        public Task<IDisposable> WatchColorSchemeChangedAsync(Action<uint> onChanged, CancellationToken cancellationToken)
        {
            if (_watchFailure is not null)
            {
                throw _watchFailure;
            }

            _onChanged = onChanged;
            _subscription = new FakeSubscription();
            return Task.FromResult<IDisposable>(_subscription);
        }

        public void RaiseColorSchemeChanged(uint scheme) => _onChanged?.Invoke(scheme);

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }

        private sealed class FakeSubscription : IDisposable
        {
            public bool Disposed { get; private set; }
            public void Dispose() => Disposed = true;
        }
    }
}

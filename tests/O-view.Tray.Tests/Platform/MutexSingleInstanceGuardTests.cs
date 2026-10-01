using OView.Tray.Platform;

namespace OView.Tray.Tests.Platform;

/// <summary>
/// The Windows single-instance mechanism. Each test uses its own GUID-suffixed mutex name
/// so tests never collide with each other, a real running O-view, or a parallel test run.
/// </summary>
public class MutexSingleInstanceGuardTests
{
    private static string UniqueName() => "OView.Tray.SingleInstance.Tests." + Guid.NewGuid();

    [Fact]
    public void FirstInstanceAcquires()
    {
        using var guard = new MutexSingleInstanceGuard(UniqueName());

        Assert.True(guard.TryAcquire());
    }

    [Fact]
    public void SecondInstanceIsRefusedWhileTheFirstHolds()
    {
        var name = UniqueName();

        using var first = new MutexSingleInstanceGuard(name);
        Assert.True(first.TryAcquire());

        using var second = new MutexSingleInstanceGuard(name);
        Assert.False(second.TryAcquire());
    }

    [Fact]
    public void ReleasingLetsTheNextInstanceIn()
    {
        var name = UniqueName();

        var first = new MutexSingleInstanceGuard(name);
        Assert.True(first.TryAcquire());
        first.Dispose();

        using var second = new MutexSingleInstanceGuard(name);
        Assert.True(second.TryAcquire());
    }

    [Fact]
    public void AcquiringTwiceFromTheSameGuardIsNotASecondInstance()
    {
        using var guard = new MutexSingleInstanceGuard(UniqueName());

        Assert.True(guard.TryAcquire());
        Assert.True(guard.TryAcquire());
    }

    [Fact]
    public void DisposingWithoutAcquiringIsSafe()
    {
        // The losing instance shuts down through the same path as the winner, so Dispose has
        // to tolerate never having held anything.
        var guard = new MutexSingleInstanceGuard(UniqueName());
        guard.Dispose();
    }

    [Fact]
    public void DefaultNameIsStable()
    {
        Assert.Equal("OView.Tray.SingleInstance", MutexSingleInstanceGuard.DefaultName);
    }
}

namespace OView.App.Tests;

/// <summary>
/// Covers ADR-0007 D6's skin-to-shell direction against fakes only: a driving harness
/// stands in for the skin and calls <see cref="ISkinToShell"/> exactly as a skin would, and a
/// fake shell records what it received. This is a structural/contract test proving the seam
/// carries the right calls with the right data — not a real UI integration (no WPF/Avalonia
/// wiring; that is a later slice).
/// </summary>
public class SkinToShellSeamTests
{
    [Fact]
    public void Driving_skin_calls_RefreshNow_and_the_shell_observes_it()
    {
        var shell = new FakeShell();

        DriveAsSkin(shell).RefreshNow();

        Assert.True(shell.RefreshNowCalled);
    }

    [Fact]
    public void Driving_skin_calls_SetThresholdPercent_and_the_shell_receives_the_exact_value()
    {
        var shell = new FakeShell();

        DriveAsSkin(shell).SetThresholdPercent(85);

        Assert.Equal(85, shell.LastThresholdPercent);
    }

    [Fact]
    public void Driving_skin_calls_SetAutoUpdate_and_the_shell_receives_the_exact_value()
    {
        var shell = new FakeShell();

        DriveAsSkin(shell).SetAutoUpdate(true);

        Assert.True(shell.LastAutoUpdate);
    }

    [Fact]
    public void Driving_skin_calls_WriteDiagnosticsBundle_and_the_shell_observes_it()
    {
        var shell = new FakeShell();

        DriveAsSkin(shell).WriteDiagnosticsBundle();

        Assert.True(shell.WriteDiagnosticsBundleCalled);
    }

    [Fact]
    public void Driving_skin_calls_Quit_and_the_shell_observes_it()
    {
        var shell = new FakeShell();

        DriveAsSkin(shell).Quit();

        Assert.True(shell.QuitCalled);
    }

    /// <summary>Stands in for the skin: accepts the interface, not the concrete fake, so
    /// this test proves the call goes through the contract rather than a direct reference.</summary>
    private static ISkinToShell DriveAsSkin(ISkinToShell shell) => shell;

    private sealed class FakeShell : ISkinToShell
    {
        public bool RefreshNowCalled { get; private set; }

        public int? LastThresholdPercent { get; private set; }

        public bool? LastAutoUpdate { get; private set; }

        public bool WriteDiagnosticsBundleCalled { get; private set; }

        public bool QuitCalled { get; private set; }

        public void RefreshNow() => RefreshNowCalled = true;

        public void SetThresholdPercent(int percent) => LastThresholdPercent = percent;

        public void SetAutoUpdate(bool enabled) => LastAutoUpdate = enabled;

        public void WriteDiagnosticsBundle() => WriteDiagnosticsBundleCalled = true;

        public void Quit() => QuitCalled = true;
    }
}

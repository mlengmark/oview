using OView.App;
using OView.Linux.Presentation;

namespace OView.Linux.Tests.Presentation;

/// <summary>ADR-0009 slice 10's repaint wiring, proved against a fake <see cref="IThemeSource"/>
/// — no real window, no real NativeMenu — mirroring
/// <c>O-view.Tray.Tests.Presentation.ThemeRepaintControllerTests</c>' shape.</summary>
public class LinuxThemeRepaintControllerTests
{
    [Fact]
    public void ConstructorAppliesTheCurrentThemeImmediately()
    {
        var themeSource = new FakeThemeSource(ThemePreference.Dark);
        var applied = new List<LinuxWindowThemeColors>();

        _ = new LinuxThemeRepaintController(themeSource, applied.Add);

        Assert.Equal(new[] { LinuxWindowThemePalette.Resolve(ThemePreference.Dark) }, applied);
    }

    [Fact]
    public void ChangedEventRepaintsWithTheNewlyReadPreference()
    {
        var themeSource = new FakeThemeSource(ThemePreference.Light);
        var applied = new List<LinuxWindowThemeColors>();
        _ = new LinuxThemeRepaintController(themeSource, applied.Add);

        themeSource.RaiseChanged(ThemePreference.Dark);

        Assert.Equal(LinuxWindowThemePalette.Resolve(ThemePreference.Dark), applied[^1]);
    }

    [Fact]
    public void UnknownPreferenceRepaintsWithTheDefinedFallbackRatherThanThrowing()
    {
        var themeSource = new FakeThemeSource(ThemePreference.Light);
        var applied = new List<LinuxWindowThemeColors>();
        _ = new LinuxThemeRepaintController(themeSource, applied.Add);

        themeSource.RaiseChanged(ThemePreference.Unknown);

        Assert.Equal(LinuxWindowThemePalette.Resolve(ThemePreference.Unknown), applied[^1]);
    }

    [Fact]
    public void DisposeUnsubscribesFromTheThemeSource()
    {
        var themeSource = new FakeThemeSource(ThemePreference.Light);
        var applied = new List<LinuxWindowThemeColors>();
        var controller = new LinuxThemeRepaintController(themeSource, applied.Add);
        var countAfterConstruction = applied.Count;

        controller.Dispose();
        themeSource.RaiseChanged(ThemePreference.Dark);

        Assert.Equal(countAfterConstruction, applied.Count);
    }

    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new LinuxThemeRepaintController(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => new LinuxThemeRepaintController(new FakeThemeSource(ThemePreference.Light), null!));
    }

    private sealed class FakeThemeSource : IThemeSource
    {
        public FakeThemeSource(ThemePreference current) => Current = current;

        public ThemePreference Current { get; private set; }

        public event EventHandler<ThemePreference>? Changed;

        public void RaiseChanged(ThemePreference preference)
        {
            Current = preference;
            Changed?.Invoke(this, preference);
        }
    }
}

using OView.App;
using OView.Tray.Presentation;

namespace OView.Tray.Tests.Presentation;

/// <summary>ADR-0009 slice 7 (OVI-489)'s repaint wiring, proved against a fake
/// <see cref="IThemeSource"/> — no real window, no real ContextMenuStrip.</summary>
public class ThemeRepaintControllerTests
{
    [Fact]
    public void ConstructorAppliesTheCurrentThemeImmediately()
    {
        var themeSource = new FakeThemeSource(ThemePreference.Dark);
        var applied = new List<WindowThemeColors>();

        _ = new ThemeRepaintController(themeSource, applied.Add);

        Assert.Equal(new[] { WindowThemePalette.Resolve(ThemePreference.Dark) }, applied);
    }

    [Fact]
    public void ChangedEventRepaintsWithTheNewlyReadPreference()
    {
        var themeSource = new FakeThemeSource(ThemePreference.Light);
        var applied = new List<WindowThemeColors>();
        _ = new ThemeRepaintController(themeSource, applied.Add);

        themeSource.RaiseChanged(ThemePreference.Dark);

        Assert.Equal(WindowThemePalette.Resolve(ThemePreference.Dark), applied[^1]);
    }

    [Fact]
    public void UnknownPreferenceRepaintsWithTheDefinedFallbackRatherThanThrowing()
    {
        var themeSource = new FakeThemeSource(ThemePreference.Light);
        var applied = new List<WindowThemeColors>();
        _ = new ThemeRepaintController(themeSource, applied.Add);

        themeSource.RaiseChanged(ThemePreference.Unknown);

        Assert.Equal(WindowThemePalette.Resolve(ThemePreference.Unknown), applied[^1]);
    }

    [Fact]
    public void DisposeUnsubscribesFromTheThemeSource()
    {
        var themeSource = new FakeThemeSource(ThemePreference.Light);
        var applied = new List<WindowThemeColors>();
        var controller = new ThemeRepaintController(themeSource, applied.Add);
        var countAfterConstruction = applied.Count;

        controller.Dispose();
        themeSource.RaiseChanged(ThemePreference.Dark);

        Assert.Equal(countAfterConstruction, applied.Count);
    }

    [Fact]
    public void ConstructorRejectsNullDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new ThemeRepaintController(null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => new ThemeRepaintController(new FakeThemeSource(ThemePreference.Light), null!));
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

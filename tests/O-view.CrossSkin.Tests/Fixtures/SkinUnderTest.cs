using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// One skin's own string-construction entry point, wired into the harness so every
/// fixture is exercised against every skin from one test run (ADR-0003). Adding a skin
/// here does not give it knowledge of another skin's wording — <see cref="Format"/> is a
/// direct call into that skin's own presentation code.
/// </summary>
public sealed record SkinUnderTest(string Name, Func<UsageSnapshot, TimeZoneInfo, string> Format);

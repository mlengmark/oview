using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// One skin's own <c>BoostChip</c>/<c>BoostCard</c> entry points, wired into the harness
/// together because — unlike <see cref="PanelTextResetSkinUnderTest"/>'s four raw-scalar
/// members — both share one input shape (<see cref="BoostNotice"/>, one
/// <see cref="DateTimeOffset"/> whose role differs per member, one <see cref="TimeZoneInfo"/>;
/// ADR-0003's 2026-09-21 amendment).
/// </summary>
public sealed record BoostNoticeSkinUnderTest(
    string Name,
    Func<BoostNotice, DateTimeOffset, TimeZoneInfo, string> BoostChip,
    Func<BoostNotice, DateTimeOffset, TimeZoneInfo, string> BoostCard);

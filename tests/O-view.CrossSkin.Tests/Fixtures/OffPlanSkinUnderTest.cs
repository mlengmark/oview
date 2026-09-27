using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// One skin's own off-plan-banner entry points (ADR-0003, OVI-168): the heading, the detail
/// line, the tile sub-label, the flipping "Est. value/spend today" label, and the hover hint.
/// Wired together the same way <see cref="BoostNoticeSkinUnderTest"/> groups a shared-shape
/// family — <c>OffPlanTitle</c> and <c>OffPlanDetail</c> share one input shape, and
/// <c>OffPlanNote</c>/<c>EstTodayLabel</c>/<c>OffPlanHint</c> each take a single bool.
/// </summary>
/// <param name="UsageSettingsUrl">
/// The link Claude's own usage settings page lives at (source app's constant of the same
/// name). ADR-0001's amendment deliberately leaves this skin-owned and duplicated rather than
/// promoting it to a Core constant — carried here so the harness can pin that both skins still
/// agree on the value, which is the drift this ADR-0003 harness exists to catch.
/// </param>
public sealed record OffPlanSkinUnderTest(
    string Name,
    Func<DivergenceReading, ExtraUsageReading?, string> OffPlanTitle,
    Func<DivergenceReading, ExtraUsageReading?, DateTimeOffset, TimeZoneInfo, string> OffPlanDetail,
    Func<bool, string> OffPlanNote,
    Func<bool, string> EstTodayLabel,
    Func<bool, string> OffPlanHint,
    string UsageSettingsUrl);

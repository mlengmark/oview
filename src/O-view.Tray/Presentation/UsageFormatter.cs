using System.Globalization;
using OView.Core.Models;

namespace OView.Tray.Presentation;

/// <summary>
/// Builds display strings for token counts and estimated USD values from the
/// <see cref="TokenCount"/>/<see cref="EstimatedUsd"/> slice of the Core-to-skin data
/// contract (ADR-0001). Every presentation decision — the ~180px panel-tile-width threshold
/// that gates the K/M abbreviation, the "$" prefix, and the "unknown" fallback string — lives
/// here, in the Windows skin, and nowhere in O-view.Core. This class is not shared with
/// O-view.Linux; each skin owns its own wording (ADR-0003).
///
/// <para>Culture-invariant throughout, matching the source app's own reasoning: these are
/// figures, and the app pins its own presentation rather than inheriting the machine's.</para>
/// </summary>
public static class UsageFormatter
{
    /// <summary>
    /// Token counts, abbreviated at thousands and millions to one decimal — this skin's
    /// panel tiles are ~180px wide, and a raw nine-digit figure does not fit beside its
    /// label. A <see cref="TokenCount"/> whose value is unavailable renders "?" rather than
    /// a fabricated zero, the same convention <c>TooltipFormatter</c> uses for an unknown
    /// percentage.
    /// </summary>
    public static string Tokens(TokenCount tokens) => tokens.Value switch
    {
        null => "?",
        >= 1_000_000 => string.Create(CultureInfo.InvariantCulture, $"{tokens.Value / 1_000_000.0:0.0}M"),
        >= 1_000 => string.Create(CultureInfo.InvariantCulture, $"{tokens.Value / 1_000.0:0.0}K"),
        _ => tokens.Value.Value.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// An estimated USD value. An unavailable/null value renders "unknown", never "$0.00":
    /// a window nothing could be priced from has an unknown value, not a zero one, and a
    /// zero would read as "this cost nothing".
    /// </summary>
    public static string Usd(EstimatedUsd usd) => usd.Value is { } value
        ? "$" + value.ToString("0.00", CultureInfo.InvariantCulture)
        : "unknown";
}

using System.Globalization;
using OView.Core.Models;

namespace OView.Linux.Presentation;

/// <summary>
/// Builds this skin's own display strings for token counts and estimated USD values from
/// the <see cref="TokenCount"/>/<see cref="EstimatedUsd"/> slice of the Core-to-skin data
/// contract (ADR-0001). The K/M abbreviation threshold, the "$" prefix, and the
/// unavailable-value fallback text here are this skin's own choices, independently made —
/// not ported from <c>O-view.Tray</c>'s ~180px WPF tile-width reasoning, which is a Windows
/// rendering fact that must not travel to this skin (ADR-0001, ADR-0002). This class is not
/// shared with O-view.Tray; each skin owns its own wording (ADR-0003).
/// </summary>
public static class UsageFormatter
{
    /// <summary>
    /// Token counts, abbreviated at thousands and millions to one decimal — this skin's own
    /// status-area space budget is tight enough to want the same shape of abbreviation as
    /// the Windows skin, arrived at independently rather than shared. A
    /// <see cref="TokenCount"/> whose value is unavailable renders "n/a" rather than a
    /// fabricated zero.
    /// </summary>
    public static string Tokens(TokenCount tokens) => tokens.Value switch
    {
        null => "n/a",
        >= 1_000_000 => string.Create(CultureInfo.InvariantCulture, $"{tokens.Value / 1_000_000.0:0.0}M"),
        >= 1_000 => string.Create(CultureInfo.InvariantCulture, $"{tokens.Value / 1_000.0:0.0}K"),
        _ => tokens.Value.Value.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// An estimated USD value. An unavailable/null value renders "n/a" — deliberately worded
    /// differently from the Windows skin's "unknown" (ADR-0003) — never a fabricated
    /// "$0.00".
    /// </summary>
    public static string Usd(EstimatedUsd usd) => usd.Value is { } value
        ? "$" + value.ToString("0.00", CultureInfo.InvariantCulture)
        : "n/a";
}

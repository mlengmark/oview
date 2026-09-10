namespace OView.Core.Models;

/// <summary>
/// The threshold band a <see cref="UsageSnapshot"/> falls into (ADR-0001). Derivation
/// stays in Core; a skin only decides how to render the band, never how to compute it.
/// </summary>
public enum UsageLevel
{
    Green,
    Amber,
    Red,
}

namespace OView.Core.Models;

/// <summary>
/// Vendor model ids seen in the 31-day window that Core could not price (ADR-0001, OVI-100),
/// relayed verbatim — ids, not display names. An empty <see cref="Real"/> list means Core
/// priced everything; <see cref="UsageValueStatus.Unavailable"/> means Core could not establish
/// the set at all. A non-empty list makes the neighbouring 31-day estimate a subtotal, not a
/// total, and a skin must say so.
/// </summary>
public sealed record UnpricedModels
{
    public UnpricedModels(IReadOnlyList<string> modelIds)
    {
        ModelIds = modelIds;
        Status = UsageValueStatus.Real;
    }

    private UnpricedModels()
    {
        ModelIds = Array.Empty<string>();
        Status = UsageValueStatus.Unavailable;
    }

    /// <summary>Core could not establish the set of unpriced models.</summary>
    public static UnpricedModels Unavailable { get; } = new();

    /// <summary>Nothing in the window was left unpriced.</summary>
    public static UnpricedModels None { get; } = new(Array.Empty<string>());

    public IReadOnlyList<string> ModelIds { get; }

    /// <summary>Only <see cref="UsageValueStatus.Real"/> or <see cref="UsageValueStatus.Unavailable"/>.</summary>
    public UsageValueStatus Status { get; }
}

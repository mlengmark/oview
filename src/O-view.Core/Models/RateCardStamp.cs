namespace OView.Core.Models;

/// <summary>
/// A narrow projection of the rate table that priced <see cref="UsageStatistics"/>'s estimated
/// figures: where it came from, the date it was read, and whether Core judged it old
/// (ADR-0001, OVI-100). Never the pricing table itself.
///
/// <para><see cref="IsStale"/> is decided by Core against the same "today" the figures beside it
/// were built for; a skin must not re-derive it, and the staleness threshold is Core policy,
/// not a contract field. <see cref="AsOf"/> is a bare calendar date, never a formatted one.
/// An <see cref="UsageValueStatus.Unavailable"/> stamp carries no source and no date.</para>
/// </summary>
public sealed record RateCardStamp
{
    public RateCardStamp(RateCardSource source, DateOnly asOf, bool isStale)
    {
        Source = source;
        AsOf = asOf;
        IsStale = isStale;
        Status = UsageValueStatus.Real;
    }

    private RateCardStamp()
    {
        Status = UsageValueStatus.Unavailable;
    }

    /// <summary>Core could not establish which rate table priced the figures.</summary>
    public static RateCardStamp Unavailable { get; } = new();

    public RateCardSource? Source { get; }

    public DateOnly? AsOf { get; }

    public bool IsStale { get; }

    /// <summary>Only <see cref="UsageValueStatus.Real"/> or <see cref="UsageValueStatus.Unavailable"/>.</summary>
    public UsageValueStatus Status { get; }
}

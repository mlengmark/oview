namespace OView.Core.Models;

/// <summary>
/// One weekly-reset instant drawn as a gridline inside a <see cref="WeeklyResetBoundaries"/>
/// window (ADR-0008 D9e).
///
/// <para><see cref="Instant"/> carries its own offset — the one in force at that instant, in
/// the same zone the day buckets were computed in — so a skin can place it against the day
/// columns without resolving a timezone itself (ADR-0006 D2's rule that the zone is always
/// caller-supplied, never read inside, applied to skins too). Per-boundary offsets, rather
/// than one window-wide offset, are what make a DST transition inside the window
/// representable.</para>
/// </summary>
public sealed record WeeklyResetBoundary(DateTimeOffset Instant, WeeklyResetBoundaryKind Kind);

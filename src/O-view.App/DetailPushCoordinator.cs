using OView.Core.Models;
using OView.Core.Statistics;

namespace OView.App;

/// <summary>
/// ADR-0008 D9b's shell-side rule for the detail window: assembles a <see cref="UsageDetail"/>
/// and pushes it through <see cref="IShellToSkin.ShowDetail"/> — once when the widget becomes
/// visible, built from the last successfully polled <see cref="UsageSnapshot"/> already in hand,
/// and again on every subsequent successful poll while it stays visible. Never while hidden
/// (D9b rule 3) — that visibility gate is the entire reason this type exists separately from
/// <see cref="UsagePollLoop"/>, which has no notion of whether anything is looking at its
/// output and must not pay for a ledger read nobody can see.
///
/// <para>Call <see cref="OnRequestWidget"/> from <see cref="ISkinToShell.RequestWidget"/>'s
/// handler and <see cref="OnPollSucceeded"/> from the poll loop's success path (ADR-0007 D2
/// point 9 — a failed poll calls neither). Composing those two call sites together is a later
/// slice's job (no composition root exists in this repository yet); this type only owns the
/// rule itself, provable against fakes.</para>
/// </summary>
public sealed class DetailPushCoordinator
{
    /// <summary>How long after the widget auto-closes from losing focus a tray-icon click
    /// still counts as the second half of that toggle, rather than a fresh open request
    /// (ui-spec.md section 4, "Clicking the icon toggles", ADR-0008 D9b amended OVI-601). The
    /// click itself takes focus from the widget, so by the time <see cref="OnIconActivated"/>
    /// runs the widget may already have reported itself hidden — without this window every
    /// click could only ever open it. Wide enough to cover that deactivate-then-click
    /// ordering, short enough that a deliberate click a moment later still opens.</summary>
    public static readonly TimeSpan IconClickAwayGrace = TimeSpan.FromMilliseconds(400);

    private readonly IUsageStatisticsSource _statistics;
    private readonly IShellToSkin _skin;
    private readonly IClock _clock;
    private readonly TimeZoneInfo _zone;

    private bool _visible;
    private UsageSnapshot _lastSnapshot = UsageSnapshot.Unavailable;
    private DateTimeOffset _lastHiddenAt = DateTimeOffset.MinValue;

    public DetailPushCoordinator(IUsageStatisticsSource statistics, IShellToSkin skin, IClock clock, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(skin);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(zone);

        _statistics = statistics;
        _skin = skin;
        _clock = clock;
        _zone = zone;
    }

    /// <summary>
    /// The skin reported an icon activation (<paramref name="visible"/> = <c>true</c>) or a
    /// dismissal (<c>false</c>). Always forwards the visibility to the skin; only assembles and
    /// pushes a detail when becoming visible — dismissal reads nothing (D9b rule 3).
    /// </summary>
    public void OnRequestWidget(bool visible)
    {
        _visible = visible;
        _skin.SetVisible(visible);

        if (visible)
        {
            PushDetail(_lastSnapshot);
        }
        else
        {
            _lastHiddenAt = _clock.UtcNow;
        }
    }

    /// <summary>
    /// The user clicked the tray icon (ADR-0008 D9b amended OVI-601, ui-spec.md section 4):
    /// toggles the widget rather than always opening it. A click arriving within
    /// <see cref="IconClickAwayGrace"/> of the widget's own focus-loss close is absorbed as
    /// the second half of that same close, not treated as a fresh open. Distinct from
    /// <see cref="OnRequestWidget"/>, which a menu's "show usage details" item still uses to
    /// mean "show" unconditionally.
    /// </summary>
    public void OnIconActivated()
    {
        if (_visible)
        {
            OnRequestWidget(false);
            return;
        }

        if (_clock.UtcNow - _lastHiddenAt < IconClickAwayGrace)
        {
            return;
        }

        OnRequestWidget(true);
    }

    /// <summary>
    /// The poll loop completed a successful poll. Always remembers the snapshot so the next
    /// widget-show has one in hand; only reads the ledger and pushes a detail while visible
    /// (D9b rule 2/3). A failed poll must not call this — <see cref="UsagePollLoop"/> already
    /// leaves <c>CurrentSnapshot</c> untouched on failure, so the caller has nothing new to
    /// report either way.
    /// </summary>
    public void OnPollSucceeded(UsageSnapshot snapshot)
    {
        _lastSnapshot = snapshot;

        if (_visible)
        {
            PushDetail(snapshot);
        }
    }

    private void PushDetail(UsageSnapshot snapshot)
    {
        var utcNow = _clock.UtcNow;
        var statistics = _statistics.GetStatistics(utcNow, _zone);
        var models = _statistics.GetModelBreakdown(utcNow, _zone);

        if (statistics == UsageStatistics.Unavailable || models == ModelUsageBreakdown.Unavailable)
        {
            _skin.ShowDetail(UsageDetail.Unavailable);
            return;
        }

        // ADR-0008 D9e/D9g (gate G7 parity P6, OVI-635): the four members below ride the same
        // push as Statistics/Models above — no new seam member, no second schedule. Account
        // (D9e's fifth new member) is a separate slice (P7) and stays at its Unavailable
        // default here.
        var history = _statistics.GetDailySeries(utcNow, _zone);
        var resetBoundaries = _statistics.GetResetBoundaries(utcNow, _zone);
        var tokensToday = _statistics.GetTokenKindTotals(utcNow, _zone, StatisticsWindow.Today);
        var tokens31d = _statistics.GetTokenKindTotals(utcNow, _zone, StatisticsWindow.ThirtyOneDays);

        var detail = new UsageDetail(snapshot, statistics, models)
        {
            History = history,
            ResetBoundaries = resetBoundaries,
            TokensToday = tokensToday,
            Tokens31d = tokens31d,
        };

        _skin.ShowDetail(detail);
    }
}

namespace OView.Core.Models;

/// <summary>
/// How a <see cref="WeeklyResetBoundary"/> was established (ADR-0008 D9e). Three kinds, not
/// two: the next reset can be directly observed, a past boundary can be derived by stepping
/// the cadence back from an observed one, or — with no observation at all — the boundary
/// falls back to the Monday convention. Collapsing <see cref="DerivedFromObserved"/> into
/// <see cref="Observed"/> would present a computed boundary as a recorded one, which the
/// "never fabricate" discipline forbids. An enum, never a display label: a skin's hover card
/// words each kind itself.
/// </summary>
public enum WeeklyResetBoundaryKind
{
    /// <summary>Read directly from the vendor's own reset instant.</summary>
    Observed,

    /// <summary>Stepped back from an observed boundary by the known cadence, not itself
    /// observed.</summary>
    DerivedFromObserved,

    /// <summary>No observation exists at all; the boundary falls back to the Monday
    /// convention.</summary>
    MondayFallback,
}

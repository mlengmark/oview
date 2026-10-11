using OView.Core.Models;

namespace OView.Linux.Presentation;

/// <summary>
/// The per-model colour-slot rule shared by every statistics tile on the panel (ADR-0008 D10b,
/// gate G7 parity slice P11, source <c>ui-spec.md</c> §2 "Colour follows the model, never its
/// rank") — the Linux counterpart of <c>O-view.Tray</c>'s own <c>ModelColourOrder</c>,
/// independently implemented (D1) but applying the identical rule: colour is decided once, from
/// the widest window every tile draws from (<see cref="ModelUsageBreakdown.Rows"/>, already
/// Core-ranked by 31-day output tokens descending — <see cref="DetailWindowContentBuilder"/>
/// never re-sorts it), and every tile colours by that one order.
///
/// <para>Getting this wrong was the source's own first-round bug: colouring by a segment's rank
/// within its own tile put one model in two different colours on one panel, because its rank
/// changed between the token and value windows.</para>
/// </summary>
internal static class ModelColourOrder
{
    /// <summary>Most models that can keep their own chromatic colour before the remainder folds
    /// into "Other" (the validated palette's own three-slot cap — see
    /// <see cref="LinuxWindowThemePalette"/>).</summary>
    public const int MaxChromaticSlots = 3;

    /// <summary>Named models shown once a fourth model forces an "Other" bucket — dropping to
    /// two frees the one neutral slot the "Other" bucket needs to stay separable from the third
    /// chromatic colour (no grey clears the deuteranope floor beside it).</summary>
    public const int NamedSlotsBesideOther = 2;

    /// <summary>
    /// The models that get their own colour, in slot order. <paramref name="rows"/> is taken as
    /// already ranked (<see cref="ModelUsageBreakdown.Rows"/>'s own Core-side order) — this never
    /// re-sorts, so it never disagrees with the model table listed beneath the tiles.
    /// </summary>
    public static IReadOnlyList<string> ColourOrder(IReadOnlyList<ModelUsageRow> rows)
    {
        var slots = rows.Count <= MaxChromaticSlots ? MaxChromaticSlots : NamedSlotsBesideOther;
        return rows.Take(slots).Select(r => r.ModelId).ToList();
    }

    /// <summary>The slot a model's colour comes from (an index into <paramref name="colourOrder"/>),
    /// or -1 for the neutral "Other" bucket. Exact (ordinal) comparison, matching Core's own
    /// grouping of <see cref="ModelUsageRow.ModelId"/> by <see cref="StringComparer.Ordinal"/> —
    /// a vendor id is never folded case-insensitively.</summary>
    public static int SlotFor(string modelId, IReadOnlyList<string> colourOrder)
    {
        for (var i = 0; i < colourOrder.Count; i++)
        {
            if (string.Equals(colourOrder[i], modelId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}

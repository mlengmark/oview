using OView.Core.Models;

namespace OView.Core.Storage;

/// <summary>
/// The one move-aside mechanism ADR-0006 D3.3 asks every store to share: when a store file is
/// found corrupt, it is relocated beside itself rather than deleted or silently overwritten, so
/// the user keeps the evidence while the app keeps running (D3.4).
///
/// <para>The relocated copy always lands at <c>{path}.corrupt</c> — a fixed name, not a
/// timestamped one. A second corruption in a later session overwrites the previous backup rather
/// than accumulating one file per incident; ADR-0006 does not require a full corruption history,
/// only that the most recent bad copy is not silently lost. This is the "simplest convention"
/// choice ADR-0006 slice 3 (OVI-236) left to the implementer.</para>
/// </summary>
internal static class CorruptStoreRecovery
{
    private const string CorruptSuffix = ".corrupt";

    /// <summary>
    /// Moves the file at <paramref name="path"/> aside if it exists. Returns
    /// <see cref="HistoryStoreState.Rebuilt"/> only when a file actually existed and was moved —
    /// the ADR-0006 D4 contract meaning of <c>Rebuilt</c> is "found corrupt and moved aside", a
    /// reason the user should be allowed to know. Returns <see cref="HistoryStoreState.Ok"/> when
    /// there was nothing to move aside (a caller retrying after some other, unrelated failure with
    /// no corrupt file on disk did not actually recover from corruption). Returns
    /// <see cref="HistoryStoreState.Unavailable"/> when a corrupt copy could not even be moved
    /// aside, which means the store cannot be trusted this session. Never throws.
    /// </summary>
    public static HistoryStoreState MoveAsideIfPresent(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return HistoryStoreState.Ok;
            }

            File.Move(path, path + CorruptSuffix, overwrite: true);
            return HistoryStoreState.Rebuilt;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return HistoryStoreState.Unavailable;
        }
    }
}

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
    /// <see cref="HistoryStoreState.Rebuilt"/> when the move succeeded (or there was nothing to
    /// move — a first-ever write follows the same "fresh store" path), or
    /// <see cref="HistoryStoreState.Unavailable"/> when the corrupt copy could not even be moved
    /// aside, which means the store cannot be trusted this session. Never throws.
    /// </summary>
    public static HistoryStoreState MoveAsideIfPresent(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Move(path, path + CorruptSuffix, overwrite: true);
            }

            return HistoryStoreState.Rebuilt;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return HistoryStoreState.Unavailable;
        }
    }
}

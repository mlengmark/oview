namespace OView.Core.Models;

/// <summary>
/// Where the rate table that priced the estimated figures came from (ADR-0001, OVI-100).
/// An enum, never a display label: skins word it themselves.
///
/// <para><see cref="UserFile"/> is carried forward though nothing emits it yet, because the
/// moment a user-editable pricing file exists its provenance must be on screen.</para>
/// </summary>
public enum RateCardSource
{
    Bundled,

    UserFile,
}

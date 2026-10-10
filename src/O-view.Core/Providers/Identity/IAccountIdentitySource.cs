using OView.Core.Models;

namespace OView.Core.Providers;

/// <summary>
/// Core's seam for the detail-window header's account block (ADR-0008 D9e, ADR-0005 D7,
/// gate G7). Deliberately not an <see cref="IUsageProvider"/>: identity is not time-varying, so
/// this seam takes no clock; it produces an <see cref="AccountIdentity"/>, not a
/// <see cref="DataSourceKind"/>; and it is not part of <c>IUsageProvider</c> composition
/// (ADR-0005 D3) — a shell wires it separately into <c>UsageDetail.Account</c>.
/// </summary>
public interface IAccountIdentitySource
{
    /// <summary>
    /// Returns the current account identity, or <see cref="AccountIdentity.Unavailable"/>
    /// when the vendor file is missing, unreadable, or does not carry an <c>oauthAccount</c>
    /// block. Never throws — the same "a reader that fails is 'no data', not an exception"
    /// contract every provider in this repository follows.
    /// </summary>
    AccountIdentity GetIdentity();
}

using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>
/// A versioned golden-master fixture (ADR-0003) for the header's account block (ADR-0008 D9e,
/// gate G7 parity slice P8, OVI-645): display name, email and tier badge, read from
/// <see cref="AccountIdentity"/> and nothing else. Parallel to <see cref="FreshnessFixture"/> —
/// same shape, no extra clock input, since identity is not time-varying (see
/// <see cref="OView.Core.Providers.IAccountIdentitySource"/>'s remarks).
/// </summary>
public sealed record AccountIdentityFixture(
    string Name,
    AccountIdentity Account,
    IReadOnlyList<ContentFact> ContentFacts);

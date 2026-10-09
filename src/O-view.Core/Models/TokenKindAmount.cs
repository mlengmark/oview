namespace OView.Core.Models;

/// <summary>
/// One token kind's total inside a <see cref="TokenKindTotals"/> window (ADR-0008 D9e):
/// the token count Core measured, and the USD value Core's rate card modelled for it.
/// Pricing per kind is Core's; the *share* each kind represents of the window's
/// <see cref="TokenKindTotals.Total"/> is a skin's computation, not carried here.
/// </summary>
public sealed record TokenKindAmount(TokenCount Tokens, EstimatedUsd EstimatedValue);

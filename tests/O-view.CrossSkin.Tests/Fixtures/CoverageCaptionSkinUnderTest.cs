using OView.Core.Models;

namespace OView.CrossSkin.Tests.Fixtures;

/// <summary>One skin's own <c>CoverageCaption</c> entry point, wired into the harness (ADR-0008
/// D11b, gate G7 parity slice P10). Parallel to <see cref="UsageCaveatSkinUnderTest"/>.</summary>
public sealed record CoverageCaptionSkinUnderTest(string Name, Func<HistoryCoverage, string> CoverageCaption);

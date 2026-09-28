using System.Reflection;
using OView.Core.Models;
using OView.Core.Providers;

namespace OView.Core.Tests.Providers;

/// <summary>
/// Proves ADR-0005 D1's four <see cref="IUsageProvider"/> obligations are enforced by a
/// named test rather than left to prose. This slice adds the seam only — no implementation
/// exists yet (ADR-0005 slices 2-5) — so the clock-injection obligation is proven at the
/// interface's shape: <see cref="GetSnapshotTakesUtcNowAsItsOnlyParameterSoAnImplementationHasNoOtherTimeSource"/>.
/// Each future provider slice carries its own test that its implementation never calls
/// <see cref="DateTimeOffset.UtcNow"/> or <see cref="DateTime.Now"/> internally.
/// </summary>
public class IUsageProviderContractTests
{
    /// <summary>
    /// A provider whose every internal path throws, proving the never-throw obligation is
    /// something an implementation must actively uphold (by catching internally) rather
    /// than something the seam gives away for free.
    /// </summary>
    private sealed class ThrowsFromEveryInternalPathProvider : IUsageProvider
    {
        public UsageSnapshot GetSnapshot(DateTimeOffset utcNow)
        {
            try
            {
                return ReadMalformedVendorData();
            }
            catch
            {
                return UsageSnapshot.Unavailable;
            }
        }

        private static UsageSnapshot ReadMalformedVendorData() =>
            throw new InvalidOperationException("simulated malformed vendor data");
    }

    [Fact]
    public void ProviderThatThrowsFromEveryInternalPathStillYieldsUnavailableThroughTheSeam()
    {
        IUsageProvider provider = new ThrowsFromEveryInternalPathProvider();

        var exception = Record.Exception(() => provider.GetSnapshot(DateTimeOffset.UtcNow));

        Assert.Null(exception);
        Assert.Equal(UsageSnapshot.Unavailable, provider.GetSnapshot(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void GetSnapshotTakesUtcNowAsItsOnlyParameterSoAnImplementationHasNoOtherTimeSource()
    {
        var method = Assert.Single(typeof(IUsageProvider).GetMethods());

        Assert.Equal(nameof(IUsageProvider.GetSnapshot), method.Name);
        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(DateTimeOffset), parameter.ParameterType);
        Assert.Equal("utcNow", parameter.Name);
    }

    [Fact]
    public void InterfaceRejectsTryGetTaskAndStreamShapesByExposingExactlyThisSignature()
    {
        var method = Assert.Single(typeof(IUsageProvider).GetMethods());

        Assert.Equal(typeof(UsageSnapshot), method.ReturnType);
        Assert.DoesNotContain("Try", method.Name, StringComparison.Ordinal);
        Assert.False(typeof(Task).IsAssignableFrom(method.ReturnType));
    }

    [Fact]
    public void IUsageProviderDeclaresNoOtherPublicMembers()
    {
        var members = typeof(IUsageProvider).GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.Single(members);
    }
}

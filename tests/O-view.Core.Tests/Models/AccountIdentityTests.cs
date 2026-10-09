using OView.Core.Models;

namespace OView.Core.Tests.Models;

public class AccountIdentityTests
{
    [Fact]
    public void UnavailableIdentityCarriesNoValueAtAllRatherThanAGuess()
    {
        var identity = AccountIdentity.Unavailable;

        Assert.Null(identity.DisplayName);
        Assert.Null(identity.EmailAddress);
        Assert.Null(identity.OrganizationType);
        Assert.Equal(UsageValueStatus.Unavailable, identity.Status);
    }

    [Fact]
    public void AnUnrecognisedOrganizationTypeIsRelayedVerbatim()
    {
        var identity = new AccountIdentity("Ada", "ada@example.com", "some_new_vendor_token", UsageValueStatus.Real);

        Assert.Equal("some_new_vendor_token", identity.OrganizationType);
    }
}

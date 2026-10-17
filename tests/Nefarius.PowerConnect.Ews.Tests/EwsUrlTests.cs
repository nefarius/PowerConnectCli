using Nefarius.PowerConnect.Ews.Ews;

namespace Nefarius.PowerConnect.Ews.Tests;

public sealed class EwsUrlTests
{
    [Fact]
    public void BuildsTheVlanMembershipSelector()
    {
        string url = EwsUrl.Config(
            "vlan_member.htm",
            new EwsUrl.Selector
            {
                Table = "VlanMemberStaticVT",
                Query = new[] { new KeyValuePair<string, string>("dot1qVlanIndex", "20") }
            },
            new EwsUrl.Selector
            {
                Table = "nameVT",
                Filter = "dot1qVlanStaticName!=''"
            });

        Assert.Equal(
            "/config/vlan_member.htm?[VlanMemberStaticVT]Query:dot1qVlanIndex=20[nameVT]Filter:dot1qVlanStaticName!=''",
            url);
    }
}

using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Vlan;

namespace Nefarius.PowerConnect.Ews.Tests;

public sealed class VlanSubmissionTests
{
    [Fact]
    public void MembershipEditOmitsClientControlsAndKeepsTheQueryBlock()
    {
        FormPage page = FormPage.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "vlan_member.htm")));
        IReadOnlyList<KeyValuePair<string, string>> body = page.BuildSubmission(
            new Dictionary<string, string>
            {
                ["dot1qVlanStaticEgressPorts$query"] = "805000",
                ["dot1qVlanStaticRowStatus$query"] = "1"
            },
            VlanRepository.OmittedOnEdit);

        Assert.Contains(body, pair => pair.Key == "dot1qVlanStaticEgressPorts$query" && pair.Value == "805000");
        Assert.Contains(body, pair => pair.Key == "dot1qVlanIndex$queryStatic");
        int egressAt = body.ToList().FindIndex(pair => pair.Key == "dot1qVlanStaticEgressPorts$query");
        int endQueryAt = body.ToList().FindIndex(pair => pair.Key == "VlanMemberStaticVT$endQuery");
        Assert.True(egressAt >= 0 && endQueryAt >= 0 && egressAt < endQueryAt);
        Assert.DoesNotContain(body, pair => pair.Key == "indexNameRadio");
        Assert.DoesNotContain(body, pair => pair.Key == "dot1qVlanName");
        Assert.DoesNotContain(body, pair => pair.Key == "dot1qVlanIndex$select");
        Assert.DoesNotContain(body, pair => pair.Key == "Unauthorized");
        Assert.DoesNotContain(body, pair => pair.Key == "dot1qVlanStaticEgressPorts$add");
        Assert.DoesNotContain(body, pair => pair.Key == "dot1qVlanIndex$query");
    }
}

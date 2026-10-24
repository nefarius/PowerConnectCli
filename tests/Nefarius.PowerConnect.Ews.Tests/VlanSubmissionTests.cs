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

    [Fact]
    public void CreateSubmissionInitializesPortBitmapsAndOmitsAuthNotReq()
    {
        FormPage page = FormPage.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "create_new_vlan.htm")));
        IReadOnlyList<KeyValuePair<string, string>> body = page.BuildSubmission(
            VlanRepository.CreateMutations(20, "Lab"),
            VlanRepository.OmittedOnCreate);

        Assert.Contains(body, pair => pair.Key == "dot1qVlanIndex$add" && pair.Value == "20");
        Assert.Contains(body, pair => pair.Key == "dot1qVlanStaticName$add" && pair.Value == "Lab");
        Assert.Contains(body, pair => pair.Key == "dot1qVlanStaticRowStatus$add" && pair.Value == "4");
        Assert.Contains(body, pair => pair.Key == "dot1qVlanStaticEgressPorts$add" && pair.Value == "00");
        Assert.Contains(body, pair => pair.Key == "dot1qVlanForbiddenEgressPorts$add" && pair.Value == "00");
        Assert.Contains(body, pair => pair.Key == "dot1qVlanStaticUntaggedPorts$add" && pair.Value == "00");
        Assert.Contains(body, pair => pair.Key == "VlanMemberStaticVT$endAdd" && pair.Value == "OK");
        Assert.DoesNotContain(body, pair => pair.Key == "AuthNotReq");
    }
}

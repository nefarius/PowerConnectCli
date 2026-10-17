using Nefarius.PowerConnect.Ews.Ews;

namespace Nefarius.PowerConnect.Ews.Tests;

public sealed class FormPageTests
{
    [Fact]
    public void VlanMembershipPageExposesIdsAndPortLists()
    {
        FormPage page = FormPage.Parse(Fixture("vlan_member.htm"));

        Assert.Equal(new[] { "1", "20", "30" }, page.Options("dot1qVlanIndex$select").Select(option => option.Text));
        Assert.Equal("Lab", page.Query("dot1qVlanStaticName"));
        Assert.Equal(new[] { "g1", "g5" }, PortList.Decode(page.Query("dot1qVlanStaticEgressPorts")).Select(port => port.Name));
        Assert.Equal(new[] { "g5" }, PortList.Decode(page.Query("dot1qVlanStaticUntaggedPorts")).Select(port => port.Name));
        Assert.Contains(page.Rows(), row => row.Value("dot1qVlanIndex") == "30" && row.Value("dot1qVlanStaticName") == "Quarantine");
    }

    [Fact]
    public void DisabledFieldsAreOmittedUntilMutated()
    {
        FormPage page = FormPage.Parse(Fixture("snmp_global.htm"));
        IReadOnlyList<KeyValuePair<string, string>> unchanged = page.BuildSubmission(null);
        IReadOnlyList<KeyValuePair<string, string>> mutated = page.BuildSubmission(
            new Dictionary<string, string> { ["rndAlarmEnabling$scalar"] = "2" });

        Assert.DoesNotContain(unchanged, pair => pair.Key == "rndAlarmEnabling$scalar");
        Assert.Contains(mutated, pair => pair.Key == "rndAlarmEnabling$scalar" && pair.Value == "2");
    }

    [Fact]
    public void ValueTypeMetadataParsesPortListType()
    {
        FormPage page = FormPage.Parse(Fixture("vlan_member.htm"));
        ValueTypeMetadata? metadata = page.Metadata("dot1qVlanStaticEgressPorts");

        Assert.NotNull(metadata);
        Assert.Equal(101, metadata.Type);
        Assert.Equal(2, metadata.Access);
    }

    [Fact]
    public void PortConfigurationKeepsAdminStatus()
    {
        FormPage page = FormPage.Parse(Fixture("port_config.htm"));

        Assert.Equal("1", page.Query("ifAdminStatus"));
        Assert.Equal("1", page.Query("swIfSpeedDuplexAutoNegotiation"));
        Assert.Equal("/config/port_config.htm", page.Action);
    }

    private static string Fixture(string name)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
    }
}

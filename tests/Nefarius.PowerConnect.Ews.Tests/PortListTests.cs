using Nefarius.PowerConnect.Ews.Ews;

namespace Nefarius.PowerConnect.Ews.Tests;

public sealed class PortListTests
{
    [Fact]
    public void G1AndG5AreTheHighNibbleOfTheFirstByte()
    {
        string hex = PortList.Encode(new[] { PortName.Parse("g1"), PortName.Parse("g5") }, 4);

        Assert.Equal("8800", hex);
        Assert.Equal(new[] { "g1", "g5" }, PortList.Decode(hex).Select(port => port.Name));
    }

    [Fact]
    public void AllLagsEndIn01fe()
    {
        IEnumerable<PortName> lags = Enumerable.Range(1, 8).Select(index => PortName.Parse("ch" + index));
        string hex = PortList.Encode(lags, PortList.DefaultHexLength);

        Assert.EndsWith("01fe", hex);
        Assert.Equal(8, PortList.Decode(hex).Count);
    }

    [Fact]
    public void Vlan1ShapeExcludesG5AndG12AndIncludesEveryLag()
    {
        string zeros = new string('0', PortList.DefaultHexLength - 8);
        string hex = "f7ef" + zeros + "01fe";
        List<string> names = PortList.Decode(hex).Select(port => port.Name).ToList();

        Assert.DoesNotContain("g5", names);
        Assert.DoesNotContain("g12", names);
        Assert.Contains("g1", names);
        Assert.Contains("g16", names);
        Assert.Contains("ch1", names);
        Assert.Contains("ch8", names);
    }

    [Fact]
    public void MembershipUpdatesKeepUnknownIfIndexes()
    {
        string unknown = "000080" + new string('0', PortList.DefaultHexLength - 6);
        (string egress, string untagged, string forbidden) = PortList.UpdateMembership(
            unknown,
            unknown,
            "0",
            new[] { PortName.Parse("g1") },
            Array.Empty<PortName>(),
            Array.Empty<PortName>(),
            Array.Empty<PortName>());

        Assert.StartsWith("800080", egress);
        Assert.StartsWith("000080", untagged);
        Assert.Equal(PortList.DefaultHexLength, forbidden.Length);
    }

    [Theory]
    [InlineData("g1", 1)]
    [InlineData("g16", 16)]
    [InlineData("ch1", 1000)]
    [InlineData("ch8", 1007)]
    public void NamesMapToIfIndex(string name, int ifIndex)
    {
        PortName port = PortName.Parse(name);

        Assert.Equal(ifIndex, port.IfIndex);
        Assert.Equal(name, PortName.Parse(ifIndex.ToString()).Name);
    }
}

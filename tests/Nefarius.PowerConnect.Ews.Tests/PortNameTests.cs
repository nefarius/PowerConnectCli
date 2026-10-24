using Nefarius.PowerConnect.Ews.Ews;

namespace Nefarius.PowerConnect.Ews.Tests;

public sealed class PortNameTests
{
    [Fact]
    public void RangeExpandsGigabitPorts()
    {
        Assert.Equal(new[] { "g1", "g2", "g3", "g4", "g5", "g6", "g7", "g8" }, Names("g1-g8"));
    }

    [Fact]
    public void MixedListKeepsOrder()
    {
        Assert.Equal(new[] { "g1", "g2", "g3", "g4", "g6", "g8", "g9", "g10" }, Names("g1-g4,g6,g8-g10"));
    }

    [Fact]
    public void ShortFormUsesTheSameKind()
    {
        Assert.Equal(new[] { "g1", "g2", "g3", "g4", "g5", "g6", "g7", "g8" }, Names("g1-8"));
        Assert.Equal(new[] { "ch1", "ch2", "ch3" }, Names("ch1-3"));
    }

    [Fact]
    public void LagRangeExpands()
    {
        Assert.Equal(new[] { "ch1", "ch2", "ch3" }, Names("ch1-ch3"));
    }

    [Fact]
    public void DuplicatesCollapseAndKeepTheFirstPlace()
    {
        Assert.Equal(new[] { "g1", "g2", "g3", "g4" }, Names("g1-g4,g3"));
    }

    [Theory]
    [InlineData("g8-g1")]
    [InlineData("g1-g20")]
    [InlineData("g1-ch2")]
    [InlineData("1-8")]
    [InlineData("g1-")]
    [InlineData("g1-g4-g6")]
    [InlineData("-g1")]
    [InlineData("nope")]
    public void BadRangesThrow(string text)
    {
        Assert.Throws<FormatException>(() => PortName.ParseList(text));
    }

    [Fact]
    public void SinglePortAndBareIfIndexStaySingle()
    {
        Assert.Equal("g5", PortName.Parse("g5").Name);
        Assert.Equal(new[] { "g5" }, Names("g5"));
        Assert.Equal(new[] { "g5" }, Names("5"));
        Assert.Equal(new[] { "ch2" }, Names("1001"));
    }

    [Fact]
    public void LagSelectionKeepsLagsAndRejectsPhysicalPorts()
    {
        Assert.Equal(new[] { "ch1", "ch2", "ch3" }, PortName.ParseLagSelection("ch1-ch3").Select(port => port.Name));
        Assert.Equal(new[] { "ch2" }, PortName.ParseLagSelection("1001").Select(port => port.Name));
        Assert.Throws<FormatException>(() => PortName.ParseLagSelection("g1"));
        Assert.Throws<FormatException>(() => PortName.ParseLagSelection("ch1,g2"));
    }

    [Fact]
    public void EmptySelectionThrowsAndAnEmptyListStaysEmpty()
    {
        Assert.Throws<FormatException>(() => PortName.ParseSelection("  "));
        Assert.Empty(PortName.ParseList(null));
    }

    private static string[] Names(string text)
    {
        return PortName.ParseList(text).Select(port => port.Name).ToArray();
    }
}

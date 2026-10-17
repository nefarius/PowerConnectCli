using Nefarius.PowerConnect.Ews.Common;

namespace Nefarius.PowerConnect.Ews.Tests;

public sealed class MibValuesTests
{
    [Theory]
    [InlineData("1", "up")]
    [InlineData("2", "down")]
    [InlineData("3", "testing")]
    [InlineData("4", "unknown")]
    [InlineData("5", "dormant")]
    [InlineData("6", "not present")]
    [InlineData("7", "lower layer down")]
    [InlineData("9", "9")]
    [InlineData(null, "")]
    public void OperStatusUsesFriendlyNames(string? value, string expected)
    {
        Assert.Equal(expected, MibValues.OperStatusText(value));
    }
}

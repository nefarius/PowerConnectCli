using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Ports;

namespace Nefarius.PowerConnect.Ews.Tests;

public sealed class PortSubmissionTests
{
    [Fact]
    public void DescriptionChangeOmitsUnchangedPortFields()
    {
        FormPage page = FormPage.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "port_config.htm")));
        IReadOnlyList<KeyValuePair<string, string>> body = page.BuildSubmission(
            new Dictionary<string, string> { ["ifAlias$query"] = "B5 NFLATSW02 Uplink" },
            PortRepository.OmittedUnlessMutated);

        Assert.Contains(body, pair => pair.Key == "ifAlias$query" && pair.Value == "B5 NFLATSW02 Uplink");
        Assert.Contains(body, pair => pair.Key == "ifIndex$query" && pair.Value == "1");
        int aliasAt = body.ToList().FindIndex(pair => pair.Key == "ifAlias$query");
        int endQueryAt = body.ToList().FindIndex(pair => pair.Key == "PortConfigTable$endQuery");
        Assert.True(aliasAt >= 0 && endQueryAt >= 0 && aliasAt < endQueryAt);
        Assert.DoesNotContain(body, pair => pair.Key == "swIfSpeedAdminMode$query");
        Assert.DoesNotContain(body, pair => pair.Key == "swIfDuplexAdminMode$query");
        Assert.DoesNotContain(body, pair => pair.Key == "ifAdminStatus$query");
        Assert.DoesNotContain(body, pair => pair.Key == "swIfType$query");
        Assert.DoesNotContain(body, pair => pair.Key == "adminAdvertisementFlags");
    }

    [Fact]
    public void NonBreakingSpacesInAttributesAreOrdinarySpaces()
    {
        const string html = "<form><input name=\"ifAlias$query\" value=\"B5&nbsp;NFLATSW02&nbsp;Uplink\"></form>";
        FormPage page = FormPage.Parse(html);

        Assert.Equal("B5 NFLATSW02 Uplink", page.Query("ifAlias"));
    }
}

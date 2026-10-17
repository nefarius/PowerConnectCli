using Nefarius.PowerConnect.Ews.Common;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Http;

namespace Nefarius.PowerConnect.Ews.Diagnostics;

/// <summary>Cable test and optical transceiver diagnostics.</summary>
public sealed class DiagnosticsRepository
{
    private readonly SwitchClient _client;

    /// <summary>Creates a repository bound to a session.</summary>
    public DiagnosticsRepository(SwitchClient client)
    {
        _client = client;
    }

    /// <summary>
    ///     Runs the copper cable test by opening the diagnostics page for the port.
    ///     The device executes the test as part of rendering that selector.
    /// </summary>
    public async Task<FormPage> CableTestAsync(PortName port, CancellationToken cancellationToken = default)
    {
        string index = MibValues.Number(port.IfIndex);
        string url = "/config/general_health_copper.htm?[PhyTestSet]Query:ifIndex=" + index +
                     "[PhyTestGet]Filter:ifIndex=" + index +
                     "[ifTableVT]Query:ifIndex=" + index;
        return await _client.GetFormAsync(url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads the transceiver diagnostics page when the port has an SFP.</summary>
    public Task<FormPage> TransceiverAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetFormAsync("/config/general_health_copper.htm", cancellationToken);
    }
}

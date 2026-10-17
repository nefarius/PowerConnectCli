using Nefarius.PowerConnect.Ews.Dhcp;
using Nefarius.PowerConnect.Ews.Diagnostics;
using Nefarius.PowerConnect.Ews.Dot1x;
using Nefarius.PowerConnect.Ews.Files;
using Nefarius.PowerConnect.Ews.Http;
using Nefarius.PowerConnect.Ews.Ip;
using Nefarius.PowerConnect.Ews.Lag;
using Nefarius.PowerConnect.Ews.Multicast;
using Nefarius.PowerConnect.Ews.Ports;
using Nefarius.PowerConnect.Ews.Qos;
using Nefarius.PowerConnect.Ews.Security;
using Nefarius.PowerConnect.Ews.Snapshot;
using Nefarius.PowerConnect.Ews.Snmp;
using Nefarius.PowerConnect.Ews.Stats;
using Nefarius.PowerConnect.Ews.Stp;
using Nefarius.PowerConnect.Ews.SystemInfo;
using Nefarius.PowerConnect.Ews.Vlan;

namespace Nefarius.PowerConnect.Ews;

/// <summary>
///     A logged-in PowerConnect session and the feature repositories that use it.
/// </summary>
public sealed class SwitchDevice : IAsyncDisposable
{
    private SwitchDevice(SwitchClient client, string user)
    {
        Client = client;
        SessionUser = user;
        Vlans = new VlanRepository(client);
        Ports = new PortRepository(client);
        Lags = new LagRepository(client);
        GreenEthernet = new GreenEthernetRepository(client);
        StormControl = new StormControlRepository(client);
        Mirror = new MirrorRepository(client);
        MacTable = new MacTableRepository(client);
        Stp = new StpRepository(client);
        Multicast = new MulticastRepository(client);
        Dot1x = new Dot1xRepository(client);
        Users = new UserRepository(client);
        Radius = new RadiusRepository(client);
        Snmp = new SnmpRepository(client);
        Ip = new IpRepository(client);
        Dhcp = new DhcpRepository(client);
        System = new SystemRepository(client);
        Files = new FileRepository(client);
        Diagnostics = new DiagnosticsRepository(client);
        Stats = new StatsRepository(client);
        Qos = new QosRepository(client);
        Raw = new RawRepository(client);
    }

    /// <summary>Underlying HTTP session.</summary>
    public SwitchClient Client { get; }

    /// <summary>User name used to log in.</summary>
    public string SessionUser { get; }

    /// <summary>VLANs.</summary>
    public VlanRepository Vlans { get; }

    /// <summary>Copper port configuration.</summary>
    public PortRepository Ports { get; }

    /// <summary>Link aggregation groups.</summary>
    public LagRepository Lags { get; }

    /// <summary>Green Ethernet.</summary>
    public GreenEthernetRepository GreenEthernet { get; }

    /// <summary>Storm control.</summary>
    public StormControlRepository StormControl { get; }

    /// <summary>Port mirroring.</summary>
    public MirrorRepository Mirror { get; }

    /// <summary>Dynamic MAC table.</summary>
    public MacTableRepository MacTable { get; }

    /// <summary>Spanning tree.</summary>
    public StpRepository Stp { get; }

    /// <summary>Multicast and IGMP snooping.</summary>
    public MulticastRepository Multicast { get; }

    /// <summary>802.1X.</summary>
    public Dot1xRepository Dot1x { get; }

    /// <summary>Local users.</summary>
    public UserRepository Users { get; }

    /// <summary>RADIUS.</summary>
    public RadiusRepository Radius { get; }

    /// <summary>SNMP.</summary>
    public SnmpRepository Snmp { get; }

    /// <summary>Management IP.</summary>
    public IpRepository Ip { get; }

    /// <summary>DHCP server.</summary>
    public DhcpRepository Dhcp { get; }

    /// <summary>System asset, reset, secure mode, jumbo frames.</summary>
    public SystemRepository System { get; }

    /// <summary>File copy and factory defaults.</summary>
    public FileRepository Files { get; }

    /// <summary>Cable test.</summary>
    public DiagnosticsRepository Diagnostics { get; }

    /// <summary>RMON and CPU.</summary>
    public StatsRepository Stats { get; }

    /// <summary>QoS.</summary>
    public QosRepository Qos { get; }

    /// <summary>Raw page access.</summary>
    public RawRepository Raw { get; }

    /// <summary>Logs in and opens every feature repository.</summary>
    public static async Task<SwitchDevice> ConnectAsync(
        SwitchConnection connection,
        Serilog.ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        SwitchClient client = await SwitchClient.ConnectAsync(connection, logger, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new SwitchDevice(client, connection.User);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return Client.DisposeAsync();
    }
}

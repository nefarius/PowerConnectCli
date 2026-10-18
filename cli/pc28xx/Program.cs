using System.CommandLine;
using System.Globalization;
using System.Text;

using Nefarius.PowerConnect.Cli;
using Nefarius.PowerConnect.Ews;
using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Dhcp;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Ports;
using Nefarius.PowerConnect.Ews.Qos;
using Nefarius.PowerConnect.Ews.Security;
using Nefarius.PowerConnect.Ews.Snapshot;

using Spectre.Console;

RootCommand root = new("pc28xx drives a Dell PowerConnect 28xx web UI. Writes are shown and applied only after confirmation.");
CliHost.AddGlobals(root);

Command vlan = Add(root, "vlan", "VLAN membership");
Bind(Add(vlan, "list", "List VLANs"), (device, session, token) =>
    Show(session, "Reading VLANs…", () => device.Vlans.ListAsync(token)));
Argument<int> vlanId = new("id") { Description = "VLAN id" };
BindParse(Add(vlan, "show", "Show one VLAN", vlanId), (device, session, token, parse) =>
    Show(session, "Reading VLAN…", () => device.Vlans.GetAsync(parse.GetValue(vlanId), token)));
Option<string> vlanName = new("--name") { Description = "VLAN name", Required = true };
BindParse(Add(vlan, "create", "Create a VLAN", vlanId, vlanName), (device, session, token, parse) =>
    Plan(device, session, "Preparing VLAN change…", () => device.Vlans.PlanCreateAsync(parse.GetValue(vlanId), parse.GetValue(vlanName)!, token)));
BindParse(Add(vlan, "delete", "Delete a VLAN", vlanId), (device, session, token, parse) =>
    Plan(device, session, "Preparing VLAN change…", () => device.Vlans.PlanDeleteAsync(parse.GetValue(vlanId), token)));
Option<string?> tagged = new("--tagged") { Description = "Ports to tag, comma separated" };
Option<string?> untagged = new("--untagged") { Description = "Ports to leave untagged" };
Option<string?> forbidden = new("--forbidden") { Description = "Ports to forbid" };
Option<string?> none = new("--none") { Description = "Ports to remove from the VLAN" };
BindParse(Add(vlan, "set", "Edit VLAN membership", vlanId, tagged, untagged, forbidden, none), (device, session, token, parse) =>
    Plan(device, session, "Preparing VLAN change…", () => device.Vlans.PlanSetAsync(
        parse.GetValue(vlanId),
        PortName.ParseList(parse.GetValue(tagged)),
        PortName.ParseList(parse.GetValue(untagged)),
        PortName.ParseList(parse.GetValue(forbidden)),
        PortName.ParseList(parse.GetValue(none)),
        token)));

Command vlanPort = Add(vlan, "port", "Per-port VLAN settings");
Argument<string> portName = new("port") { Description = "Port or LAG (g1-g16, ch1-ch8)" };
Bind(Add(vlanPort, "list", "List PVID and ingress settings"), (device, session, token) =>
    Show(session, "Reading VLAN ports…", () => device.Vlans.ListPortsAsync(token)));
BindParse(Add(vlanPort, "show", "Show one port", portName), (device, session, token, parse) =>
    Show(session, "Reading VLAN port…", () => device.Vlans.GetPortAsync(PortName.Parse(parse.GetValue(portName)!), token)));
Option<int?> pvid = new("--pvid") { Description = "Port VLAN id" };
Option<string?> frameType = new("--frame-type") { Description = "admit-all or tagged-only" };
Option<string?> ingress = new("--ingress-filtering") { Description = "on or off" };
BindParse(Add(vlanPort, "set", "Set PVID, frame type, or ingress filtering", portName, pvid, frameType, ingress), async (device, session, token, parse) =>
{
    bool? filtering = parse.GetValue(ingress) is string text ? On(text) : null;
    return await Plan(device, session, "Preparing VLAN change…", () => device.Vlans.PlanPortSetAsync(
        PortName.Parse(parse.GetValue(portName)!),
        parse.GetValue(pvid),
        parse.GetValue(frameType),
        filtering,
        token)).ConfigureAwait(false);
});

Command port = Add(root, "port", "Port configuration");
Bind(Add(port, "list", "List copper ports"), (device, session, token) =>
    Show(session, "Reading ports…", () => device.Ports.ListAsync(token)));
BindParse(Add(port, "show", "Show one port", portName), (device, session, token, parse) =>
    Show(session, "Reading port…", () => device.Ports.GetAsync(PortName.Parse(parse.GetValue(portName)!), token)));
Option<string?> admin = new("--admin") { Description = "up or down" };
Option<int?> speed = new("--speed") { Description = "10, 100, or 1000" };
Option<string?> duplex = new("--duplex") { Description = "full or half" };
Option<string?> autoneg = new("--autoneg") { Description = "on or off" };
Option<string?> flow = new("--flow-control") { Description = "on, off, or autoneg" };
Option<string?> mdix = new("--mdix") { Description = "auto, on, or off" };
Option<string?> backPressure = new("--back-pressure") { Description = "on or off" };
Option<string?> description = new("--description") { Description = "Port description" };
BindParse(Add(port, "set", "Change port settings. With no options this resubmits the current form.", portName, admin, speed, duplex, autoneg, flow, mdix, backPressure, description), async (device, session, token, parse) =>
{
    PortUpdate update = new PortUpdate
    {
        AdminUp = parse.GetValue(admin) is string adminText ? ParseAdmin(adminText) : null,
        SpeedMegabits = parse.GetValue(speed),
        Duplex = parse.GetValue(duplex),
        AutoNegotiation = parse.GetValue(autoneg) is string autoText ? On(autoText) : null,
        FlowControl = parse.GetValue(flow),
        Mdix = parse.GetValue(mdix),
        BackPressure = parse.GetValue(backPressure) is string backText ? On(backText) : null,
        Description = parse.GetValue(description)
    };
    return await Plan(device, session, "Preparing port change…", () => device.Ports.PlanSetAsync(PortName.Parse(parse.GetValue(portName)!), update, token)).ConfigureAwait(false);
});
Option<int> accessVlan = new("--vlan") { Description = "Access VLAN", Required = true };
BindParse(Add(port, "access", "Make a port an untagged member of one VLAN and set its PVID", portName, accessVlan), (device, session, token, parse) =>
    Plan(device, session, "Preparing port change…", () => device.Vlans.PlanAccessAsync(PortName.Parse(parse.GetValue(portName)!), parse.GetValue(accessVlan), token)));

Command lag = Add(root, "lag", "Link aggregation");
Argument<string> channel = new("channel") { Description = "LAG ch1-ch8" };
Bind(Add(lag, "list", "List LAGs"), (device, session, token) =>
    Show(session, "Reading LAGs…", () => device.Lags.ListAsync(token)));
BindParse(Add(lag, "show", "Show one LAG", channel), (device, session, token, parse) =>
    Show(session, "Reading LAG…", () => device.Lags.GetAsync(PortName.Parse(parse.GetValue(channel)!), token)));
Option<string?> balance = new("--balance") { Description = "l2, l3, or l2l3" };
BindParse(Add(lag, "set", "Set LAG description or load balance", channel, description, balance), (device, session, token, parse) =>
    Plan(device, session, "Preparing LAG change…", () => device.Lags.PlanSetAsync(PortName.Parse(parse.GetValue(channel)!), parse.GetValue(description), parse.GetValue(balance), token)));
Command lagMembers = Add(lag, "members", "LAG membership");
Option<string> memberPorts = new("--ports") { Description = "Member ports", Required = true };
Option<bool> lacp = new("--lacp") { Description = "Enable LACP actor state" };
BindParse(Add(lagMembers, "set", "Replace LAG members", channel, memberPorts, lacp), (device, session, token, parse) =>
    Plan(device, session, "Preparing LAG change…", () => device.Lags.PlanMembersAsync(
        PortName.Parse(parse.GetValue(channel)!),
        PortName.ParseList(parse.GetValue(memberPorts)),
        parse.GetValue(lacp),
        token)));

Command green = Add(root, "green-ethernet", "Green Ethernet");
Option<string?> energy = new("--energy-detect") { Description = "on or off" };
Option<string?> reach = new("--short-reach") { Description = "on or off" };
Option<string?> greenPort = new("--port") { Description = "Limit the change to one port" };
Bind(Add(green, "show", "Show Green Ethernet"), (device, session, token) =>
    Show(session, "Reading Green Ethernet…", () => device.GreenEthernet.GetAsync(token)));
BindParse(Add(green, "set", "Set Green Ethernet", energy, reach, greenPort), (device, session, token, parse) =>
    Plan(device, session, "Preparing Green Ethernet change…", () => device.GreenEthernet.PlanSetAsync(
        parse.GetValue(energy) is string energyText ? On(energyText) : null,
        parse.GetValue(reach) is string reachText ? On(reachText) : null,
        parse.GetValue(greenPort) is string portText ? PortName.Parse(portText) : null,
        token)));

Command storm = Add(root, "storm-control", "Storm control");
Option<string?> broadcast = new("--broadcast") { Description = "on or off" };
Option<int?> rate = new("--rate") { Description = "Raw broadcast rate counter (UI shows this divided by 1000)" };
Option<string?> multicastFlag = new("--multicast") { Description = "on or off" };
Option<string?> unknown = new("--unknown-unicast") { Description = "on or off" };
BindParse(Add(storm, "show", "Show storm control", portName), (device, session, token, parse) =>
    Show(session, "Reading storm control…", () => device.StormControl.GetAsync(PortName.Parse(parse.GetValue(portName)!), token)));
BindParse(Add(storm, "set", "Set storm control", portName, broadcast, rate, multicastFlag, unknown), (device, session, token, parse) =>
    Plan(device, session, "Preparing storm control change…", () => device.StormControl.PlanSetAsync(
        PortName.Parse(parse.GetValue(portName)!),
        parse.GetValue(broadcast) is string broadcastText ? On(broadcastText) : null,
        parse.GetValue(rate),
        parse.GetValue(multicastFlag) is string multiText ? On(multiText) : null,
        parse.GetValue(unknown) is string unknownText ? On(unknownText) : null,
        token)));

Command mirror = Add(root, "mirror", "Port mirroring");
Option<string?> source = new("--source") { Description = "Source port" };
Option<string?> dest = new("--dest") { Description = "Destination port" };
Bind(Add(mirror, "show", "Show the mirror session"), (device, session, token) =>
    Show(session, "Reading mirror session…", async () =>
    {
        (string mirrorSource, string mirrorDest) = await device.Mirror.GetAsync(token).ConfigureAwait(false);
        return new Dictionary<string, string> { ["source"] = mirrorSource, ["dest"] = mirrorDest };
    }));
BindParse(Add(mirror, "set", "Set the mirror session", source, dest), (device, session, token, parse) =>
    Plan(device, session, "Preparing mirror change…", () => device.Mirror.PlanSetAsync(
        parse.GetValue(source) is string sourceText ? PortName.Parse(sourceText) : null,
        parse.GetValue(dest) is string destText ? PortName.Parse(destText) : null,
        false,
        token)));
Bind(Add(mirror, "clear", "Clear the mirror session"), (device, session, token) =>
    Plan(device, session, "Preparing mirror change…", () => device.Mirror.PlanSetAsync(null, null, true, token)));

Command mac = Add(root, "mac-table", "Dynamic MAC address table");
Option<int?> macVlan = new("--vlan") { Description = "Filter by VLAN" };
Option<string?> macPort = new("--port") { Description = "Filter by port" };
BindParse(Add(mac, "list", "List learned addresses", macVlan, macPort), (device, session, token, parse) =>
    Show(session, "Reading MAC table…", () => device.MacTable.ListAsync(
        parse.GetValue(macVlan),
        parse.GetValue(macPort) is string macPortText ? PortName.Parse(macPortText) : null,
        token)));
Bind(Add(mac, "clear", "Clear the dynamic table"), (device, session, token) =>
    Plan(device, session, "Preparing MAC table change…", () => device.MacTable.PlanClearAsync(token)));

Command stp = Add(root, "stp", "Spanning tree");
Option<string?> enable = new("--enable") { Description = "on or off" };
Option<string?> mode = new("--mode") { Description = "stp or rstp" };
Option<int?> priority = new("--priority") { Description = "Bridge or port priority" };
Option<int?> hello = new("--hello") { Description = "Hello time in seconds" };
Option<int?> maxAge = new("--max-age") { Description = "Max age in seconds" };
Option<int?> forward = new("--forward-delay") { Description = "Forward delay in seconds" };
Option<string?> pathCostMethod = new("--path-cost-method") { Description = "short or long" };
Bind(Add(stp, "show", "Show bridge spanning tree"), (device, session, token) =>
    Show(session, "Reading spanning tree…", () => device.Stp.GetBridgeAsync(token)));
BindParse(Add(stp, "set", "Set bridge spanning tree", enable, mode, priority, hello, maxAge, forward, pathCostMethod), (device, session, token, parse) =>
    Plan(device, session, "Preparing spanning tree change…", () => device.Stp.PlanBridgeAsync(
        parse.GetValue(enable) is string enableText ? On(enableText) : null,
        parse.GetValue(mode),
        parse.GetValue(priority),
        parse.GetValue(hello),
        parse.GetValue(maxAge),
        parse.GetValue(forward),
        parse.GetValue(pathCostMethod),
        token)));
Command stpPort = Add(stp, "port", "Per-port spanning tree");
Option<int?> pathCost = new("--path-cost") { Description = "Admin path cost" };
Option<string?> edge = new("--edge") { Description = "on or off" };
Option<string?> restricted = new("--restricted-role") { Description = "on or off" };
Option<bool> migrate = new("--migrate") { Description = "Send a protocol migration" };
Bind(Add(stpPort, "list", "List STP port settings"), (device, session, token) =>
    Show(session, "Reading spanning tree ports…", () => device.Stp.ListPortsAsync(token)));
BindParse(Add(stpPort, "set", "Set STP on a port or LAG", portName, enable, priority, pathCost, edge, restricted, migrate), (device, session, token, parse) =>
    Plan(device, session, "Preparing spanning tree change…", () => device.Stp.PlanPortAsync(
        PortName.Parse(parse.GetValue(portName)!),
        parse.GetValue(enable) is string stpEnable ? On(stpEnable) : null,
        parse.GetValue(priority),
        parse.GetValue(pathCost),
        parse.GetValue(edge) is string edgeText ? On(edgeText) : null,
        parse.GetValue(restricted) is string restrictedText ? On(restrictedText) : null,
        parse.GetValue(migrate),
        token)));

Command mcast = Add(root, "multicast", "Bridge multicast");
Option<string?> filtering = new("--bridge-filtering") { Description = "on or off" };
Option<string?> igmp = new("--igmp-snooping") { Description = "on or off" };
Bind(Add(mcast, "show", "Show global multicast"), (device, session, token) =>
    Show(session, "Reading multicast…", () => device.Multicast.GetGlobalAsync(token)));
BindParse(Add(mcast, "set", "Set global multicast", filtering, igmp), (device, session, token, parse) =>
    Plan(device, session, "Preparing multicast change…", () => device.Multicast.PlanGlobalAsync(
        parse.GetValue(filtering) is string filterText ? On(filterText) : null,
        parse.GetValue(igmp) is string igmpText ? On(igmpText) : null,
        token)));
Command group = Add(mcast, "group", "Multicast groups");
Argument<int> groupVlan = new("vlan") { Description = "VLAN id" };
Argument<string> groupMac = new("mac") { Description = "Group MAC address" };
Option<string?> groupPorts = new("--ports") { Description = "Egress ports" };
Bind(Add(group, "list", "List groups"), (device, session, token) =>
    Show(session, "Reading multicast groups…", async () => Fields(await device.Multicast.GroupsPageAsync(token).ConfigureAwait(false))));
BindParse(Add(group, "add", "Add a group", groupVlan, groupMac, groupPorts), (device, session, token, parse) =>
    Plan(device, session, "Preparing multicast change…", () => device.Multicast.PlanGroupAsync(parse.GetValue(groupVlan), parse.GetValue(groupMac)!, PortName.ParseList(parse.GetValue(groupPorts)), false, token)));
BindParse(Add(group, "delete", "Delete a group", groupVlan, groupMac), (device, session, token, parse) =>
    Plan(device, session, "Preparing multicast change…", () => device.Multicast.PlanGroupAsync(parse.GetValue(groupVlan), parse.GetValue(groupMac)!, Array.Empty<PortName>(), true, token)));
Command forwardAll = Add(mcast, "forward-all", "Forward-all router ports");
Bind(Add(forwardAll, "show", "Show forward-all"), (device, session, token) =>
    Show(session, "Reading forward-all ports…", async () => Fields(await device.Multicast.ForwardAllAsync(token).ConfigureAwait(false))));
BindParse(Add(forwardAll, "set", "Set forward-all ports", groupVlan, groupPorts), (device, session, token, parse) =>
{
    string? portText = parse.GetValue(groupPorts);
    IReadOnlyList<PortName>? ports = portText == null ? null : PortName.ParseList(portText);
    return Plan(device, session, "Preparing multicast change…", () => device.Multicast.PlanForwardAllAsync(parse.GetValue(groupVlan), ports, token));
});
Command igmpSnoop = Add(root, "igmp-snooping", "IGMP snooping per VLAN");
Option<string?> autoLearn = new("--auto-learn") { Description = "on or off" };
Option<int?> hostTimeout = new("--host-timeout");
Option<int?> routerTimeout = new("--router-timeout");
Option<int?> leaveTimeout = new("--leave-timeout");
Option<string?> querier = new("--querier") { Description = "on or off" };
Option<string?> querierAddress = new("--querier-address");
BindParse(Add(igmpSnoop, "show", "Show snooping for a VLAN", groupVlan), (device, session, token, parse) =>
    Show(session, "Reading IGMP snooping…", () => device.Multicast.GetSnoopingAsync(parse.GetValue(groupVlan), token)));
BindParse(Add(igmpSnoop, "set", "Set snooping for a VLAN", groupVlan, enable, autoLearn, hostTimeout, routerTimeout, leaveTimeout, querier, querierAddress), (device, session, token, parse) =>
    Plan(device, session, "Preparing IGMP change…", () => device.Multicast.PlanSnoopingAsync(
        parse.GetValue(groupVlan),
        parse.GetValue(enable) is string snoopEnable ? On(snoopEnable) : null,
        parse.GetValue(autoLearn) is string learnText ? On(learnText) : null,
        parse.GetValue(hostTimeout),
        parse.GetValue(routerTimeout),
        parse.GetValue(leaveTimeout),
        parse.GetValue(querier) is string querierText ? On(querierText) : null,
        parse.GetValue(querierAddress),
        token)));

Command dot1x = Add(root, "dot1x", "802.1X");
Option<string?> control = new("--control") { Description = "auto, force-authorized, or force-unauthorized" };
Option<string?> reauth = new("--reauth") { Description = "on or off" };
Option<string?> dot1xPort = new("--port") { Description = "Port" };
BindParse(Add(dot1x, "show", "Show 802.1X", dot1xPort), (device, session, token, parse) =>
    Show(session, "Reading 802.1X…", async () => Fields(await device.Dot1x.GetPortAsync(PortName.Parse(parse.GetValue(dot1xPort) ?? "g1"), token).ConfigureAwait(false))));
BindParse(Add(dot1x, "set", "Set 802.1X port control", dot1xPort, control, reauth), (device, session, token, parse) =>
    Plan(device, session, "Preparing 802.1X change…", () => device.Dot1x.PlanPortAsync(
        PortName.Parse(parse.GetValue(dot1xPort) ?? throw new FormatException("--port is required.")),
        parse.GetValue(control),
        parse.GetValue(reauth) is string reauthText ? On(reauthText) : null,
        token)));
Command dot1xHosts = Add(dot1x, "hosts", "Multiple hosts");
Option<string> hostMode = new("--host-mode") { Description = "single, multiple, or multi-session", Required = true };
BindParse(Add(dot1xHosts, "show", "Show host mode", portName), (device, session, token, parse) =>
    Show(session, "Reading host mode…", async () => Fields(await device.Dot1x.GetHostsAsync(PortName.Parse(parse.GetValue(portName)!), token).ConfigureAwait(false))));
BindParse(Add(dot1xHosts, "set", "Set host mode", portName, hostMode), (device, session, token, parse) =>
    Plan(device, session, "Preparing 802.1X change…", () => device.Dot1x.PlanHostsAsync(PortName.Parse(parse.GetValue(portName)!), parse.GetValue(hostMode)!, token)));
Command dot1xUsers = Add(dot1x, "users", "Authenticated users");
Bind(Add(dot1xUsers, "list", "List authenticated users"), (device, session, token) =>
    Show(session, "Reading authenticated users…", async () => Fields(await device.Dot1x.UsersAsync(token).ConfigureAwait(false))));

Command system = Add(root, "system", "System");
Bind(Add(system, "info", "Asset and identity"), (device, session, token) =>
    Show(session, "Reading system info…", () => device.System.GetAssetAsync(token)));
Bind(Add(system, "versions", "Firmware versions"), (device, session, token) =>
    Show(session, "Reading firmware versions…", () => device.System.GetVersionsAsync(token)));
Command asset = Add(system, "asset", "Asset tag");
Option<string> assetTag = new("--tag") { Description = "Asset tag", Required = true };
BindParse(Add(asset, "set", "Set the asset tag", assetTag), (device, session, token, parse) =>
    Plan(device, session, "Preparing system change…", () => device.System.PlanAssetTagAsync(parse.GetValue(assetTag)!, token)));
Bind(Add(system, "reset", "Reset the switch"), (device, session, token) =>
    Plan(device, session, "Preparing system change…", () => device.System.PlanResetAsync(token)));
Command secure = Add(system, "secure-mode", "Secure mode");
Argument<string> secureMode = new("mode") { Description = "managed or unmanaged" };
BindParse(Add(secure, "set", "Set secure mode", secureMode), (device, session, token, parse) =>
    Plan(device, session, "Preparing system change…", () => device.System.PlanSecureModeAsync(parse.GetValue(secureMode)!, token)));

Command ip = Add(root, "ip", "Management IP");
Option<string?> address = new("--address");
Option<string?> mask = new("--mask");
Option<string?> gateway = new("--gateway");
Bind(Add(ip, "show", "Show the management address"), (device, session, token) =>
    Show(session, "Reading management address…", () => device.Ip.GetAsync(token)));
BindParse(Add(ip, "set", "Change the management address", address, mask, gateway), (device, session, token, parse) =>
    Plan(device, session, "Preparing address change…", () => device.Ip.PlanSetAsync(parse.GetValue(address), parse.GetValue(mask), parse.GetValue(gateway), token)));

Command diag = Add(root, "diag", "Diagnostics");
BindParse(Add(diag, "cable-test", "Run the copper cable test", portName), (device, session, token, parse) =>
    Show(session, "Running cable test…", async () => Fields(await device.Diagnostics.CableTestAsync(PortName.Parse(parse.GetValue(portName)!), token).ConfigureAwait(false))));
Bind(Add(diag, "transceiver", "Show transceiver diagnostics"), (device, session, token) =>
    Show(session, "Reading transceiver diagnostics…", async () => Fields(await device.Diagnostics.TransceiverAsync(token).ConfigureAwait(false))));

Command users = Add(root, "users", "Local users");
Argument<string> userNameArg = new("name") { Description = "User name" };
Option<bool> userPassword = new("--user-password") { Description = "Read the new password from a hidden prompt, or from stdin when input is not a terminal" };
Option<int?> level = new("--level") { Description = "Privilege level" };
Bind(Add(users, "list", "List users"), (device, session, token) =>
    Show(session, "Reading users…", () => device.Users.ListAsync(token)));
BindParse(Add(users, "add", "Add a user", userNameArg, userPassword, level), async (device, session, token, parse) =>
{
    string? password = ReadUserPassword(parse.GetValue(userPassword));
    return await Plan(device, session, "Preparing user change…", () => device.Users.PlanUpsertAsync(parse.GetValue(userNameArg)!, password, parse.GetValue(level), true, token)).ConfigureAwait(false);
});
BindParse(Add(users, "set", "Update a user", userNameArg, userPassword, level), async (device, session, token, parse) =>
{
    string? password = ReadUserPassword(parse.GetValue(userPassword));
    return await Plan(device, session, "Preparing user change…", () => device.Users.PlanUpsertAsync(parse.GetValue(userNameArg)!, password, parse.GetValue(level), false, token)).ConfigureAwait(false);
});
BindParse(Add(users, "delete", "Delete a user", userNameArg), (device, session, token, parse) =>
    Plan(device, session, "Preparing user change…", () => device.Users.PlanDeleteAsync(parse.GetValue(userNameArg)!, device.SessionUser, token)));

Command radius = Add(root, "radius", "RADIUS servers");
Argument<string> radiusAddress = new("address") { Description = "Server address" };
Option<string?> radiusKey = new("--key");
Option<int?> authPort = new("--auth-port");
Option<int?> radiusTimeout = new("--radius-timeout");
Option<int?> retries = new("--retries");
Bind(Add(radius, "list", "List RADIUS settings"), (device, session, token) =>
    Show(session, "Reading RADIUS…", async () => Fields(await device.Radius.GetAsync(token).ConfigureAwait(false))));
BindParse(Add(radius, "add", "Add a RADIUS server", radiusAddress, radiusKey, authPort, radiusTimeout, retries), (device, session, token, parse) =>
    RadiusPlan(device, session, token, parse, RadiusRowAction.Create));
BindParse(Add(radius, "set", "Update a RADIUS server", radiusAddress, radiusKey, authPort, radiusTimeout, retries), (device, session, token, parse) =>
    RadiusPlan(device, session, token, parse, RadiusRowAction.Update));
BindParse(Add(radius, "delete", "Delete a RADIUS server", radiusAddress), (device, session, token, parse) =>
    RadiusPlan(device, session, token, parse, RadiusRowAction.Delete));

Command snmp = Add(root, "snmp", "SNMP");
Option<string?> authTraps = new("--auth-traps") { Description = "on or off" };
Option<string?> alarms = new("--alarms") { Description = "on or off" };
Bind(Add(snmp, "show", "Show SNMP globals"), (device, session, token) =>
    Show(session, "Reading SNMP…", () => device.Snmp.GetGlobalAsync(token)));
BindParse(Add(snmp, "set", "Set SNMP globals", authTraps, alarms), (device, session, token, parse) =>
    Plan(device, session, "Preparing SNMP change…", () => device.Snmp.PlanGlobalAsync(
        parse.GetValue(authTraps) is string traps ? On(traps) : null,
        parse.GetValue(alarms) is string alarmText ? On(alarmText) : null,
        token)));
Command community = Add(snmp, "community", "SNMP communities");
Argument<string> communityString = new("community") { Description = "Community string" };
Option<string?> access = new("--access") { Description = "ro, rw, or su" };
Option<string?> station = new("--station") { Description = "Management station" };
Bind(Add(community, "list", "List communities"), (device, session, token) =>
    Show(session, "Reading SNMP communities…", () => device.Snmp.ListCommunitiesAsync(token)));
BindParse(Add(community, "add", "Add a community", communityString, access, station), (device, session, token, parse) =>
    Plan(device, session, "Preparing SNMP change…", () => device.Snmp.PlanCommunityAsync(parse.GetValue(communityString)!, parse.GetValue(access), parse.GetValue(station), false, token)));
BindParse(Add(community, "delete", "Delete a community", communityString, station), (device, session, token, parse) =>
    Plan(device, session, "Preparing SNMP change…", () => device.Snmp.PlanCommunityAsync(parse.GetValue(communityString)!, null, parse.GetValue(station), true, token)));
Command trap = Add(snmp, "trap-recipient", "Trap recipients");
Option<string?> trapVersion = new("--version");
Bind(Add(trap, "list", "List trap recipients"), (device, session, token) =>
    Show(session, "Reading trap recipients…", async () => Fields(await device.Snmp.RecipientsPageAsync(token).ConfigureAwait(false))));
BindParse(Add(trap, "add", "Add a trap recipient", communityString, station, trapVersion), (device, session, token, parse) =>
    Plan(device, session, "Preparing SNMP change…", () => device.Snmp.PlanRecipientAsync(parse.GetValue(communityString)!, parse.GetValue(station) ?? "0.0.0.0", parse.GetValue(trapVersion), false, token)));
BindParse(Add(trap, "delete", "Delete a trap recipient", communityString, station), (device, session, token, parse) =>
    Plan(device, session, "Preparing SNMP change…", () => device.Snmp.PlanRecipientAsync(parse.GetValue(communityString)!, parse.GetValue(station) ?? "0.0.0.0", null, true, token)));

Command file = Add(root, "file", "File management");
Option<string> tftp = new("--tftp") { Description = "TFTP server", Required = true };
Option<string> fileName = new("--file") { Description = "File name", Required = true };
Option<string> fileType = new("--type") { Description = "firmware, boot, or config", Required = true };
BindParse(Add(file, "download", "Download a file onto the switch", tftp, fileName, fileType), (device, session, token, parse) =>
    Plan(device, session, "Preparing file operation…", () => device.Files.PlanDownloadAsync(parse.GetValue(tftp)!, parse.GetValue(fileName)!, parse.GetValue(fileType)!, token)));
Option<string> uploadType = new("--type") { Description = "config or backup", Required = true };
BindParse(Add(file, "upload", "Upload the configuration", tftp, fileName, uploadType), (device, session, token, parse) =>
    Plan(device, session, "Preparing file operation…", () => device.Files.PlanUploadAsync(parse.GetValue(tftp)!, parse.GetValue(fileName)!, parse.GetValue(uploadType)!, token)));
Bind(Add(file, "restore-defaults", "Restore configuration factory defaults"), (device, session, token) =>
    Plan(device, session, "Preparing file operation…", () => device.Files.PlanRestoreDefaultsAsync(token)));

Command dhcp = Add(root, "dhcp-server", "DHCP server");
Option<string?> dhcpEnable = new("--enable") { Description = "on or off" };
Option<string?> probe = new("--probe") { Description = "on or off" };
Option<int?> probeTimeout = new("--probe-timeout");
Bind(Add(dhcp, "show", "Show DHCP server properties"), (device, session, token) =>
    Show(session, "Reading DHCP server…", () => device.Dhcp.GetPropertiesAsync(token)));
BindParse(Add(dhcp, "set", "Set DHCP server properties", dhcpEnable, probe, retries, probeTimeout), (device, session, token, parse) =>
    Plan(device, session, "Preparing DHCP change…", () => device.Dhcp.PlanPropertiesAsync(
        parse.GetValue(dhcpEnable) is string dhcpOn ? On(dhcpOn) : null,
        parse.GetValue(probe) is string probeText ? On(probeText) : null,
        parse.GetValue(retries),
        parse.GetValue(probeTimeout),
        token)));
Command pool = Add(dhcp, "pool", "Network pool");
Option<string?> poolStart = new("--start");
Option<string?> poolEnd = new("--end");
Bind(Add(pool, "show", "Show the pool"), (device, session, token) =>
    Show(session, "Reading DHCP pool…", async () => Fields(await device.Dhcp.PoolAsync(token).ConfigureAwait(false))));
BindParse(Add(pool, "set", "Set the pool", poolStart, poolEnd, mask), (device, session, token, parse) =>
    Plan(device, session, "Preparing DHCP change…", () => device.Dhcp.PlanPoolAsync(parse.GetValue(poolStart), parse.GetValue(poolEnd), parse.GetValue(mask), token)));
Command excluded = Add(dhcp, "excluded", "Excluded addresses");
Argument<string> rangeFrom = new("from");
Argument<string> rangeTo = new("to");
Bind(Add(excluded, "list", "List exclusions"), (device, session, token) =>
    Show(session, "Reading DHCP exclusions…", () => device.Dhcp.ListExcludedAsync(token)));
BindParse(Add(excluded, "add", "Add an exclusion", rangeFrom, rangeTo), (device, session, token, parse) =>
    Plan(device, session, "Preparing DHCP change…", () => device.Dhcp.PlanExcludedAsync(parse.GetValue(rangeFrom)!, parse.GetValue(rangeTo)!, false, token)));
BindParse(Add(excluded, "delete", "Delete an exclusion", rangeFrom, rangeTo), (device, session, token, parse) =>
    Plan(device, session, "Preparing DHCP change…", () => device.Dhcp.PlanExcludedAsync(parse.GetValue(rangeFrom)!, parse.GetValue(rangeTo)!, true, token)));
Command staticHost = Add(dhcp, "static", "Static hosts");
Argument<string> hostAddress = new("address");
Option<string?> hostMac = new("--mac");
Option<string?> hostName = new("--name");
Bind(Add(staticHost, "list", "List static hosts"), (device, session, token) =>
    Show(session, "Reading static hosts…", async () => Fields(await device.Dhcp.StaticHostsAsync(token).ConfigureAwait(false))));
BindParse(Add(staticHost, "add", "Add a static host", hostAddress, hostMac, hostName), (device, session, token, parse) =>
    Plan(device, session, "Preparing DHCP change…", () => device.Dhcp.PlanStaticAsync(parse.GetValue(hostAddress)!, parse.GetValue(hostMac) ?? "", parse.GetValue(hostName), DhcpStaticAction.Add, token)));
BindParse(Add(staticHost, "set", "Update a static host", hostAddress, hostMac, hostName), (device, session, token, parse) =>
    Plan(device, session, "Preparing DHCP change…", () => device.Dhcp.PlanStaticAsync(parse.GetValue(hostAddress)!, parse.GetValue(hostMac) ?? "", parse.GetValue(hostName), DhcpStaticAction.Update, token)));
BindParse(Add(staticHost, "delete", "Delete a static host", hostAddress), (device, session, token, parse) =>
    Plan(device, session, "Preparing DHCP change…", () => device.Dhcp.PlanStaticAsync(parse.GetValue(hostAddress)!, "", null, DhcpStaticAction.Delete, token)));
Command bindings = Add(dhcp, "bindings", "Address bindings");
Bind(Add(bindings, "list", "List bindings"), (device, session, token) =>
    Show(session, "Reading DHCP bindings…", async () => Fields(await device.Dhcp.BindingsAsync(token).ConfigureAwait(false))));

Command jumbo = Add(root, "jumbo-frames", "Jumbo frames");
Argument<string> jumboState = new("state") { Description = "on or off" };
Bind(Add(jumbo, "show", "Show jumbo frames"), (device, session, token) =>
    Show(session, "Reading jumbo frames…", () => device.System.GetJumboAsync(token)));
BindParse(Add(jumbo, "set", "Set jumbo frames after the next reset", jumboState), (device, session, token, parse) =>
    Plan(device, session, "Preparing jumbo frame change…", () => device.System.PlanJumboAsync(On(parse.GetValue(jumboState)!), token)));

Command stats = Add(root, "stats", "Statistics");
Option<bool> resetCounters = new("--reset");
BindParse(Add(stats, "rmon", "RMON counters", portName, resetCounters), async (device, session, token, parse) =>
{
    PortName statsPort = PortName.Parse(parse.GetValue(portName)!);
    if (parse.GetValue(resetCounters))
    {
        return await Plan(device, session, "Preparing counter reset…", () => device.Stats.PlanResetAsync(statsPort, token)).ConfigureAwait(false);
    }

    return await Show(session, "Reading RMON counters…", () => device.Stats.RmonAsync(statsPort, token)).ConfigureAwait(false);
});
Bind(Add(stats, "cpu", "CPU utilization"), (device, session, token) =>
    Show(session, "Reading CPU utilization…", () => device.Stats.CpuAsync(token)));

Command qos = Add(root, "qos", "Quality of service");
Option<string?> qosMode = new("--mode") { Description = "basic, advanced, or disabled" };
Option<string?> trust = new("--trust") { Description = "cos or dscp" };
Bind(Add(qos, "show", "Show global QoS"), (device, session, token) =>
    Show(session, "Reading QoS…", () => device.Qos.GetGlobalAsync(token)));
BindParse(Add(qos, "set", "Set global QoS", qosMode, trust), (device, session, token, parse) =>
    Plan(device, session, "Preparing QoS change…", () => device.Qos.PlanGlobalAsync(parse.GetValue(qosMode), parse.GetValue(trust), token)));
Command qosIf = Add(qos, "interface", "Interface QoS");
Option<int> defaultPriority = new("--default-priority") { Required = true };
BindParse(Add(qosIf, "show", "Show interface priority", portName), (device, session, token, parse) =>
    Show(session, "Reading interface QoS…", async () =>
    {
        string name = parse.GetValue(portName)!;
        string value = await device.Qos.GetInterfaceAsync(PortName.Parse(name), token).ConfigureAwait(false);
        return new Dictionary<string, string> { ["port"] = name, ["priority"] = value };
    }));
BindParse(Add(qosIf, "set", "Set interface priority", portName, defaultPriority), (device, session, token, parse) =>
    Plan(device, session, "Preparing QoS change…", () => device.Qos.PlanInterfaceAsync(PortName.Parse(parse.GetValue(portName)!), parse.GetValue(defaultPriority), token)));
Command qosQueue = Add(qos, "queue", "Queue scheduling");
Option<string?> weights = new("--wrr") { Description = "Eight comma-separated weights" };
Option<bool> strict = new("--strict");
Bind(Add(qosQueue, "show", "Show queues"), (device, session, token) =>
    Show(session, "Reading queues…", () => device.Qos.GetQueuesAsync(token)));
BindParse(Add(qosQueue, "set", "Set queues", weights, strict), async (device, session, token, parse) =>
{
    List<int?> parsed = new List<int?>();
    if (parse.GetValue(weights) is string weightText)
    {
        foreach (string part in weightText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            parsed.Add(int.Parse(part, CultureInfo.InvariantCulture));
        }
    }

    return await Plan(device, session, "Preparing QoS change…", () => device.Qos.PlanQueuesAsync(parsed, parse.GetValue(strict) ? true : null, token)).ConfigureAwait(false);
});
Command cosMap = Add(qos, "cos-map", "CoS to queue");
Argument<int> cosValue = new("cos");
Argument<int> queueValue = new("queue");
Bind(Add(cosMap, "show", "Show the CoS map"), (device, session, token) =>
    Show(session, "Reading CoS map…", () => device.Qos.GetCosMapAsync(token)));
BindParse(Add(cosMap, "set", "Set one CoS mapping", cosValue, queueValue), (device, session, token, parse) =>
    Plan(device, session, "Preparing QoS change…", () => device.Qos.PlanCosMapAsync(parse.GetValue(cosValue), parse.GetValue(queueValue), token)));
Command dscpMap = Add(qos, "dscp-map", "DSCP to queue");
Argument<int> dscpValue = new("dscp");
Bind(Add(dscpMap, "show", "Show the DSCP map"), (device, session, token) =>
    Show(session, "Reading DSCP map…", () => device.Qos.GetDscpMapAsync(token)));
BindParse(Add(dscpMap, "set", "Set one DSCP mapping", dscpValue, queueValue), (device, session, token, parse) =>
    Plan(device, session, "Preparing QoS change…", () => device.Qos.PlanDscpMapAsync(parse.GetValue(dscpValue), parse.GetValue(queueValue), token)));

Command config = Add(root, "config", "Configuration snapshot");
Option<string?> output = new("-o") { Description = "Output file" };
Argument<string> snapshotFile = new("file") { Description = "Snapshot JSON file" };
BindParse(Add(config, "export", "Export readable configuration", output), async (device, session, token, parse) =>
{
    ConfigSnapshot snapshot = await CliActivity.RunAsync(
        session,
        "Exporting configuration…",
        () => ConfigSnapshot.CaptureAsync(device.Client, token)).ConfigureAwait(false);
    string path = parse.GetValue(output) ?? "pc28xx-config.json";
    await snapshot.SaveAsync(path, token).ConfigureAwait(false);
    Console.WriteLine(path);
    return 0;
});
BindParse(Add(config, "diff", "Show what apply would change", snapshotFile), async (device, session, token, parse) =>
{
    ConfigSnapshot snapshot = await ConfigSnapshot.LoadAsync(parse.GetValue(snapshotFile)!, token).ConfigureAwait(false);
    ChangePlan plan = await CliActivity.RunAsync(
        session,
        "Comparing configuration…",
        () => snapshot.PlanApplyAsync(device.Client, token)).ConfigureAwait(false);
    CliSession dry = new CliSession
    {
        Host = session.Host,
        User = session.User,
        Password = session.Password,
        DryRun = true,
        Json = session.Json,
        Verbose = session.Verbose
    };
    return await new ChangeExecutor().ExecuteAsync(plan, dry.Execution(Console.WriteLine), token).ConfigureAwait(false);
});
BindParse(Add(config, "apply", "Apply a snapshot", snapshotFile), async (device, session, token, parse) =>
{
    ConfigSnapshot snapshot = await ConfigSnapshot.LoadAsync(parse.GetValue(snapshotFile)!, token).ConfigureAwait(false);
    return await Plan(device, session, "Preparing configuration apply…", () => snapshot.PlanApplyAsync(device.Client, token)).ConfigureAwait(false);
});

Command raw = Add(root, "raw", "Raw page access");
Argument<string> page = new("page") { Description = "Page name or /config path" };
Option<string?> select = new("--select") { Description = "Selector appended after the page, including the leading ?" };
BindParse(Add(raw, "get", "Dump parsed fields", page, select), (device, session, token, parse) =>
    Show(session, "Reading page…", async () =>
    {
        string path = parse.GetValue(page)! + (parse.GetValue(select) ?? "");
        return Fields(await device.Raw.GetAsync(path, token).ConfigureAwait(false));
    }));
Option<string[]> assignments = new("--set") { Description = "field=value", Arity = ArgumentArity.OneOrMore };
BindParse(Add(raw, "set", "Replay a page with field=value mutations", page, assignments), async (device, session, token, parse) =>
{
    Dictionary<string, string> mutations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (string assignment in parse.GetValue(assignments) ?? Array.Empty<string>())
    {
        int split = assignment.IndexOf('=');
        if (split <= 0)
        {
            throw new FormatException("Expected field=value, got '" + assignment + "'.");
        }

        mutations[assignment[..split]] = assignment[(split + 1)..];
    }

    return await Plan(device, session, "Preparing page change…", () => device.Raw.PlanSetAsync(parse.GetValue(page)!, mutations, session.IUnderstand, token)).ConfigureAwait(false);
});

return await root.Parse(args).InvokeAsync().ConfigureAwait(false);

static bool On(string text)
{
    string normalized = text.Trim().ToLowerInvariant();
    if (normalized is "on" or "enable" or "true" or "1")
    {
        return true;
    }

    if (normalized is "off" or "disable" or "false" or "0")
    {
        return false;
    }

    throw new FormatException("Expected on or off, got '" + text + "'.");
}

static bool ParseAdmin(string text)
{
    if (text.Equals("up", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (text.Equals("down", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    throw new FormatException("Admin status must be up or down.");
}

static Dictionary<string, string> Fields(FormPage page)
{
    Dictionary<string, string> values = new Dictionary<string, string>();
    foreach (FormField field in page.Fields)
    {
        if (field.Role is FieldRole.Metadata or FieldRole.Marker)
        {
            continue;
        }

        if (ConfigSnapshot.IsSensitive(field.Name))
        {
            values[field.Name] = "***";
            continue;
        }

        values[field.Name] = field.Value;
    }

    return values;
}

static Task<int> Apply(SwitchDevice device, CliSession session, ChangePlan plan)
{
    return CliHost.Apply(device, session, plan);
}

static async Task<int> Show<T>(CliSession session, string status, Func<Task<T>> load)
{
    T value = await CliActivity.RunAsync(session, status, load).ConfigureAwait(false);
    TextOutput.Write(value, session.Json);
    return 0;
}

static async Task<int> Plan(SwitchDevice device, CliSession session, string status, Func<Task<ChangePlan>> load)
{
    ChangePlan plan = await CliActivity.RunAsync(session, status, load).ConfigureAwait(false);
    return await Apply(device, session, plan).ConfigureAwait(false);
}

static string? ReadUserPassword(bool requested)
{
    if (!requested)
    {
        return null;
    }

    string password = Console.IsInputRedirected ? Console.ReadLine() ?? "" : ReadSecretLine();

    if (password.Length == 0)
    {
        throw new FormatException("User password was not provided.");
    }

    return password;
}

static string ReadSecretLine()
{
    if (AnsiConsole.Profile.Capabilities.Interactive)
    {
        return AnsiConsole.Prompt(new TextPrompt<string>("User password:").Secret());
    }

    Console.Error.Write("User password: ");
    StringBuilder builder = new StringBuilder();
    while (true)
    {
        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.Error.WriteLine();
            return builder.ToString();
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (builder.Length > 0)
            {
                builder.Length--;
            }

            continue;
        }

        if (!char.IsControl(key.KeyChar))
        {
            builder.Append(key.KeyChar);
        }
    }
}

async Task<int> RadiusPlan(SwitchDevice device, CliSession session, CancellationToken token, ParseResult parse, RadiusRowAction action)
{
    return await Plan(device, session, "Preparing RADIUS change…", () => device.Radius.PlanAsync(
        parse.GetValue(radiusAddress)!,
        parse.GetValue(radiusKey),
        parse.GetValue(authPort),
        parse.GetValue(radiusTimeout),
        parse.GetValue(retries),
        action,
        token)).ConfigureAwait(false);
}

static Command Add(Command parent, string name, string description, params Symbol[] symbols)
{
    Command command = new Command(name, description);
    foreach (Symbol symbol in symbols)
    {
        if (symbol is Option option)
        {
            command.Options.Add(option);
        }
        else if (symbol is Argument argument)
        {
            command.Arguments.Add(argument);
        }
    }

    parent.Subcommands.Add(command);
    return command;
}

static void Bind(Command command, Func<SwitchDevice, CliSession, CancellationToken, Task<int>> action)
{
    command.SetAction((parse, token) => CliHost.Run(parse, token, action));
}

static void BindParse(Command command, Func<SwitchDevice, CliSession, CancellationToken, ParseResult, Task<int>> action)
{
    command.SetAction((parse, token) => CliHost.Run(parse, token, (device, session, cancellationToken) => action(device, session, cancellationToken, parse)));
}

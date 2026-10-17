using System.CommandLine;
using System.Globalization;

using Nefarius.PowerConnect.Cli;
using Nefarius.PowerConnect.Ews;
using Nefarius.PowerConnect.Ews.Changes;
using Nefarius.PowerConnect.Ews.Ews;
using Nefarius.PowerConnect.Ews.Ports;
using Nefarius.PowerConnect.Ews.Qos;
using Nefarius.PowerConnect.Ews.Snapshot;

RootCommand root = new("pc28xx drives a Dell PowerConnect 28xx web UI. Writes are shown and applied only after confirmation.");
CliHost.AddGlobals(root);

Command vlan = Add(root, "vlan", "VLAN membership");
Bind(Add(vlan, "list", "List VLANs"), async (device, session, token) =>
{
    TextOutput.Write(await device.Vlans.ListAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
Argument<int> vlanId = new("id") { Description = "VLAN id" };
BindParse(Add(vlan, "show", "Show one VLAN", vlanId), async (device, session, token, parse) =>
{
    TextOutput.Write(await device.Vlans.GetAsync(parse.GetValue(vlanId), token).ConfigureAwait(false), session.Json);
    return 0;
});
Option<string> vlanName = new("--name") { Description = "VLAN name", Required = true };
BindParse(Add(vlan, "create", "Create a VLAN", vlanId, vlanName), async (device, session, token, parse) =>
    await Apply(device, session, await device.Vlans.PlanCreateAsync(parse.GetValue(vlanId), parse.GetValue(vlanName)!, token).ConfigureAwait(false)).ConfigureAwait(false));
BindParse(Add(vlan, "delete", "Delete a VLAN", vlanId), async (device, session, token, parse) =>
    await Apply(device, session, await device.Vlans.PlanDeleteAsync(parse.GetValue(vlanId), token).ConfigureAwait(false)).ConfigureAwait(false));
Option<string?> tagged = new("--tagged") { Description = "Ports to tag, comma separated" };
Option<string?> untagged = new("--untagged") { Description = "Ports to leave untagged" };
Option<string?> forbidden = new("--forbidden") { Description = "Ports to forbid" };
Option<string?> none = new("--none") { Description = "Ports to remove from the VLAN" };
BindParse(Add(vlan, "set", "Edit VLAN membership", vlanId, tagged, untagged, forbidden, none), async (device, session, token, parse) =>
    await Apply(device, session, await device.Vlans.PlanSetAsync(
        parse.GetValue(vlanId),
        PortName.ParseList(parse.GetValue(tagged)),
        PortName.ParseList(parse.GetValue(untagged)),
        PortName.ParseList(parse.GetValue(forbidden)),
        PortName.ParseList(parse.GetValue(none)),
        token).ConfigureAwait(false)).ConfigureAwait(false));

Command vlanPort = Add(vlan, "port", "Per-port VLAN settings");
Argument<string> portName = new("port") { Description = "Port or LAG (g1-g16, ch1-ch8)" };
Bind(Add(vlanPort, "list", "List PVID and ingress settings"), async (device, session, token) =>
{
    TextOutput.Write(await device.Vlans.ListPortsAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(vlanPort, "show", "Show one port", portName), async (device, session, token, parse) =>
{
    TextOutput.Write(await device.Vlans.GetPortAsync(PortName.Parse(parse.GetValue(portName)!), token).ConfigureAwait(false), session.Json);
    return 0;
});
Option<int?> pvid = new("--pvid") { Description = "Port VLAN id" };
Option<string?> frameType = new("--frame-type") { Description = "admit-all or tagged-only" };
Option<string?> ingress = new("--ingress-filtering") { Description = "on or off" };
BindParse(Add(vlanPort, "set", "Set PVID, frame type, or ingress filtering", portName, pvid, frameType, ingress), async (device, session, token, parse) =>
{
    bool? filtering = parse.GetValue(ingress) is string text ? On(text) : null;
    return await Apply(device, session, await device.Vlans.PlanPortSetAsync(
        PortName.Parse(parse.GetValue(portName)!),
        parse.GetValue(pvid),
        parse.GetValue(frameType),
        filtering,
        token).ConfigureAwait(false)).ConfigureAwait(false);
});

Command port = Add(root, "port", "Port configuration");
Bind(Add(port, "list", "List copper ports"), async (device, session, token) =>
{
    TextOutput.Write(await device.Ports.ListAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(port, "show", "Show one port", portName), async (device, session, token, parse) =>
{
    TextOutput.Write(await device.Ports.GetAsync(PortName.Parse(parse.GetValue(portName)!), token).ConfigureAwait(false), session.Json);
    return 0;
});
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
        AdminUp = parse.GetValue(admin) is string adminText ? adminText.Equals("up", StringComparison.OrdinalIgnoreCase) : null,
        SpeedMegabits = parse.GetValue(speed),
        Duplex = parse.GetValue(duplex),
        AutoNegotiation = parse.GetValue(autoneg) is string autoText ? On(autoText) : null,
        FlowControl = parse.GetValue(flow),
        Mdix = parse.GetValue(mdix),
        BackPressure = parse.GetValue(backPressure) is string backText ? On(backText) : null,
        Description = parse.GetValue(description)
    };
    return await Apply(device, session, await device.Ports.PlanSetAsync(PortName.Parse(parse.GetValue(portName)!), update, token).ConfigureAwait(false)).ConfigureAwait(false);
});
Option<int> accessVlan = new("--vlan") { Description = "Access VLAN", Required = true };
BindParse(Add(port, "access", "Make a port an untagged member of one VLAN and set its PVID", portName, accessVlan), async (device, session, token, parse) =>
    await Apply(device, session, await device.Vlans.PlanAccessAsync(PortName.Parse(parse.GetValue(portName)!), parse.GetValue(accessVlan), token).ConfigureAwait(false)).ConfigureAwait(false));

Command lag = Add(root, "lag", "Link aggregation");
Argument<string> channel = new("channel") { Description = "LAG ch1-ch8" };
Bind(Add(lag, "list", "List LAGs"), async (device, session, token) =>
{
    TextOutput.Write(await device.Lags.ListAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(lag, "show", "Show one LAG", channel), async (device, session, token, parse) =>
{
    TextOutput.Write(await device.Lags.GetAsync(PortName.Parse(parse.GetValue(channel)!), token).ConfigureAwait(false), session.Json);
    return 0;
});
Option<string?> balance = new("--balance") { Description = "l2, l3, or l2l3" };
BindParse(Add(lag, "set", "Set LAG description or load balance", channel, description, balance), async (device, session, token, parse) =>
    await Apply(device, session, await device.Lags.PlanSetAsync(PortName.Parse(parse.GetValue(channel)!), parse.GetValue(description), parse.GetValue(balance), token).ConfigureAwait(false)).ConfigureAwait(false));
Command lagMembers = Add(lag, "members", "LAG membership");
Option<string> memberPorts = new("--ports") { Description = "Member ports", Required = true };
Option<bool> lacp = new("--lacp") { Description = "Enable LACP actor state" };
BindParse(Add(lagMembers, "set", "Replace LAG members", channel, memberPorts, lacp), async (device, session, token, parse) =>
    await Apply(device, session, await device.Lags.PlanMembersAsync(
        PortName.Parse(parse.GetValue(channel)!),
        PortName.ParseList(parse.GetValue(memberPorts)),
        parse.GetValue(lacp),
        token).ConfigureAwait(false)).ConfigureAwait(false));

Command green = Add(root, "green-ethernet", "Green Ethernet");
Option<string?> energy = new("--energy-detect") { Description = "on or off" };
Option<string?> reach = new("--short-reach") { Description = "on or off" };
Option<string?> greenPort = new("--port") { Description = "Limit the change to one port" };
Bind(Add(green, "show", "Show Green Ethernet"), async (device, session, token) =>
{
    TextOutput.Write(await device.GreenEthernet.GetAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(green, "set", "Set Green Ethernet", energy, reach, greenPort), async (device, session, token, parse) =>
    await Apply(device, session, await device.GreenEthernet.PlanSetAsync(
        parse.GetValue(energy) is string energyText ? On(energyText) : null,
        parse.GetValue(reach) is string reachText ? On(reachText) : null,
        parse.GetValue(greenPort) is string portText ? PortName.Parse(portText) : null,
        token).ConfigureAwait(false)).ConfigureAwait(false));

Command storm = Add(root, "storm-control", "Storm control");
Option<string?> broadcast = new("--broadcast") { Description = "on or off" };
Option<int?> rate = new("--rate") { Description = "Raw broadcast rate counter (UI shows this divided by 1000)" };
Option<string?> multicastFlag = new("--multicast") { Description = "on or off" };
Option<string?> unknown = new("--unknown-unicast") { Description = "on or off" };
BindParse(Add(storm, "show", "Show storm control", portName), async (device, session, token, parse) =>
{
    TextOutput.Write(await device.StormControl.GetAsync(PortName.Parse(parse.GetValue(portName)!), token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(storm, "set", "Set storm control", portName, broadcast, rate, multicastFlag, unknown), async (device, session, token, parse) =>
    await Apply(device, session, await device.StormControl.PlanSetAsync(
        PortName.Parse(parse.GetValue(portName)!),
        parse.GetValue(broadcast) is string broadcastText ? On(broadcastText) : null,
        parse.GetValue(rate),
        parse.GetValue(multicastFlag) is string multiText ? On(multiText) : null,
        parse.GetValue(unknown) is string unknownText ? On(unknownText) : null,
        token).ConfigureAwait(false)).ConfigureAwait(false));

Command mirror = Add(root, "mirror", "Port mirroring");
Option<string?> source = new("--source") { Description = "Source port" };
Option<string?> dest = new("--dest") { Description = "Destination port" };
Bind(Add(mirror, "show", "Show the mirror session"), async (device, session, token) =>
{
    (string mirrorSource, string mirrorDest) = await device.Mirror.GetAsync(token).ConfigureAwait(false);
    TextOutput.Write(new Dictionary<string, string> { ["source"] = mirrorSource, ["dest"] = mirrorDest }, session.Json);
    return 0;
});
BindParse(Add(mirror, "set", "Set the mirror session", source, dest), async (device, session, token, parse) =>
    await Apply(device, session, await device.Mirror.PlanSetAsync(
        parse.GetValue(source) is string sourceText ? PortName.Parse(sourceText) : null,
        parse.GetValue(dest) is string destText ? PortName.Parse(destText) : null,
        false,
        token).ConfigureAwait(false)).ConfigureAwait(false));
Bind(Add(mirror, "clear", "Clear the mirror session"), async (device, session, token) =>
    await Apply(device, session, await device.Mirror.PlanSetAsync(null, null, true, token).ConfigureAwait(false)).ConfigureAwait(false));

Command mac = Add(root, "mac-table", "Dynamic MAC address table");
Option<int?> macVlan = new("--vlan") { Description = "Filter by VLAN" };
Option<string?> macPort = new("--port") { Description = "Filter by port" };
BindParse(Add(mac, "list", "List learned addresses", macVlan, macPort), async (device, session, token, parse) =>
{
    TextOutput.Write(await device.MacTable.ListAsync(
        parse.GetValue(macVlan),
        parse.GetValue(macPort) is string macPortText ? PortName.Parse(macPortText) : null,
        token).ConfigureAwait(false), session.Json);
    return 0;
});
Bind(Add(mac, "clear", "Clear the dynamic table"), async (device, session, token) =>
    await Apply(device, session, await device.MacTable.PlanClearAsync(token).ConfigureAwait(false)).ConfigureAwait(false));

Command stp = Add(root, "stp", "Spanning tree");
Option<string?> enable = new("--enable") { Description = "on or off" };
Option<string?> mode = new("--mode") { Description = "stp or rstp" };
Option<int?> priority = new("--priority") { Description = "Bridge or port priority" };
Option<int?> hello = new("--hello") { Description = "Hello time in seconds" };
Option<int?> maxAge = new("--max-age") { Description = "Max age in seconds" };
Option<int?> forward = new("--forward-delay") { Description = "Forward delay in seconds" };
Option<string?> pathCostMethod = new("--path-cost-method") { Description = "short or long" };
Bind(Add(stp, "show", "Show bridge spanning tree"), async (device, session, token) =>
{
    TextOutput.Write(await device.Stp.GetBridgeAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(stp, "set", "Set bridge spanning tree", enable, mode, priority, hello, maxAge, forward, pathCostMethod), async (device, session, token, parse) =>
    await Apply(device, session, await device.Stp.PlanBridgeAsync(
        parse.GetValue(enable) is string enableText ? On(enableText) : null,
        parse.GetValue(mode),
        parse.GetValue(priority),
        parse.GetValue(hello),
        parse.GetValue(maxAge),
        parse.GetValue(forward),
        parse.GetValue(pathCostMethod),
        token).ConfigureAwait(false)).ConfigureAwait(false));
Command stpPort = Add(stp, "port", "Per-port spanning tree");
Option<int?> pathCost = new("--path-cost") { Description = "Admin path cost" };
Option<string?> edge = new("--edge") { Description = "on or off" };
Option<string?> restricted = new("--restricted-role") { Description = "on or off" };
Option<bool> migrate = new("--migrate") { Description = "Send a protocol migration" };
Bind(Add(stpPort, "list", "List STP port settings"), async (device, session, token) =>
{
    TextOutput.Write(await device.Stp.ListPortsAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(stpPort, "set", "Set STP on a port or LAG", portName, enable, priority, pathCost, edge, restricted, migrate), async (device, session, token, parse) =>
    await Apply(device, session, await device.Stp.PlanPortAsync(
        PortName.Parse(parse.GetValue(portName)!),
        parse.GetValue(enable) is string stpEnable ? On(stpEnable) : null,
        parse.GetValue(priority),
        parse.GetValue(pathCost),
        parse.GetValue(edge) is string edgeText ? On(edgeText) : null,
        parse.GetValue(restricted) is string restrictedText ? On(restrictedText) : null,
        parse.GetValue(migrate),
        token).ConfigureAwait(false)).ConfigureAwait(false));

Command mcast = Add(root, "multicast", "Bridge multicast");
Option<string?> filtering = new("--bridge-filtering") { Description = "on or off" };
Option<string?> igmp = new("--igmp-snooping") { Description = "on or off" };
Bind(Add(mcast, "show", "Show global multicast"), async (device, session, token) =>
{
    TextOutput.Write(await device.Multicast.GetGlobalAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(mcast, "set", "Set global multicast", filtering, igmp), async (device, session, token, parse) =>
    await Apply(device, session, await device.Multicast.PlanGlobalAsync(
        parse.GetValue(filtering) is string filterText ? On(filterText) : null,
        parse.GetValue(igmp) is string igmpText ? On(igmpText) : null,
        token).ConfigureAwait(false)).ConfigureAwait(false));
Command group = Add(mcast, "group", "Multicast groups");
Argument<int> groupVlan = new("vlan") { Description = "VLAN id" };
Argument<string> groupMac = new("mac") { Description = "Group MAC address" };
Option<string?> groupPorts = new("--ports") { Description = "Egress ports" };
Bind(Add(group, "list", "List groups"), async (device, session, token) =>
{
    TextOutput.Write(Fields(await device.Multicast.GroupsPageAsync(token).ConfigureAwait(false)), session.Json);
    return 0;
});
BindParse(Add(group, "add", "Add a group", groupVlan, groupMac, groupPorts), async (device, session, token, parse) =>
    await Apply(device, session, await device.Multicast.PlanGroupAsync(parse.GetValue(groupVlan), parse.GetValue(groupMac)!, PortName.ParseList(parse.GetValue(groupPorts)), false, token).ConfigureAwait(false)).ConfigureAwait(false));
BindParse(Add(group, "delete", "Delete a group", groupVlan, groupMac), async (device, session, token, parse) =>
    await Apply(device, session, await device.Multicast.PlanGroupAsync(parse.GetValue(groupVlan), parse.GetValue(groupMac)!, Array.Empty<PortName>(), true, token).ConfigureAwait(false)).ConfigureAwait(false));
Command forwardAll = Add(mcast, "forward-all", "Forward-all router ports");
Bind(Add(forwardAll, "show", "Show forward-all"), async (device, session, token) =>
{
    TextOutput.Write(Fields(await device.Multicast.ForwardAllAsync(token).ConfigureAwait(false)), session.Json);
    return 0;
});
BindParse(Add(forwardAll, "set", "Set forward-all ports", groupVlan, groupPorts), async (device, session, token, parse) =>
{
    FormPage page = await device.Multicast.ForwardAllAsync(token).ConfigureAwait(false);
    ChangePlan plan = new ChangePlan();
    plan.Add(FormChange.Post(device.Client, "Set forward-all", page, new Dictionary<string, string>
    {
        ["OneVlanId"] = parse.GetValue(groupVlan).ToString(CultureInfo.InvariantCulture)
    }));
    return await Apply(device, session, plan).ConfigureAwait(false);
});
Command igmpSnoop = Add(root, "igmp-snooping", "IGMP snooping per VLAN");
Option<string?> autoLearn = new("--auto-learn") { Description = "on or off" };
Option<int?> hostTimeout = new("--host-timeout");
Option<int?> routerTimeout = new("--router-timeout");
Option<int?> leaveTimeout = new("--leave-timeout");
Option<string?> querier = new("--querier") { Description = "on or off" };
Option<string?> querierAddress = new("--querier-address");
BindParse(Add(igmpSnoop, "show", "Show snooping for a VLAN", groupVlan), async (device, session, token, parse) =>
{
    TextOutput.Write(await device.Multicast.GetSnoopingAsync(parse.GetValue(groupVlan), token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(igmpSnoop, "set", "Set snooping for a VLAN", groupVlan, enable, autoLearn, hostTimeout, routerTimeout, leaveTimeout, querier, querierAddress), async (device, session, token, parse) =>
    await Apply(device, session, await device.Multicast.PlanSnoopingAsync(
        parse.GetValue(groupVlan),
        parse.GetValue(enable) is string snoopEnable ? On(snoopEnable) : null,
        parse.GetValue(autoLearn) is string learnText ? On(learnText) : null,
        parse.GetValue(hostTimeout),
        parse.GetValue(routerTimeout),
        parse.GetValue(leaveTimeout),
        parse.GetValue(querier) is string querierText ? On(querierText) : null,
        parse.GetValue(querierAddress),
        token).ConfigureAwait(false)).ConfigureAwait(false));

Command dot1x = Add(root, "dot1x", "802.1X");
Option<string?> control = new("--control") { Description = "auto, force-authorized, or force-unauthorized" };
Option<string?> reauth = new("--reauth") { Description = "on or off" };
Option<string?> dot1xPort = new("--port") { Description = "Port" };
BindParse(Add(dot1x, "show", "Show 802.1X", dot1xPort), async (device, session, token, parse) =>
{
    TextOutput.Write(Fields(await device.Dot1x.GetPortAsync(PortName.Parse(parse.GetValue(dot1xPort) ?? "g1"), token).ConfigureAwait(false)), session.Json);
    return 0;
});
BindParse(Add(dot1x, "set", "Set 802.1X port control", dot1xPort, control, reauth), async (device, session, token, parse) =>
    await Apply(device, session, await device.Dot1x.PlanPortAsync(
        PortName.Parse(parse.GetValue(dot1xPort) ?? "g1"),
        parse.GetValue(control),
        parse.GetValue(reauth) is string reauthText ? On(reauthText) : null,
        token).ConfigureAwait(false)).ConfigureAwait(false));
Command dot1xHosts = Add(dot1x, "hosts", "Multiple hosts");
Option<string> hostMode = new("--host-mode") { Description = "single, multiple, or multi-session", Required = true };
BindParse(Add(dot1xHosts, "show", "Show host mode", portName), async (device, session, token, parse) =>
{
    TextOutput.Write(Fields(await device.Dot1x.GetHostsAsync(PortName.Parse(parse.GetValue(portName)!), token).ConfigureAwait(false)), session.Json);
    return 0;
});
BindParse(Add(dot1xHosts, "set", "Set host mode", portName, hostMode), async (device, session, token, parse) =>
    await Apply(device, session, await device.Dot1x.PlanHostsAsync(PortName.Parse(parse.GetValue(portName)!), parse.GetValue(hostMode)!, token).ConfigureAwait(false)).ConfigureAwait(false));
Command dot1xUsers = Add(dot1x, "users", "Authenticated users");
Bind(Add(dot1xUsers, "list", "List authenticated users"), async (device, session, token) =>
{
    TextOutput.Write(Fields(await device.Dot1x.UsersAsync(token).ConfigureAwait(false)), session.Json);
    return 0;
});

Command system = Add(root, "system", "System");
Bind(Add(system, "info", "Asset and identity"), async (device, session, token) =>
{
    TextOutput.Write(await device.System.GetAssetAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
Bind(Add(system, "versions", "Firmware versions"), async (device, session, token) =>
{
    TextOutput.Write(await device.System.GetVersionsAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
Command asset = Add(system, "asset", "Asset tag");
Option<string> assetTag = new("--tag") { Description = "Asset tag", Required = true };
BindParse(Add(asset, "set", "Set the asset tag", assetTag), async (device, session, token, parse) =>
    await Apply(device, session, await device.System.PlanAssetTagAsync(parse.GetValue(assetTag)!, token).ConfigureAwait(false)).ConfigureAwait(false));
Bind(Add(system, "reset", "Reset the switch"), async (device, session, token) =>
    await Apply(device, session, await device.System.PlanResetAsync(token).ConfigureAwait(false)).ConfigureAwait(false));
Command secure = Add(system, "secure-mode", "Secure mode");
Argument<string> secureMode = new("mode") { Description = "managed or unmanaged" };
BindParse(Add(secure, "set", "Set secure mode", secureMode), async (device, session, token, parse) =>
    await Apply(device, session, await device.System.PlanSecureModeAsync(parse.GetValue(secureMode)!, token).ConfigureAwait(false)).ConfigureAwait(false));

Command ip = Add(root, "ip", "Management IP");
Option<string?> address = new("--address");
Option<string?> mask = new("--mask");
Option<string?> gateway = new("--gateway");
Bind(Add(ip, "show", "Show the management address"), async (device, session, token) =>
{
    TextOutput.Write(await device.Ip.GetAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(ip, "set", "Change the management address", address, mask, gateway), async (device, session, token, parse) =>
    await Apply(device, session, await device.Ip.PlanSetAsync(parse.GetValue(address), parse.GetValue(mask), parse.GetValue(gateway), token).ConfigureAwait(false)).ConfigureAwait(false));

Command diag = Add(root, "diag", "Diagnostics");
BindParse(Add(diag, "cable-test", "Run the copper cable test", portName), async (device, session, token, parse) =>
{
    TextOutput.Write(Fields(await device.Diagnostics.CableTestAsync(PortName.Parse(parse.GetValue(portName)!), token).ConfigureAwait(false)), session.Json);
    return 0;
});
Bind(Add(diag, "transceiver", "Show transceiver diagnostics"), async (device, session, token) =>
{
    TextOutput.Write(Fields(await device.Diagnostics.TransceiverAsync(token).ConfigureAwait(false)), session.Json);
    return 0;
});

Command users = Add(root, "users", "Local users");
Argument<string> userNameArg = new("name") { Description = "User name" };
Option<string?> userPassword = new("--password") { Description = "User password" };
Option<int?> level = new("--level") { Description = "Privilege level" };
Bind(Add(users, "list", "List users"), async (device, session, token) =>
{
    TextOutput.Write(await device.Users.ListAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(users, "add", "Add a user", userNameArg, userPassword, level), async (device, session, token, parse) =>
    await Apply(device, session, await device.Users.PlanUpsertAsync(parse.GetValue(userNameArg)!, parse.GetValue(userPassword), parse.GetValue(level), true, token).ConfigureAwait(false)).ConfigureAwait(false));
BindParse(Add(users, "set", "Update a user", userNameArg, userPassword, level), async (device, session, token, parse) =>
    await Apply(device, session, await device.Users.PlanUpsertAsync(parse.GetValue(userNameArg)!, parse.GetValue(userPassword), parse.GetValue(level), false, token).ConfigureAwait(false)).ConfigureAwait(false));
BindParse(Add(users, "delete", "Delete a user", userNameArg), async (device, session, token, parse) =>
    await Apply(device, session, await device.Users.PlanDeleteAsync(parse.GetValue(userNameArg)!, device.SessionUser, token).ConfigureAwait(false)).ConfigureAwait(false));

Command radius = Add(root, "radius", "RADIUS servers");
Argument<string> radiusAddress = new("address") { Description = "Server address" };
Option<string?> radiusKey = new("--key");
Option<int?> authPort = new("--auth-port");
Option<int?> radiusTimeout = new("--timeout");
Option<int?> retries = new("--retries");
Bind(Add(radius, "list", "List RADIUS settings"), async (device, session, token) =>
{
    TextOutput.Write(Fields(await device.Radius.GetAsync(token).ConfigureAwait(false)), session.Json);
    return 0;
});
BindParse(Add(radius, "add", "Add a RADIUS server", radiusAddress, radiusKey, authPort, radiusTimeout, retries), (device, session, token, parse) =>
    RadiusPlan(device, session, token, parse, false));
BindParse(Add(radius, "set", "Update a RADIUS server", radiusAddress, radiusKey, authPort, radiusTimeout, retries), (device, session, token, parse) =>
    RadiusPlan(device, session, token, parse, false));
BindParse(Add(radius, "delete", "Delete a RADIUS server", radiusAddress), (device, session, token, parse) =>
    RadiusPlan(device, session, token, parse, true));

Command snmp = Add(root, "snmp", "SNMP");
Option<string?> authTraps = new("--auth-traps") { Description = "on or off" };
Option<string?> alarms = new("--alarms") { Description = "on or off" };
Bind(Add(snmp, "show", "Show SNMP globals"), async (device, session, token) =>
{
    TextOutput.Write(await device.Snmp.GetGlobalAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(snmp, "set", "Set SNMP globals", authTraps, alarms), async (device, session, token, parse) =>
    await Apply(device, session, await device.Snmp.PlanGlobalAsync(
        parse.GetValue(authTraps) is string traps ? On(traps) : null,
        parse.GetValue(alarms) is string alarmText ? On(alarmText) : null,
        token).ConfigureAwait(false)).ConfigureAwait(false));
Command community = Add(snmp, "community", "SNMP communities");
Argument<string> communityString = new("community") { Description = "Community string" };
Option<string?> access = new("--access") { Description = "ro, rw, or su" };
Option<string?> station = new("--station") { Description = "Management station" };
Bind(Add(community, "list", "List communities"), async (device, session, token) =>
{
    TextOutput.Write(await device.Snmp.ListCommunitiesAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(community, "add", "Add a community", communityString, access, station), async (device, session, token, parse) =>
    await Apply(device, session, await device.Snmp.PlanCommunityAsync(parse.GetValue(communityString)!, parse.GetValue(access), parse.GetValue(station), false, token).ConfigureAwait(false)).ConfigureAwait(false));
BindParse(Add(community, "delete", "Delete a community", communityString, station), async (device, session, token, parse) =>
    await Apply(device, session, await device.Snmp.PlanCommunityAsync(parse.GetValue(communityString)!, null, parse.GetValue(station), true, token).ConfigureAwait(false)).ConfigureAwait(false));
Command trap = Add(snmp, "trap-recipient", "Trap recipients");
Option<string?> trapVersion = new("--version");
Bind(Add(trap, "list", "List trap recipients"), async (device, session, token) =>
{
    TextOutput.Write(Fields(await device.Snmp.RecipientsPageAsync(token).ConfigureAwait(false)), session.Json);
    return 0;
});
BindParse(Add(trap, "add", "Add a trap recipient", communityString, station, trapVersion), async (device, session, token, parse) =>
    await Apply(device, session, await device.Snmp.PlanRecipientAsync(parse.GetValue(communityString)!, parse.GetValue(station) ?? "0.0.0.0", parse.GetValue(trapVersion), false, token).ConfigureAwait(false)).ConfigureAwait(false));
BindParse(Add(trap, "delete", "Delete a trap recipient", communityString, station), async (device, session, token, parse) =>
    await Apply(device, session, await device.Snmp.PlanRecipientAsync(parse.GetValue(communityString)!, parse.GetValue(station) ?? "0.0.0.0", null, true, token).ConfigureAwait(false)).ConfigureAwait(false));

Command file = Add(root, "file", "File management");
Option<string> tftp = new("--tftp") { Description = "TFTP server", Required = true };
Option<string> fileName = new("--file") { Description = "File name", Required = true };
Option<string> fileType = new("--type") { Description = "firmware, boot, or config", Required = true };
BindParse(Add(file, "download", "Download a file onto the switch", tftp, fileName, fileType), async (device, session, token, parse) =>
    await Apply(device, session, await device.Files.PlanDownloadAsync(parse.GetValue(tftp)!, parse.GetValue(fileName)!, parse.GetValue(fileType)!, token).ConfigureAwait(false)).ConfigureAwait(false));
Option<string> uploadType = new("--type") { Description = "config or backup", Required = true };
BindParse(Add(file, "upload", "Upload the configuration", tftp, fileName, uploadType), async (device, session, token, parse) =>
    await Apply(device, session, await device.Files.PlanUploadAsync(parse.GetValue(tftp)!, parse.GetValue(fileName)!, parse.GetValue(uploadType)!, token).ConfigureAwait(false)).ConfigureAwait(false));
Bind(Add(file, "restore-defaults", "Restore configuration factory defaults"), async (device, session, token) =>
    await Apply(device, session, await device.Files.PlanRestoreDefaultsAsync(token).ConfigureAwait(false)).ConfigureAwait(false));

Command dhcp = Add(root, "dhcp-server", "DHCP server");
Option<string?> dhcpEnable = new("--enable") { Description = "on or off" };
Option<string?> probe = new("--probe") { Description = "on or off" };
Bind(Add(dhcp, "show", "Show DHCP server properties"), async (device, session, token) =>
{
    TextOutput.Write(await device.Dhcp.GetPropertiesAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(dhcp, "set", "Set DHCP server properties", dhcpEnable, probe, retries, radiusTimeout), async (device, session, token, parse) =>
    await Apply(device, session, await device.Dhcp.PlanPropertiesAsync(
        parse.GetValue(dhcpEnable) is string dhcpOn ? On(dhcpOn) : null,
        parse.GetValue(probe) is string probeText ? On(probeText) : null,
        parse.GetValue(retries),
        parse.GetValue(radiusTimeout),
        token).ConfigureAwait(false)).ConfigureAwait(false));
Command pool = Add(dhcp, "pool", "Network pool");
Option<string?> poolStart = new("--start");
Option<string?> poolEnd = new("--end");
Bind(Add(pool, "show", "Show the pool"), async (device, session, token) =>
{
    TextOutput.Write(Fields(await device.Dhcp.PoolAsync(token).ConfigureAwait(false)), session.Json);
    return 0;
});
BindParse(Add(pool, "set", "Set the pool", poolStart, poolEnd, mask), async (device, session, token, parse) =>
    await Apply(device, session, await device.Dhcp.PlanPoolAsync(parse.GetValue(poolStart), parse.GetValue(poolEnd), parse.GetValue(mask), token).ConfigureAwait(false)).ConfigureAwait(false));
Command excluded = Add(dhcp, "excluded", "Excluded addresses");
Argument<string> rangeFrom = new("from");
Argument<string> rangeTo = new("to");
Bind(Add(excluded, "list", "List exclusions"), async (device, session, token) =>
{
    TextOutput.Write(await device.Dhcp.ListExcludedAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(excluded, "add", "Add an exclusion", rangeFrom, rangeTo), async (device, session, token, parse) =>
    await Apply(device, session, await device.Dhcp.PlanExcludedAsync(parse.GetValue(rangeFrom)!, parse.GetValue(rangeTo)!, false, token).ConfigureAwait(false)).ConfigureAwait(false));
BindParse(Add(excluded, "delete", "Delete an exclusion", rangeFrom, rangeTo), async (device, session, token, parse) =>
    await Apply(device, session, await device.Dhcp.PlanExcludedAsync(parse.GetValue(rangeFrom)!, parse.GetValue(rangeTo)!, true, token).ConfigureAwait(false)).ConfigureAwait(false));
Command staticHost = Add(dhcp, "static", "Static hosts");
Argument<string> hostAddress = new("address");
Option<string?> hostMac = new("--mac");
Option<string?> hostName = new("--name");
Bind(Add(staticHost, "list", "List static hosts"), async (device, session, token) =>
{
    TextOutput.Write(Fields(await device.Dhcp.StaticHostsAsync(token).ConfigureAwait(false)), session.Json);
    return 0;
});
BindParse(Add(staticHost, "add", "Add a static host", hostAddress, hostMac, hostName), async (device, session, token, parse) =>
    await Apply(device, session, await device.Dhcp.PlanStaticAsync(parse.GetValue(hostAddress)!, parse.GetValue(hostMac) ?? "", parse.GetValue(hostName), false, token).ConfigureAwait(false)).ConfigureAwait(false));
BindParse(Add(staticHost, "set", "Update a static host", hostAddress, hostMac, hostName), async (device, session, token, parse) =>
    await Apply(device, session, await device.Dhcp.PlanStaticAsync(parse.GetValue(hostAddress)!, parse.GetValue(hostMac) ?? "", parse.GetValue(hostName), false, token).ConfigureAwait(false)).ConfigureAwait(false));
BindParse(Add(staticHost, "delete", "Delete a static host", hostAddress), async (device, session, token, parse) =>
    await Apply(device, session, await device.Dhcp.PlanStaticAsync(parse.GetValue(hostAddress)!, "", null, true, token).ConfigureAwait(false)).ConfigureAwait(false));
Command bindings = Add(dhcp, "bindings", "Address bindings");
Bind(Add(bindings, "list", "List bindings"), async (device, session, token) =>
{
    TextOutput.Write(Fields(await device.Dhcp.BindingsAsync(token).ConfigureAwait(false)), session.Json);
    return 0;
});

Command jumbo = Add(root, "jumbo-frames", "Jumbo frames");
Argument<string> jumboState = new("state") { Description = "on or off" };
Bind(Add(jumbo, "show", "Show jumbo frames"), async (device, session, token) =>
{
    TextOutput.Write(await device.System.GetJumboAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(jumbo, "set", "Set jumbo frames after the next reset", jumboState), async (device, session, token, parse) =>
    await Apply(device, session, await device.System.PlanJumboAsync(On(parse.GetValue(jumboState)!), token).ConfigureAwait(false)).ConfigureAwait(false));

Command stats = Add(root, "stats", "Statistics");
Option<bool> resetCounters = new("--reset");
BindParse(Add(stats, "rmon", "RMON counters", portName, resetCounters), async (device, session, token, parse) =>
{
    PortName statsPort = PortName.Parse(parse.GetValue(portName)!);
    if (parse.GetValue(resetCounters))
    {
        return await Apply(device, session, await device.Stats.PlanResetAsync(statsPort, token).ConfigureAwait(false)).ConfigureAwait(false);
    }

    TextOutput.Write(await device.Stats.RmonAsync(statsPort, token).ConfigureAwait(false), session.Json);
    return 0;
});
Bind(Add(stats, "cpu", "CPU utilization"), async (device, session, token) =>
{
    TextOutput.Write(await device.Stats.CpuAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});

Command qos = Add(root, "qos", "Quality of service");
Option<string?> qosMode = new("--mode") { Description = "basic, advanced, or disabled" };
Option<string?> trust = new("--trust") { Description = "cos or dscp" };
Bind(Add(qos, "show", "Show global QoS"), async (device, session, token) =>
{
    TextOutput.Write(await device.Qos.GetGlobalAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(qos, "set", "Set global QoS", qosMode, trust), async (device, session, token, parse) =>
    await Apply(device, session, await device.Qos.PlanGlobalAsync(parse.GetValue(qosMode), parse.GetValue(trust), token).ConfigureAwait(false)).ConfigureAwait(false));
Command qosIf = Add(qos, "interface", "Interface QoS");
Option<int> defaultPriority = new("--default-priority") { Required = true };
BindParse(Add(qosIf, "show", "Show interface priority", portName), async (device, session, token, parse) =>
{
    string value = await device.Qos.GetInterfaceAsync(PortName.Parse(parse.GetValue(portName)!), token).ConfigureAwait(false);
    TextOutput.Write(new Dictionary<string, string> { ["port"] = parse.GetValue(portName)!, ["priority"] = value }, session.Json);
    return 0;
});
BindParse(Add(qosIf, "set", "Set interface priority", portName, defaultPriority), async (device, session, token, parse) =>
    await Apply(device, session, await device.Qos.PlanInterfaceAsync(PortName.Parse(parse.GetValue(portName)!), parse.GetValue(defaultPriority), token).ConfigureAwait(false)).ConfigureAwait(false));
Command qosQueue = Add(qos, "queue", "Queue scheduling");
Option<string?> weights = new("--wrr") { Description = "Eight comma-separated weights" };
Option<bool> strict = new("--strict");
Bind(Add(qosQueue, "show", "Show queues"), async (device, session, token) =>
{
    TextOutput.Write(await device.Qos.GetQueuesAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
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

    return await Apply(device, session, await device.Qos.PlanQueuesAsync(parsed, parse.GetValue(strict) ? true : null, token).ConfigureAwait(false)).ConfigureAwait(false);
});
Command cosMap = Add(qos, "cos-map", "CoS to queue");
Argument<int> cosValue = new("cos");
Argument<int> queueValue = new("queue");
Bind(Add(cosMap, "show", "Show the CoS map"), async (device, session, token) =>
{
    TextOutput.Write(await device.Qos.GetCosMapAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(cosMap, "set", "Set one CoS mapping", cosValue, queueValue), async (device, session, token, parse) =>
    await Apply(device, session, await device.Qos.PlanCosMapAsync(parse.GetValue(cosValue), parse.GetValue(queueValue), token).ConfigureAwait(false)).ConfigureAwait(false));
Command dscpMap = Add(qos, "dscp-map", "DSCP to queue");
Argument<int> dscpValue = new("dscp");
Bind(Add(dscpMap, "show", "Show the DSCP map"), async (device, session, token) =>
{
    TextOutput.Write(await device.Qos.GetDscpMapAsync(token).ConfigureAwait(false), session.Json);
    return 0;
});
BindParse(Add(dscpMap, "set", "Set one DSCP mapping", dscpValue, queueValue), async (device, session, token, parse) =>
    await Apply(device, session, await device.Qos.PlanDscpMapAsync(parse.GetValue(dscpValue), parse.GetValue(queueValue), token).ConfigureAwait(false)).ConfigureAwait(false));

Command config = Add(root, "config", "Configuration snapshot");
Option<string?> output = new("-o") { Description = "Output file" };
Argument<string> snapshotFile = new("file") { Description = "Snapshot JSON file" };
BindParse(Add(config, "export", "Export readable configuration", output), async (device, session, token, parse) =>
{
    ConfigSnapshot snapshot = await ConfigSnapshot.CaptureAsync(device.Client, token).ConfigureAwait(false);
    string path = parse.GetValue(output) ?? "pc28xx-config.json";
    await snapshot.SaveAsync(path, token).ConfigureAwait(false);
    Console.WriteLine(path);
    return 0;
});
BindParse(Add(config, "diff", "Show what apply would change", snapshotFile), async (device, session, token, parse) =>
{
    ConfigSnapshot snapshot = await ConfigSnapshot.LoadAsync(parse.GetValue(snapshotFile)!, token).ConfigureAwait(false);
    ChangePlan plan = await snapshot.PlanApplyAsync(device.Client, token).ConfigureAwait(false);
    CliSession dry = new CliSession
    {
        Host = session.Host,
        User = session.User,
        Password = session.Password,
        DryRun = true,
        Json = session.Json
    };
    return await new ChangeExecutor().ExecuteAsync(plan, dry.Execution(Console.WriteLine), token).ConfigureAwait(false);
});
BindParse(Add(config, "apply", "Apply a snapshot", snapshotFile), async (device, session, token, parse) =>
{
    ConfigSnapshot snapshot = await ConfigSnapshot.LoadAsync(parse.GetValue(snapshotFile)!, token).ConfigureAwait(false);
    return await Apply(device, session, await snapshot.PlanApplyAsync(device.Client, token).ConfigureAwait(false)).ConfigureAwait(false);
});

Command raw = Add(root, "raw", "Raw page access");
Argument<string> page = new("page") { Description = "Page name or /config path" };
Option<string?> select = new("--select") { Description = "Selector appended after the page, including the leading ?" };
BindParse(Add(raw, "get", "Dump parsed fields", page, select), async (device, session, token, parse) =>
{
    string path = parse.GetValue(page)! + (parse.GetValue(select) ?? "");
    TextOutput.Write(Fields(await device.Raw.GetAsync(path, token).ConfigureAwait(false)), session.Json);
    return 0;
});
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

    return await Apply(device, session, await device.Raw.PlanSetAsync(parse.GetValue(page)!, mutations, session.IUnderstand, token).ConfigureAwait(false)).ConfigureAwait(false);
});

return await root.Parse(args).InvokeAsync().ConfigureAwait(false);

static bool On(string text)
{
    return text.Trim().ToLowerInvariant() is "on" or "enable" or "true" or "1";
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

        if (field.Name.Contains("password", StringComparison.OrdinalIgnoreCase))
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

async Task<int> RadiusPlan(SwitchDevice device, CliSession session, CancellationToken token, ParseResult parse, bool delete)
{
    return await Apply(device, session, await device.Radius.PlanAsync(
        parse.GetValue(radiusAddress)!,
        parse.GetValue(radiusKey),
        parse.GetValue(authPort),
        parse.GetValue(radiusTimeout),
        parse.GetValue(retries),
        delete,
        token).ConfigureAwait(false)).ConfigureAwait(false);
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
    command.SetAction((parse, token) => CliHost.Run(parse, (device, session, _) => action(device, session, token)));
}

static void BindParse(Command command, Func<SwitchDevice, CliSession, CancellationToken, ParseResult, Task<int>> action)
{
    command.SetAction((parse, token) => CliHost.Run(parse, (device, session, _) => action(device, session, token, parse)));
}

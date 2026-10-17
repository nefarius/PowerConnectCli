# <img src="assets/NSS-128x128.png" align="left" />PowerConnectCli

[![.NET](https://github.com/nefarius/PowerConnectCli/actions/workflows/build.yml/badge.svg)](https://github.com/nefarius/PowerConnectCli/actions/workflows/build.yml)
[![Nuget](https://img.shields.io/nuget/v/Nefarius.PowerConnect.Ews)](https://www.nuget.org/packages/Nefarius.PowerConnect.Ews/)
[![Assisted by Cursor AI](https://img.shields.io/badge/Assisted%20by-Cursor%20AI-8B5CF6?style=flat)](https://cursor.com/)

`pc28xx` configures a Dell PowerConnect 28xx switch through its built-in web UI.

## About

The 2816 has no SSH and no documented API. Everything the browser can change is a form post to the GoAhead web server that ships with the Marvell EWS framework. This repository is a client for that interface:

- `Nefarius.PowerConnect.Ews` parses the pages, encodes port-list bitmaps, and turns a change into a before/after plan that is read back after it is applied.
- `pc28xx` is the command-line front end. Writes are printed and wait for confirmation. `--dry-run` never posts. `--yes` skips the prompt.

The switch speaks plain HTTP. The password and every setting travel in cleartext on the LAN, and the session cookie is bound to the client IP. The web UI only allows a few sessions at once. Each command logs out when it finishes; leave a few seconds between commands so the switch can free the previous session.

## Usage

```text
pc28xx --host 192.168.2.17 --user admin vlan list
pc28xx vlan show 20
pc28xx vlan set 20 --tagged g1 --untagged g5
pc28xx port access g5 --vlan 20
pc28xx port set g16 --description spare --speed 100 --duplex full
pc28xx --dry-run snmp community add lab --access ro --station 192.168.2.1
pc28xx config export -o switch.json
pc28xx config diff switch.json
pc28xx config apply switch.json
```

The password comes from `--password` or the `PC28XX_PASSWORD` environment variable. `--json` prints machine-readable output. `--capture-dir` stores redacted request and response bodies.

`system reset`, `file restore-defaults`, `file download --type firmware|boot`, `secure-mode set unmanaged`, `ip set`, and deleting the user you are logged in as also require `--i-understand`.

### Commands

`vlan`, `vlan port`, `port`, `port access`, `lag`, `lag members`, `green-ethernet`, `storm-control`, `mirror`, `mac-table`, `stp`, `stp port`, `multicast`, `multicast group`, `multicast forward-all`, `igmp-snooping`, `dot1x`, `dot1x hosts`, `dot1x users`, `system`, `ip`, `diag`, `users`, `radius`, `snmp`, `file`, `dhcp-server`, `jumbo-frames`, `stats`, `qos`, `config`, `raw`.

`raw get <page>` and `raw set <page> field=value` replay any page the typed commands do not cover. On this firmware QoS `basic` and `advanced` both enable the global QoS mode; `disabled` turns it off.

## Protocol

Login is `POST /config/log_off_page.htm` with `userName$query` and `password$query`. A successful login redirects to `/home.htm` and sets a `UserId` cookie. Logout is `GET /config/log_out.htm`.

Pages are addressed as `/config/<page>.htm?[Table]Query:key=value[Other]Filter:expr`. Fields use a suffix for their role: `$query` is the row on screen, `$add` creates a row, `$repeat?N` is row N of a table, `$scalar` is global, and `$VT` describes the type and range. Row status `1` modifies, `4` creates, and `6` deletes.

Port lists are hex bitmaps. Bit index equals ifIndex minus one, most significant bit first inside each byte. `g1`–`g16` are ifIndex 1–16. `ch1`–`ch8` are ifIndex 1000–1007. `docs/protocol.md` lists the fields captured from a 2816.

## Build

```text
./build.sh Compile
./build.sh Test
./build.sh PublishLinux
./build.sh PublishWindows
```

Publish profiles under `cli/pc28xx/Properties/PublishProfiles` produce a self-contained single-file binary for `linux-x64` and `win-x64`.

## Tests

The fixtures in `tests/Nefarius.PowerConnect.Ews.Tests/Fixtures` were captured from a live 2816 and then sanitized. Serial number, service tag, MAC addresses, and the password hash were replaced before they were committed.

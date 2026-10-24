# Bring up a wiped PowerConnect 2816

`pc28xx` talks to the switch over HTTP. A wiped 2816 does not answer until it is in Managed mode and VLAN 1 has an address. This page is the serial-console procedure for getting there. It was checked on a PowerConnect 2816 in Managed mode, software 1.0.0.45.

The 2808 has no external serial port. Use the front-panel mode button and the web UI on that model. The console steps below are for the 2816, 2824, and 2848.

## Cable and terminal

The console port is the male DB-9 on the back panel. It is wired as data terminal equipment, so the cable is an RS-232 crossover (null modem) with a female DB-9 at the switch. A straight-through cable stays silent.

Open the serial port in a VT100 terminal emulator:

| Setting | Value |
| --- | --- |
| Speed | 9600 |
| Data bits | 8 |
| Parity | none |
| Stop bits | 1 |
| Flow control | none |
| Emulation | VT100 |

Connect the cable before applying power when you need to watch the boot messages. Press Enter a few times. A switch that is already in Managed mode answers `console>` or `console#`.

## Managed mode

The switch leaves the factory in Unmanaged mode. In that mode it has no management address, the web UI is absent, and the console stays in debug mode. Hold the front-panel Managed Mode button with a paperclip until the Managed Mode LED indicates managed mode. The user guide says to hold the button for up to 7 seconds. Managed mode, Unmanaged mode, and Secure mode all survive a power cycle.

Secure mode keeps the saved configuration and removes the management address. The same button returns the switch to Managed mode.

## Factory management values

In Managed mode, with an empty configuration, Dell documents these defaults:

| Setting | Value |
| --- | --- |
| Address | 192.168.2.1 |
| Mask | 255.255.255.0 |
| User | `admin` |
| Password | none |
| DHCP client | off |

An empty configuration also starts the Easy Setup Wizard on the console. It waits 60 seconds. Press Ctrl+Z to leave it. Answering `N` to the first question keeps the defaults above, and a browser on 192.168.2.1 can log in as `admin` with an empty password. Set the PC to another address in 192.168.2.0/24, such as 192.168.2.10, before opening that page.

The wizard asks for an optional SNMP community, a level-15 user, the management address and mask, and the default gateway. Answer `Y` at the end to store that configuration. The address it configures is the VLAN 1 management address.

## Assign the address from the console

This is the manual path, from user EXEC. `enable` asks for a password only after one has been set. On software 1.0.0.45 the gateway is the next address in `ip address`, which is the form the interactive help offers.

The example below moves the switch to 192.168.2.18/24 and sets the gateway to 192.168.2.1. Use the address, mask, and gateway for your network. A prefix such as `/24` is accepted in place of the dotted mask.

```text
console> enable
console# configure
console(config)# interface vlan 1
console(config-if)# ip address 192.168.2.18 255.255.255.0 192.168.2.1
console(config-if)# end
```

`end` returns to privileged EXEC. Check the result:

```text
console# show ip interface
```

A successful change looks like this:

```text
Gateway IP Address        Activity status       Type
-----------------------   -------------------   --------
192.168.2.1               Active                static

IP Address                I/F        Type
-----------------------   --------   -------
192.168.2.18/24           vlan 1     Static
```

Then test the path. `count` limits the probes:

```text
console# ping 192.168.2.1 count 2
```

From a PC on the same subnet:

```text
ping 192.168.2.18
```

VLAN 1 stays down until at least one member port has link. A ping that fails while `show ip interface` already shows the new address usually means the PC and the switch are not on the same Ethernet segment yet. Plug the PC into any port. Every port is an untagged member of VLAN 1 until you change that.

## Saving the address

On software 1.0.0.45 these commands are rejected with `Source And Destination URL combination is not supported`:

```text
copy running-config startup-config
copy running-config flash://startup-config
copy running-config flash://config
```

Privileged EXEC on this software has no separate save command. Its `copy` command moves a software image, boot file, or configuration to or from TFTP, XMODEM, or `null:`. The 28xx user guide says the running configuration is automatically saved to local storage. This procedure did not power-cycle the switch after the address change.

`copy run start` is the wrong shortcut on other Dell switches: it can create a file named `start` and leave the saved configuration unchanged. It is not a save command here either.

## Continue with the web UI or pc28xx

Open `http://192.168.2.18` and log in as `admin`. The session is plain HTTP.

`pc28xx` refuses to start when the password is empty. Set a password in the Easy Setup Wizard, or later from the web UI, before the first `pc28xx` command. Then:

```text
pc28xx --host 192.168.2.18 --user admin ip show
```

The password comes from `--password` or `PC28XX_PASSWORD`. Changing the address again with `pc28xx ip set` drops the HTTP session, so that command also requires `--i-understand`. Reconnect to the new address afterward.

## Blank or garbled console

- Confirm the terminal is 9600 8N1, with flow control off and VT100 emulation, and that the cable is a null modem.
- Press Enter. The prompt is easy to miss when the switch has already finished booting.
- Garbled characters mean the speed or framing does not match the switch. Set the terminal to 9600 before the next power-on so the boot text is readable.
- A console that only offers debug commands is in Unmanaged or Secure mode. Hold the Managed Mode button until the switch returns to Managed mode and reboots.
- The COM port number belongs to the PC, not the switch. In Windows Device Manager, the USB-serial adapter is under Ports (COM & LPT).

## Recover a switch you cannot log into

This erases the saved configuration. Use it when the address or password is unknown and the web UI cannot be reached. The configuration file name on this platform is `config`. Entering that name is required.

1. Connect the null-modem cable and open the terminal at 9600 8N1 before power is applied.
2. Power-cycle the switch.
3. When the boot text says `Autoboot in 2 seconds`, press Esc within those 2 seconds.
4. At the Startup menu, choose `2` (Erase Flash File).
5. Answer `Y` to `Are you sure (Y/N)?`.
6. At `Write Flash file name (Up to 8 characters, Enter for none.)` type `config` and press Enter.
7. The Easy Setup Wizard starts. Answer `N` to keep the factory address, or complete the wizard with the address you want.
8. The switch is still Unmanaged after this erase, including when the wizard was completed. Hold the Managed Mode button until it reboots into Managed mode.

The factory login is then `admin` with no password, at 192.168.2.1/24 if the wizard was skipped. Continue with [Assign the address from the console](#assign-the-address-from-the-console) or the web UI.

`restore-defaults` from `console#` also returns the device to the factory configuration. It is available only after you already have a Managed-mode console session.

## References

- [Dell PowerConnect 2816 User's Guide](https://dl.dell.com/manuals/all-products/esuprt_ser_stor_net/esuprt_networking/esuprt_net_fxd_prt_swtchs/powerconnect-2816_user's%20guide_en-us.pdf): console wiring, management modes, the setup wizard, and the CLI command reference.
- [How to reset a PowerConnect 2800 series switch to factory default](https://www.dell.com/support/kbdoc/en-au/000121429/how-to-reset-a-powerconnect-2800-series-switch-to-factory-default): the Startup-menu erase procedure.
- [Basic Command Line Interface on Dell Networking PowerConnect 2800 model switches](https://www.dell.com/support/kbdoc/en-uk/000120225/basic-command-line-interface-on-dell-networking-powerconnect-2800-model-switches): what the 28xx console can and cannot do.

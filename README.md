# Unofficial C# SDK for ZR04RN CCTV

SDK for direct LAN access to CCTV DVR or NVR devices.

## Supported Devices

This SDK has been tested on the following devices:
* Zosi H.264 PoE Network Video Recorder
  * Serial: ZR04RN (also referred to as 1NR-04RN00-EU, ZR04RN00, or ZR04FRN)
  * Firmware: V1.2.1.200420

It will also work for similar devices by other manufacturers that use the same firmware and protocol. (Uniden Guardian G6880D2, G6840D1, G6440D1, ...)

No official support is provided.

## Protocol

On connection, the DVR will send a 64 byte packet starting with the ASCII marker "head".

Each subsequent packet from the DVR and client application will start with an 8 byte header with the ASCII marker "AAAA" followed by the 32-bit length of the command which follows.

A command starts with a 32-bit integer specifying the command type, followed by a 32-bit request identifier. Responses are sent with the same identifier as the request, to match responses with their respective requests.

If the command has data, the header also contains a 32-bit value specifying the version (usually 0xA) of the command, and a 32-bit integer specifying the length of the data. The data length plus the 16-byte header size should match the command length specified by the packet.

## Network Behaviour

The CCTV advertises itself using SSDP, specifying location http://x.x.x.x:49152/upnp_eth0.xml or http://x.x.x.x:49153/upnp_eth0.xml. The contents in the SSDP advertisement seem mostly bogus, crashing the CCTV when access is attempted. It provides web access over port 80 using outdated and non-working ActiveX plugins, and direct LAN access over port 5000. Port 5800 appears to be open as well. There are also outgoing connections for cloud support, which ideally should be blocked. Port 80 and 5800 should be blocked entirely.

Access over port 5000 is fully unencrypted, and should not be exposed to the internet, usernames and passwords are sent in plaintext. Preferably, internet access may be tunneled using SSH through a separate device.

Sending a bogus packet length to the CCTV direct LAN access will crash it. The device will automatically reboot after crashing.

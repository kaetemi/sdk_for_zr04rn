# Unofficial C# SDK for ZR04RN CCTV

SDK for direct LAN access to CCTV DVR or NVR devices.

## Supported Devices

This SDK has been tested on the following devices:
* Zosi H.264 PoE Network Video Recorder
  * Serial: ZR04RN (also referred to as 1NR-04RN00-EU, ZR04RN00, or ZR04FRN)
  * Firmware: V1.2.1.200420

It will also work for similar devices by other manufacturers that use the same firmware and protocol.

No official support is provided.

## Protocol

On connection, the DVR will send a 64 byte packet starting with the ASCII marker "head".

Each subsequent packet from the DVR and client application will start with an 8 byte header with the ASCII marker "AAAA" followed by the 32-bit length of the command which follows.

A command starts with a 32-bit integer specifying the command type, followed by a 32-bit request identifier. Responses are sent with the same identifier as the request, to match responses with their respective requests.

If the command has data, the header also contains a 32-bit value specifying the version (usually 0xA) of the command, and a 32-bit integer specifying the length of the data. The data length plus the 16-byte header size should match the command length specified by the packet.

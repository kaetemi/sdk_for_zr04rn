using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Diagnostics;

namespace ApiForZR04RN
{
    public delegate void CommandCallback(CommandType cmdType, uint cmdId, uint cmdVer, byte[] data);

    struct CommandData
    {
        public CommandType Type;
        public uint Id;
        public uint Version;
        public byte[] Data;
    }

    class RawDeviceConnection
    {
        TcpClient client;
        bool abortListen = false;
        Task listenTask;

        static byte[] blankPackBuffer = new byte[0];
        byte[] packBuffer = blankPackBuffer;
        int packSize = 0;

        public event Action Connected;
        public event CommandCallback CommandReceived;
        public event Action Disconnected;

        public SequentialScheduler Scheduler { get; private set; }

        public RawDeviceConnection(SequentialScheduler scheduler)
        {
            Scheduler = scheduler;
        }

        ~RawDeviceConnection()
        {
            Disconnect();
        }

        public async Task Connect(string address, int port)
        {
            Disconnect();
            client = new TcpClient();
            abortListen = false;
            Debug.Assert(TaskScheduler.Current == Scheduler);
            await client.ConnectAsync(address, port);
            Debug.Assert(TaskScheduler.Current == Scheduler);
            listenTask = Listen(client);
            Debug.Assert(TaskScheduler.Current == Scheduler);
            if (Connected != null)
                Connected();
        }

        public void Disconnect()
        {
            Debug.Assert(TaskScheduler.Current == Scheduler);
            if (client != null)
            {
                abortListen = true;
                try
                {
                    client.Close();
                    client.Dispose();
                }
                catch { }
                client = null;

                listenTask = null;
            }
        }

        public const int MagicMarkerAAAA = 0x41414141;
        public const int MagicMarkerHEAD = 0x64616568;
        public const int MagicMarkerPACK = 0x4B434150;

        private async Task Listen(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            Debug.Assert(TaskScheduler.Current == Scheduler);
            try
            {
                byte[] buffer = new byte[256 * 1024];
                int i = 0;
                bool mustRead = true;
                int maxFetch = 4096;
                while (!abortListen && client == this.client)
                {
                    Debug.Assert(TaskScheduler.Current == Scheduler);
                    int len;
                    if (mustRead)
                    {
                        Debug.Assert(TaskScheduler.Current == Scheduler);
                        len = await stream.ReadAsync(buffer, i, Math.Min(buffer.Length - i, maxFetch));
                        Debug.Assert(TaskScheduler.Current == Scheduler);
                        maxFetch = 4096;
                        if (len <= 0)
                            break;
                    }
                    else
                    {
                        len = 0;
                    }
                    i += len;
                    if (i < 8)
                        continue;

                    // We now have at least 8 bytes of a packet
                    int magicMarker = buffer[0] | buffer[1] << 8 | buffer[2] << 16 | buffer[3] << 24;
                    int packetLen = buffer[4] | buffer[5] << 8 | buffer[6] << 16 | buffer[7] << 24;

                    if (packetLen > buffer.Length + 8)
                    {
                        // Console.WriteLine("Oversize packet {0} from {1}", packetLen, stream);
                        // Console.WriteLine();
                    }

                    switch (magicMarker)
                    {
                        case MagicMarkerAAAA:
                            if (i < (packetLen + 8))
                            {
                                mustRead = true;
                                maxFetch = (packetLen + 8) - i;
                                continue;
                            }
                            // Console.WriteLine("Received packet with length {0} from {1}", packetLen, fromName);

                            if (packetLen >= (4 * 4)) // (8 + (4 * 4)))
                            {
                                // cmdtype, cmdid, cmdver, datalen
                                // Decode command
                                int vi = 8;
                                uint cmdType = buffer[vi] | (uint)buffer[vi + 1] << 8 | (uint)buffer[vi + 2] << 16 | (uint)buffer[vi + 3] << 24;
                                vi += 4; // cmdType
                                uint cmdId = buffer[vi] | (uint)buffer[vi + 1] << 8 | (uint)buffer[vi + 2] << 16 | (uint)buffer[vi + 3] << 24;
                                vi += 4; // cmdId
                                uint cmdVer = buffer[vi] | (uint)buffer[vi + 1] << 8 | (uint)buffer[vi + 2] << 16 | (uint)buffer[vi + 3] << 24;
                                vi += 4; // cmdVer
                                int dataLen = buffer[vi] | buffer[vi + 1] << 8 | buffer[vi + 2] << 16 | buffer[vi + 3] << 24;
                                vi += 4; // dataLen
                                // Console.WriteLine("Command 0x{0}, id 0x{1}, version 0x{2}, with length {3}",
                                //     Convert.ToString(cmdType, 16),
                                //     Convert.ToString(cmdId, 16),
                                //     Convert.ToString(cmdVer, 16), dataLen);
                                if (dataLen == MagicMarkerAAAA)
                                {
                                    // Console.WriteLine("Abort packet, magic AAAA found as length", dataLen);
                                    // Console.WriteLine();
                                    for (int j = (vi - 4); j < i; ++j)
                                        buffer[j - (vi - 4)] = buffer[j];
                                    i -= (vi - 4);
                                    mustRead = (i == 0);
                                    maxFetch = 4096;
                                    break;
                                }
                                if ((packetLen + 8) >= (vi + dataLen))
                                {
                                    byte[] data = buffer.SubArray(vi, dataLen);
                                    if (CommandReceived != null)
                                        CommandReceived((CommandType)cmdType, cmdId, cmdVer, data);
                                }
                                else
                                {
                                    if (dataLen > (8 * 1024 * 1024))
                                    {
                                        // Console.WriteLine("Packet data too large at {0} bytes", dataLen);
                                    }
                                    else if (dataLen < 16)
                                    {
                                        // Console.WriteLine("Packet data too small at {0} bytes", dataLen);
                                    }
                                    else if (packetLen < (vi + 12))
                                    {
                                        // Console.WriteLine("Packet too small at {0} bytes", packetLen);
                                    }
                                    else if (cmdType == MagicMarkerPACK)
                                    {
                                        vi += 12;
                                        int packLoad = (packetLen + 8 - vi);
                                        // Console.WriteLine("PACK {0} {1} load {2}", cmdId, cmdVer, packLoad);
                                        if (packBuffer.Length != dataLen)
                                        {
                                            if (packSize != 0)
                                            {
                                                // Console.WriteLine("Lost packed command at {0} out of {1} bytes", packSize, packBuffer.Length);
                                            }
                                            packBuffer = new byte[dataLen];
                                            packSize = 0;
                                        }
                                        int packOverflow = (packSize + packLoad) - packBuffer.Length;
                                        if (packOverflow > 0)
                                        {
                                            // Console.WriteLine("Packed command overflow by {0} bytes", packOverflow);
                                            packBuffer = new byte[0];
                                            packSize = 0;
                                        }
                                        else
                                        {
                                            byte[] data = buffer.SubArray(vi, packLoad);
                                            data.CopyTo(packBuffer, packSize);
                                            packSize += packLoad;
                                            if (packSize == dataLen)
                                            {
                                                // Console.WriteLine("PACK COMPLETE");
                                                vi = 0;
                                                cmdType = packBuffer[vi] | (uint)packBuffer[vi + 1] << 8 | (uint)packBuffer[vi + 2] << 16 | (uint)packBuffer[vi + 3] << 24;
                                                vi += 4; // cmdType
                                                cmdId = packBuffer[vi] | (uint)packBuffer[vi + 1] << 8 | (uint)packBuffer[vi + 2] << 16 | (uint)packBuffer[vi + 3] << 24;
                                                vi += 4; // cmdId
                                                cmdVer = packBuffer[vi] | (uint)packBuffer[vi + 1] << 8 | (uint)packBuffer[vi + 2] << 16 | (uint)packBuffer[vi + 3] << 24;
                                                vi += 4; // cmdVer
                                                dataLen = packBuffer[vi] | packBuffer[vi + 1] << 8 | packBuffer[vi + 2] << 16 | packBuffer[vi + 3] << 24;
                                                vi += 4; // dataLen
                                                // Console.WriteLine("Packed command 0x{0}, id 0x{1}, version 0x{2}, with length {3}",
                                                //     Convert.ToString(cmdType, 16),
                                                //     Convert.ToString(cmdId, 16),
                                                //     Convert.ToString(cmdVer, 16), dataLen);
                                                if ((packBuffer.Length) >= (vi + dataLen))
                                                {
                                                    byte[] packData = packBuffer.SubArray(vi, dataLen);
                                                    if (CommandReceived != null)
                                                        CommandReceived((CommandType)cmdType, cmdId, cmdVer, packData);
                                                }
                                                else
                                                {
                                                    // Console.WriteLine("Packed command length exceeds packet by {0} bytes", (vi + dataLen) - (packetLen + 8));
                                                }
                                                packBuffer = blankPackBuffer;
                                                packSize = 0;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Console.WriteLine("Command length exceeds packet by {0} bytes", (vi + dataLen) - (packetLen + 8));
                                        // hackerPrint(buffer, 0, (packetLen + 8));
                                    }
                                }
                                if ((packetLen + 8) > (vi + dataLen))
                                {
                                    // Console.WriteLine("Packet length exceeds command by {0} bytes", (packetLen + 8) - (vi + dataLen));
                                }
                            }
                            else if (packetLen == 16)
                            {
                                int vi = 8;
                                uint cmdType = buffer[vi] | (uint)buffer[vi + 1] << 8 | (uint)buffer[vi + 2] << 16 | (uint)buffer[vi + 3] << 24;
                                vi += 4; // cmdType
                                uint cmdId = buffer[vi] | (uint)buffer[vi + 1] << 8 | (uint)buffer[vi + 2] << 16 | (uint)buffer[vi + 3] << 24;
                                vi += 4; // command?
                                // Console.WriteLine("Short command 0x{0}: {1} ({2})", Convert.ToString(cmdType, 16), BitConverter.ToString(buffer, vi - 4, 4), cmdId);
                                if (CommandReceived != null)
                                    CommandReceived((CommandType)cmdType, cmdId, 0, null);
                            }

                            // Console.WriteLine();
                            // await toStream.WriteAsync(buffer, 0, packetLen + 8);
                            for (int j = (packetLen + 8); j < i; ++j)
                                buffer[j - (packetLen + 8)] = buffer[j];
                            i -= (packetLen + 8);
                            mustRead = (i == 0);
                            break;
                        case MagicMarkerHEAD:
                            if (i < 64)
                            {
                                mustRead = true;
                                maxFetch = 64 - i;
                                continue;
                            }
                            // Console.WriteLine("Received head marker from {0}", fromName);
                            // Console.WriteLine();
                            // await toStream.WriteAsync(buffer, 0, 64);
                            for (int j = 64; j < i; ++j)
                                buffer[j - 64] = buffer[j];
                            i -= 64;
                            mustRead = (i == 0);
                            break;
                        default:
                            // Console.WriteLine("Unknown marker {0}", magicMarker);
                            // Console.WriteLine();
                            return;
                    }

                    // await toStream.WriteAsync(buffer, 0, i);
                    // i = 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            if (Disconnected != null)
                Disconnected();
            Debug.Assert(TaskScheduler.Current == Scheduler);
        }

        public async Task SendCommand(CommandType cmdType, uint cmdId, uint cmdVer, byte[] data)
        {
            Debug.Assert(TaskScheduler.Current == Scheduler);
            if (data != null)
            {
                byte[] packet = new byte[24 + data.Length];
                uint cmdLen = (uint)data.Length;
                uint packetLen = (uint)data.Length + 16;
                packet[0] = (byte)'A';
                packet[1] = (byte)'A';
                packet[2] = (byte)'A';
                packet[3] = (byte)'A';
                packet[4] = (byte)(packetLen & 0xFF);
                packet[5] = (byte)((packetLen >> 8) & 0xFF);
                packet[6] = (byte)((packetLen >> 16) & 0xFF);
                packet[7] = (byte)((packetLen >> 24) & 0xFF);
                packet[8] = (byte)((uint)cmdType & 0xFF);
                packet[9] = (byte)(((uint)cmdType >> 8) & 0xFF);
                packet[10] = (byte)(((uint)cmdType >> 16) & 0xFF);
                packet[11] = (byte)(((uint)cmdType >> 24) & 0xFF);
                packet[12] = (byte)(cmdId & 0xFF);
                packet[13] = (byte)((cmdId >> 8) & 0xFF);
                packet[14] = (byte)((cmdId >> 16) & 0xFF);
                packet[15] = (byte)((cmdId >> 24) & 0xFF);
                packet[16] = (byte)(cmdVer & 0xFF);
                packet[17] = (byte)((cmdVer >> 8) & 0xFF);
                packet[18] = (byte)((cmdVer >> 16) & 0xFF);
                packet[19] = (byte)((cmdVer >> 24) & 0xFF);
                packet[20] = (byte)(cmdLen & 0xFF);
                packet[21] = (byte)((cmdLen >> 8) & 0xFF);
                packet[22] = (byte)((cmdLen >> 16) & 0xFF);
                packet[23] = (byte)((cmdLen >> 24) & 0xFF);
                for (int i = 0; i < data.Length; ++i)
                    packet[24 + i] = data[i];
                await client.GetStream().WriteAsync(packet, 0, packet.Length);
                Debug.Assert(TaskScheduler.Current == Scheduler);
            }
        }
    }
}

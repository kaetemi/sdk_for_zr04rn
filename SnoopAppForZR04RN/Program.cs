using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text;
using System.IO;

namespace SnoopAppForZR04RN
{
    static class Program
    {
        static TcpListener listener5000;
        static TcpListener listener80;
        static string target = "192.168.226.180";
        static bool running = true;

        static byte[] blankPackBuffer = new byte[0];
        static byte[] packBuffer = blankPackBuffer;
        static int packSize = 0;

        public static T[] SubArray<T>(this T[] data, int index, int length)
        {
            T[] result = new T[length];
            Array.Copy(data, index, result, 0, length);
            return result;
        }

        public static string NullTerminate(this string data)
        {
            int i = data.IndexOf('\0');
            if (i >= 0)
                return data.Substring(0, i);
            return data;
        }

        public static string Passwordize(this string data)
        {
            return new string('*', data.Length);
        }

        static async Task Main(string[] args)
        {
            listener5000 = new TcpListener(IPAddress.Any, 5000);
            listener5000.Start();
            listener80 = new TcpListener(IPAddress.Any, 80);
            listener80.Start();
            Task a = listen5000();
            Task b = listen80();
            Console.ReadKey();
            running = false;
            listener5000.Stop();
            listener80.Stop();
            await a;
            await b;
        }
        static async Task listen5000()
        {
            while (running)
            {
                Console.WriteLine("Wait for 5000");
                Console.WriteLine();
                try
                {
                    TcpClient client = await listener5000.AcceptTcpClientAsync();
                    TcpClient nvr = new TcpClient(target, 5000);
                    Console.WriteLine("Have 5000");
                    Console.WriteLine();
                    Task a = forwardZR04RN(client, nvr, "Client");
                    Task b = forwardZR04RN(nvr, client, "NVR");
                    await a;
                    await b;
                    try { client.Close(); } catch { }
                    try { nvr.Close(); } catch { }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        static async Task listen80()
        {
            while (running)
            {
                Console.WriteLine("Wait for 80");
                Console.WriteLine();
                try
                {
                    TcpClient client = await listener80.AcceptTcpClientAsync();
                    TcpClient nvr = new TcpClient(target, 80);
                    Console.WriteLine("Have 80");
                    Console.WriteLine();
                    Task a = forwardRaw(client, nvr);
                    Task b = forwardRaw(nvr, client);
                    await a;
                    await b;
                    try { client.Close(); } catch { }
                    try { nvr.Close(); } catch { }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        static async Task forwardRaw(TcpClient from, TcpClient to)
        {
            NetworkStream fromStream = from.GetStream();
            NetworkStream toStream = to.GetStream();
            try
            {
                byte[] buffer = new byte[64 * 1024];
                for (; ; )
                {
                    int len = await fromStream.ReadAsync(buffer, 0, buffer.Length);
                    if (len <= 0)
                        break;
                    await toStream.WriteAsync(buffer, 0, len);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        static void hackerPrint(byte[] buffer, int offset, int size)
        {
            if (buffer == null)
            {
                Console.WriteLine("NULL");
                return;
            }
            int lines = (size + 15) / 16;
            for (int y = 0; y < lines; ++y)
            {
                for (int x = 0; x < 16; ++x)
                {
                    if (((x) % 4) == 0)
                        Console.Write(" ");
                    int i = (y * 16) + x;
                    if (i < size)
                    {
                        Console.Write(" {0}", BitConverter.ToString(new byte[1] { buffer[offset + i] }).ToUpper());
                    }
                    else
                    {
                        Console.Write("   ");
                    }
                }
                Console.Write(" ");
                for (int x = 0; x < 16; ++x)
                {
                    if (((x) % 4) == 0)
                        Console.Write(" ");
                    int i = (y * 16) + x;
                    if (i < size)
                    {
                        char c = (char)buffer[offset + i];
                        if ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                            Console.Write(c);
                        else
                            Console.Write('.');
                    }
                    else
                    {
                        Console.Write(" ");
                    }
                }
                Console.WriteLine();
            }
        }

        const int MagicMarkerAAAA = 0x41414141;
        const int MagicMarkerHEAD = 0x64616568;
        const int MagicMarkerPACK = 0x4B434150;

        static async Task forwardZR04RN(TcpClient from, TcpClient to, string fromName)
        {
            NetworkStream fromStream = from.GetStream();
            NetworkStream toStream = to.GetStream();
            try
            {
                byte[] buffer = new byte[256 * 1024];
                int i = 0;
                bool mustRead = true;
                int maxFetch = 4096;
                for (; ; )
                {
                    int len;
                    if (mustRead)
                    {
                        len = await fromStream.ReadAsync(buffer, i, Math.Min(buffer.Length - i, maxFetch));
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
                        Console.WriteLine("Oversize packet {0} from {1}", packetLen, fromName);
                        hackerPrint(buffer, 0, len);
                        Console.WriteLine();
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
                            Console.WriteLine("Received packet with length {0} from {1}", packetLen, fromName);

                            if (packetLen >= (4 * 4)) // (8 + (4 * 4)))
                            {
                                // cmdtype, cmdid, cmdver, datalen
                                // Decode command
                                int vi = 8;
                                int cmdType = buffer[vi] | buffer[vi + 1] << 8 | buffer[vi + 2] << 16 | buffer[vi + 3] << 24;
                                vi += 4; // cmdType
                                int cmdId = buffer[vi] | buffer[vi + 1] << 8 | buffer[vi + 2] << 16 | buffer[vi + 3] << 24;
                                vi += 4; // cmdId
                                int cmdVer = buffer[vi] | buffer[vi + 1] << 8 | buffer[vi + 2] << 16 | buffer[vi + 3] << 24;
                                vi += 4; // cmdVer
                                int dataLen = buffer[vi] | buffer[vi + 1] << 8 | buffer[vi + 2] << 16 | buffer[vi + 3] << 24;
                                vi += 4; // dataLen
                                Console.WriteLine("Command 0x{0}, id 0x{1}, version 0x{2}, with length {3}",
                                    Convert.ToString(cmdType, 16),
                                    Convert.ToString(cmdId, 16),
                                    Convert.ToString(cmdVer, 16), dataLen);
                                if (dataLen == MagicMarkerAAAA)
                                {
                                    Console.WriteLine("Abort packet, magic AAAA found as length", dataLen);
                                    Console.WriteLine();
                                    await toStream.WriteAsync(buffer, 0, vi - 4);
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
                                    parseCommand(cmdType, data);
                                }
                                else
                                {
                                    if (dataLen > (8 * 1024 * 1024))
                                    {
                                        Console.WriteLine("Packet data too large at {0} bytes", dataLen);
                                    }
                                    else if (dataLen < 16)
                                    {
                                        Console.WriteLine("Packet data too small at {0} bytes", dataLen);
                                    }
                                    else if (packetLen < (vi + 12))
                                    {
                                        Console.WriteLine("Packet too small at {0} bytes", packetLen);
                                    }
                                    else if (cmdType == MagicMarkerPACK)
                                    {
                                        vi += 12;
                                        int packLoad = (packetLen + 8 - vi);
                                        Console.WriteLine("PACK {0} {1} load {2}", cmdId, cmdVer, packLoad);
                                        if (packBuffer.Length != dataLen)
                                        {
                                            if (packSize != 0)
                                            {
                                                Console.WriteLine("Lost packed command at {0} out of {1} bytes", packSize, packBuffer.Length);
                                            }
                                            packBuffer = new byte[dataLen];
                                            packSize = 0;
                                        }
                                        int packOverflow = (packSize + packLoad) - packBuffer.Length;
                                        if (packOverflow > 0)
                                        {
                                            Console.WriteLine("Packed command overflow by {0} bytes", packOverflow);
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
                                                Console.WriteLine("PACK COMPLETE");
                                                // hackerPrint(packBuffer, 0, packBuffer.Length);
                                                vi = 0;
                                                cmdType = packBuffer[vi] | packBuffer[vi + 1] << 8 | packBuffer[vi + 2] << 16 | packBuffer[vi + 3] << 24;
                                                vi += 4; // cmdType
                                                cmdId = packBuffer[vi] | packBuffer[vi + 1] << 8 | packBuffer[vi + 2] << 16 | packBuffer[vi + 3] << 24;
                                                vi += 4; // cmdId
                                                cmdVer = packBuffer[vi] | packBuffer[vi + 1] << 8 | packBuffer[vi + 2] << 16 | packBuffer[vi + 3] << 24;
                                                vi += 4; // cmdVer
                                                dataLen = packBuffer[vi] | packBuffer[vi + 1] << 8 | packBuffer[vi + 2] << 16 | packBuffer[vi + 3] << 24;
                                                vi += 4; // dataLen
                                                Console.WriteLine("Packed command 0x{0}, id 0x{1}, version 0x{2}, with length {3}",
                                                    Convert.ToString(cmdType, 16),
                                                    Convert.ToString(cmdId, 16),
                                                    Convert.ToString(cmdVer, 16), dataLen);
                                                if ((packBuffer.Length) >= (vi + dataLen))
                                                {
                                                    byte[] packData = packBuffer.SubArray(vi, dataLen);
                                                    parseCommand(cmdType, packData);
                                                }
                                                else
                                                {
                                                    Console.WriteLine("Packed command length exceeds packet by {0} bytes", (vi + dataLen) - (packetLen + 8));
                                                }
                                                packBuffer = blankPackBuffer;
                                                packSize = 0;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Command length exceeds packet by {0} bytes", (vi + dataLen) - (packetLen + 8));
                                        // hackerPrint(buffer, 0, (packetLen + 8));
                                    }
                                }
                                if ((packetLen + 8) > (vi + dataLen))
                                {
                                    Console.WriteLine("Packet length exceeds command by {0} bytes", (packetLen + 8) - (vi + dataLen));
                                }
                            }
                            else if (packetLen == 16)
                            {
                                int vi = 8;
                                int cmdType = buffer[vi] | buffer[vi + 1] << 8 | buffer[vi + 2] << 16 | buffer[vi + 3] << 24;
                                vi += 4; // cmdType
                                int command = buffer[vi] | buffer[vi + 1] << 8 | buffer[vi + 2] << 16 | buffer[vi + 3] << 24;
                                vi += 4; // command?
                                Console.WriteLine("Short command 0x{0}: {1} ({2})", Convert.ToString(cmdType, 16), BitConverter.ToString(buffer, vi - 4, 4), command);
                                parseCommand(cmdType, null);
                            }

                            Console.WriteLine();
                            await toStream.WriteAsync(buffer, 0, packetLen + 8);
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
                            Console.WriteLine("Received head marker from {0}", fromName);
                            Console.WriteLine();
                            await toStream.WriteAsync(buffer, 0, 64);
                            for (int j = 64; j < i; ++j)
                                buffer[j - 64] = buffer[j];
                            i -= 64;
                            mustRead = (i == 0);
                            break;
                        default:
                            Console.WriteLine("Unknown marker {0}", magicMarker);
                            Console.WriteLine();
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
        }

        static void parseCommand(int cmdType, byte[] data)
        {
            int vi;
            switch (cmdType)
            {
                case 0x1101:
                    Console.WriteLine("DVRV3_LOGIN");
                    if (data == null)
                        goto MissingCommandData;
                    if (data.Length != 120)
                        goto UnknownCommandLength;
                    vi = 0;
                    Console.WriteLine("ConnectType: {0}", data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24);
                    Console.WriteLine("IP: {0}.{0}.{0}.{0}", data[4], data[5], data[6], data[7]);
                    Console.WriteLine("Username: {0}", Encoding.ASCII.GetString(data, 8, 36).NullTerminate()); // 8-44, 32 with enforced 00 on last 4 bytes
                    Console.WriteLine("Password: {0}", Encoding.ASCII.GetString(data, 44, 36).NullTerminate().Passwordize()); // 44-80, 32 with enforced 00 on last 4 bytes
                    Console.WriteLine("ComputerName: {0}", Encoding.ASCII.GetString(data, 80, 28).NullTerminate());
                    Console.WriteLine("Mac: {0}", BitConverter.ToString(data, 108, 6));
                    Console.WriteLine("Reserved (NULL): {0}", BitConverter.ToString(data, 114, 2));
                    vi = 116;
                    Console.WriteLine("NetProtocolVer: {0}", Convert.ToString(data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24, 16));
                    break;
                case 0x10001:
                    Console.WriteLine("DVRV3_LOGIN_SUCCESS");
                    if (data == null)
                        goto MissingCommandData;
                    if (data.Length != 352 && data.Length != 348)
                        goto UnknownCommandLength;
                    vi = 0;
                    Console.WriteLine("Unknown: {0}", data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24);
                    vi = data.Length == 348 ? 0 : 4;
                    Console.WriteLine("Authority: {0}", BitConverter.ToString(data, vi + 0, 4));
                    Console.WriteLine("AuthLiveCH: {0}", BitConverter.ToString(data, vi + 4, 8));
                    Console.WriteLine("AuthRecordCH: {0}", BitConverter.ToString(data, vi + 12, 8));
                    Console.WriteLine("AuthPlaybackCH: {0}", BitConverter.ToString(data, vi + 20, 8));
                    Console.WriteLine("AuthBackupCH: {0}", BitConverter.ToString(data, vi + 28, 8));
                    Console.WriteLine("AuthPTZCtrlCH: {0}", BitConverter.ToString(data, vi + 36, 8));
                    Console.WriteLine("AuthRemoteViewCH: {0}", BitConverter.ToString(data, vi + 44, 8));
                    Console.WriteLine("Unknown: {0}", BitConverter.ToString(data, vi + 52, 28));
                    Console.WriteLine("VideoInputNum: {0}", data[vi + 80] | data[vi + 81] << 8);
                    Console.WriteLine("DeviceID: {0}", data[vi + 82] | data[vi + 83] << 8);
                    Console.WriteLine("VideoFormat: {0}", BitConverter.ToString(data, vi + 84, 4));
                    for (int i = 0; i < 8; ++i)
                        Console.WriteLine("Function[{0}]: {1}", i, BitConverter.ToString(data, vi + 88 + (4 * i), 4));
                    Console.WriteLine("IP: {0}.{1}.{2}.{3}", data[vi + 120], data[vi + 121], data[vi + 122], data[vi + 123]);
                    Console.WriteLine("Mac: {0}", BitConverter.ToString(data, vi + 124, 6));
                    Console.WriteLine("Reserved (NULL): {0}", BitConverter.ToString(data, vi + 130, 2));
                    // Console.WriteLine("BuildDate: {0}", BitConverter.ToString(command, 136, 4));
                    vi = data.Length == 348 ? 132 : 136;
                    int buildDate = data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24;
                    Console.WriteLine("BuildDate: {0}-{1}-{2}",
                        (buildDate >> 16).ToString("0000"), ((buildDate >> 8) & 0xFF).ToString("00"), (buildDate & 0xFF).ToString("00"));
                    // Console.WriteLine("BuildTime: {0}", BitConverter.ToString(command, 140, 4));
                    vi = data.Length == 348 ? 136 : 140;
                    int buildTime = data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24;
                    Console.WriteLine("BuildTime: {0}:{1}:{2}",
                        ((buildTime >> 16) & 0xFF).ToString("00"), ((buildTime >> 8) & 0xFF).ToString("00"), (buildTime & 0xFF).ToString("00"));
                    Console.WriteLine("DeviceName: {0}", Encoding.ASCII.GetString(data, vi - 140 + 144, 36).NullTerminate());
                    Console.WriteLine("FirmwareVersion: {0}", Encoding.ASCII.GetString(data, vi - 140 + 180, 36).NullTerminate());
                    Console.WriteLine("KernelVersion: {0}", Encoding.ASCII.GetString(data, vi - 140 + 216, 64).NullTerminate());
                    Console.WriteLine("HardwareVersion: {0}", Encoding.ASCII.GetString(data, vi - 140 + 280, 36).NullTerminate());
                    Console.WriteLine("McuVersion: {0}", Encoding.ASCII.GetString(data, vi - 140 + 316, 36).NullTerminate());
                    break;
                case 0x10002:
                    Console.WriteLine("DVRV3_LOGIN_FAILED");
                    Console.WriteLine("Unknown: {0}", BitConverter.ToString(data, 0, 4));
                    break;
                case 0x1401:
                    Console.WriteLine("DVRV3_REQUEST_CFG_ENTER");
                    if (data != null)
                        goto UnknownCommandData;
                    break;
                case 0x1402:
                    Console.WriteLine("DVRV3_REQUEST_CFG_EXIT");
                    if (data != null)
                        goto UnknownCommandData;
                    break;
                case 0x1403:
                    Console.WriteLine("DVRV3_REQUEST_CFG_GET");
                    if (data == null)
                        goto MissingCommandData;
                    // hackerPrint(data, 0, data == null ? 0 : data.Length);
                    break;
                case 0x1404:
                    Console.WriteLine("DVRV3_REQUEST_CFG_SET");
                    if (data == null)
                        goto MissingCommandData;
                    hackerPrint(data, 0, data == null ? 0 : data.Length);
                    break;
                case 0x1405:
                    Console.WriteLine("DVRV3_REQUEST_CFG_DEF_DATA");
                    if (data == null)
                        goto MissingCommandData;
                    // hackerPrint(data, 0, data == null ? 0 : data.Length);
                    break;
                /*
                DVRV3_REQUEST_CFG_DEFAULT
                DVRV3_REQUEST_CFG_MODIFY_PASS
                DVRV3_REQUEST_CFG_NET
                DVRV3_REQUEST_CFG_IMPORT
                DVRV3_REQUEST_CFG_EXPORT
                 */
                case 0x40001:
                    Console.WriteLine("DVRV3_CONFIG_SUCCESS");
                    if (data != null)
                        goto UnknownCommandData;
                    break;
                case 0x40002:
                    Console.WriteLine("DVRV3_CONFIG_DATA");
                    if (data == null)
                        goto MissingCommandData;
                    // hackerPrint(data, 0, data == null ? 0 : data.Length);
                    break;
                case 0x9000001:
                    Console.WriteLine("DVRV3_REPLY_VIDEO_LOSS");
                    if (data == null)
                        goto MissingCommandData;
                    // hackerPrint(data, 0, data == null ? 0 : data.Length);
                    break;
                case 0x9000002:
                    Console.WriteLine("DVRV3_REPLY_MOTION");
                    if (data == null)
                        goto MissingCommandData;
                    // hackerPrint(data, 0, data == null ? 0 : data.Length);
                    break;
                case 0x9000003:
                    Console.WriteLine("DVRV3_REPLY_SENSOR");
                    if (data == null)
                        goto MissingCommandData;
                    // hackerPrint(data, 0, data == null ? 0 : data.Length);
                    break;
                case 0x9000004:
                    Console.WriteLine("DVRV3_REPLY_REC_STATUS");
                    if (data == null)
                        goto MissingCommandData;
                    // hackerPrint(data, 0, data == null ? 0 : data.Length);
                    break;
                case 0x9000008:
                    Console.WriteLine("DVRV3_REPLY_CHNN_NAME");
                    if (data == null)
                        goto MissingCommandData;
                    // hackerPrint(data, 0, data == null ? 0 : data.Length);
                    break;
                case 0x1201:
                    Console.WriteLine("DVRV3_REQUEST_STREAM_START");
                    if (data == null)
                        goto MissingCommandData;
                    hackerPrint(data, 0, data == null ? 0 : data.Length);
                    break;
                case 0x1202:
                    Console.WriteLine("DVRV3_REQUEST_STREAM_CHANGE");
                    if (data == null)
                        goto MissingCommandData;
                    hackerPrint(data, 0, data == null ? 0 : data.Length);
                    break;
                case 0x1203:
                    Console.WriteLine("DVRV3_REQUEST_STREAM_STOP");
                    if (data == null)
                        goto MissingCommandData;
                    hackerPrint(data, 0, data == null ? 0 : data.Length);
                    break;
                case 0x1204:
                    Console.WriteLine("DVRV3_REQUEST_KEYFRAME");
                    if (data == null)
                        goto MissingCommandData;
                    hackerPrint(data, 0, data == null ? 0 : data.Length);
                    break;
                case 0xa000001:
                    Console.WriteLine("DVRV3_REPLY_DATA_STREAM");
                    if (data == null)
                        goto MissingCommandData;
                    if (data.Length >= 60)
                    {
                        // frametype 3 is info
                        /*
                        vi = 0;
                        Console.WriteLine("KeyFrame: {0}", data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24);
                        vi = 4;
                        Console.WriteLine("FrameType: {0}", data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24);
                        vi = 8;
                        Console.WriteLine("Length: {0}", data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24);
                        vi = 12;
                        Console.WriteLine("Width: {0}", data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24);
                        vi = 16;
                        Console.WriteLine("Height: {0}", data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24);
                        vi = 20;
                        Console.WriteLine("LData: {0}", data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24);
                        vi = 24;
                        Console.WriteLine("Channel: {0}", data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24);
                        vi = 28;
                        Console.WriteLine("BufIndex: {0}", data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24);
                        vi = 32;
                        Console.WriteLine("FrameIndex: {0}", data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24);
                        vi = 36;
                        Console.WriteLine("FrameAttrib: {0}", data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24);
                        vi = 40;
                        Console.WriteLine("StreamId: {0}", data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24);
                        vi = 44;
                        Console.WriteLine("Time: {0}", data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24
                             | data[vi + 4] << 32 | data[vi + 5] << 40 | data[vi + 6] << 48 | data[vi + 7] << 56);
                        vi = 52;
                        Console.WriteLine("RelativeTime: {0}", data[vi] | data[vi + 1] << 8 | data[vi + 2] << 16 | data[vi + 3] << 24
                             | data[vi + 4] << 32 | data[vi + 5] << 40 | data[vi + 6] << 48 | data[vi + 7] << 56);
                        hackerPrint(data, 60, Math.Min(data == null ? 0 : data.Length - 60, 64));
                        */
                    }
                    else
                    {
                        hackerPrint(data, 0, Math.Min(data == null ? 0 : data.Length, 128));
                    }
                    break;
                default:
                    Console.WriteLine("Unknown command type");
                    // hackerPrint(data, 0, data == null ? 0 : data.Length);
                    break;
            }
            return;
        UnknownCommandLength:
            Console.WriteLine("Unknown command length");
            return;
        UnknownCommandData:
            Console.WriteLine("Unknown command data");
            return;
        MissingCommandData:
            Console.WriteLine("Missing command data");
            return;
        }

        /*
        static void accept5000(Task<TcpClient> client)
        {
            listener5000.AcceptTcpClientAsync().ContinueWith(accept5000);
            TcpClient real = new TcpClient(target, 5000);
        }

        static void accept80(Task<TcpClient> client)
        {
            listener80.AcceptTcpClientAsync().ContinueWith(accept80);
            TcpClient real = new TcpClient(target, 80);

        }
        */
    }
}

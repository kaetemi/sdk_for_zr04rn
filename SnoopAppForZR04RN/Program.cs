using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.IO;

namespace SnoopAppForZR04RN
{
    class Program
    {
        static TcpListener listener5000;
        static TcpListener listener80;
        static string target = "192.168.226.180";
        static bool running = true;

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

        const int MagicMarkerAAAA = 0x41414141;
        const int MagicMarkerHEAD = 0x64616568;

        static async Task forwardZR04RN(TcpClient from, TcpClient to, string fromName)
        {
            NetworkStream fromStream = from.GetStream();
            NetworkStream toStream = to.GetStream();
            try
            {
                byte[] buffer = new byte[64 * 1024];
                int i = 0;
                bool mustRead = true;
                for (; ; )
                {
                    int len;
                    if (mustRead)
                    {
                        len = await fromStream.ReadAsync(buffer, i, buffer.Length - i);
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
                        Console.WriteLine();
                    }

                    switch (magicMarker)
                    {
                        case MagicMarkerAAAA:
                            if (i < (packetLen + 8))
                            {
                                mustRead = true;
                                continue;
                            }
                            Console.WriteLine("Received packet with length {0} from {1}", packetLen, fromName);

                            if (packetLen >= (8 + (4 * 4)))
                            {
                                // Decode message
                                int vi = 8;
                                int messageId = buffer[vi] | buffer[vi + 1] << 8 | buffer[vi + 2] << 16 | buffer[vi + 3] << 24;
                                vi += 4; // messageId
                                vi += 4; // 0x00 unknown
                                vi += 4; // 0x0a unknown
                                int messageLen = buffer[vi] | buffer[vi + 1] << 8 | buffer[vi + 2] << 16 | buffer[vi + 3] << 24;
                                vi += 4; // messageLen
                                Console.WriteLine("Message {0} with length {1}, unknown {2}, unknown {3}",
                                    Convert.ToString(messageId, 16), messageLen,
                                    BitConverter.ToString(buffer, vi - 12, 4), BitConverter.ToString(buffer, vi - 8, 4));
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

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.IO;
using ApiForZR04RN;
using System.Threading;

namespace CmdDemoForZR04RN
{
    class Program
    {
        static DeviceConnection connection;
        static uint streamId;
        static FileStream fs;
        static SequentialScheduler scheduler = new SequentialScheduler();

        static async Task Main(string[] args)
        {
            Console.WriteLine("CmdDemo");
            Task task = await Task.Factory.StartNew(() => mainTask(), CancellationToken.None, TaskCreationOptions.None, scheduler);
            Console.WriteLine("Waiting");
            try
            {
                await task;
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine(ex.Message);
            }
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
            scheduler.Dispose();
        }

        static async Task mainTask()
        {
            // Run this together with SnoopApp for diagnostics
            connection = new DeviceConnection(scheduler);
            connection.UnknownCommandReceived += Connection_UnknownCommandReceived;
            await connection.Connect("127.0.0.1", 5000);
            Console.WriteLine("Connected");
            Console.Write("Password: ");
            string password = Console.ReadLine();
            LoginSuccess loginSuccess = await connection.Login("admin", password);
            Console.WriteLine("Logged in");
            Console.WriteLine("Device name: {0}", loginSuccess.ProductInfo.DeviceName);
            Console.WriteLine("Firmware version: {0}", loginSuccess.ProductInfo.FirmwareVersion);
            StreamFrame keyframe = await connection.SnapKeyframe(0);
            Console.WriteLine("Keyframe received");
            Console.WriteLine("Width: {0}", keyframe.Width);
            Console.WriteLine("Height: {0}", keyframe.Height);
            // File.WriteAllBytes("C:\\temp\\keyframe.h264", keyframe.Data);

            /*
            connection.StreamFrameReceived += Connection_StreamFrameReceived;
            fs = new FileStream("C:\\temp\\channel0a.h264", FileMode.Create, FileAccess.Write, FileShare.Read);
            streamId = await connection.StreamStart(0);
            */

            // Console.ReadKey();
            // await (new TaskCompletionSource<bool>().Task);
        }

        private static void Connection_UnknownCommandReceived(CommandType cmdType, uint cmdId, uint cmdVer, byte[] data)
        {
            Console.WriteLine("Unknown command received");
        }

        private static void Connection_StreamFrameReceived(StreamFrame frame)
        {
            if (frame.StreamId == streamId && frame.FrameType == FrameType.Video)
            {
                // Console.WriteLine("Frame received");
                // Console.WriteLine("Attrib: 0x{0}", Convert.ToString((uint)frame.FrameAttrib, 16));
                fs.Write(frame.Data, 0, frame.Data.Length);
                if (frame.KeyFrame)
                {
                    Console.WriteLine("Keyframe received");
                    connection.StreamChange(streamId); // Should send this to KeepAlive every few seconds, so anytime a keyframe is received works!
                }
            }
            else
            {
                Console.WriteLine("Non-frame received");
            }
        }
    }
}

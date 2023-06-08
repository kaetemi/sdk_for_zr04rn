using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ApiForZR04RN;

namespace RtspCameraExample
{
    static class Program
    {
        static SequentialScheduler scheduler = new SequentialScheduler();
        static RtspServer server;

        static async Task Main(string[] args) // 8554 192.168.1.2 admin 123
        {
            Task task = await Task.Factory.StartNew(() => mainTask(args), CancellationToken.None, TaskCreationOptions.None, scheduler);

            Console.WriteLine("Waiting");
            try
            {
                await task;
            }
            catch (TaskCanceledException ex)
            {
                Console.WriteLine(ex.Message);
            }

            Console.WriteLine("Press ENTER to exit");
            string readline = null;
            while (readline == null)
            {
                readline = Console.ReadLine();

                // Avoid maxing out CPU on systems that instantly return null for ReadLine
                if (readline == null) Thread.Sleep(500);
            }

            await Task.Factory.StartNew(() => { if (server != null) { server.StopListen(); } }, CancellationToken.None, TaskCreationOptions.None, scheduler);
            scheduler.Dispose();

        }
        static async Task mainTask(string[] args)
        {
            Debug.Assert(TaskScheduler.Current == scheduler);
            int port = int.Parse(args[0]); // 8554;
            string username = null; // "user";      // or use NUL if there is no username
            string password = null; //"password";  // or use NUL if there is no password

            server = new RtspServer(scheduler, port, username, password, args[1], int.Parse(args[2]), int.Parse(args[3]), args[4], args[5]);
            Task listen = await Task.Factory.StartNew(() => server.ListenAsync(), CancellationToken.None, TaskCreationOptions.None, scheduler);

            // Wait for user to terminate programme
            string msg = "Connect RTSP client to Port=" + port;
            if (username != null && password != null)
            {
                msg += " Username=" + username + " Password=" + password;
            }
            Debug.Assert(TaskScheduler.Current == scheduler);
            Console.WriteLine(msg);
            Debug.Assert(TaskScheduler.Current == scheduler);

            await listen;
        }
    }
}

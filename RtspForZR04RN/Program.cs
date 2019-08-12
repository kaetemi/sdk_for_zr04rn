using System;
using System.Threading;
using System.Threading.Tasks;

namespace RtspCameraExample
{
    static class Program
    {
        static async Task Main(string[] args) // 8554 192.168.1.2 admin 123
        {
            int port = int.Parse(args[0]); // 8554;
            string username = null; // "user";      // or use NUL if there is no username
            string password = null; //"password";  // or use NUL if there is no password

            RtspServer s = new RtspServer(port, username, password, args[1], int.Parse(args[2]), int.Parse(args[3]), args[4], args[5]);
            try
            {
                await s.ListenAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return;
            }

            // Wait for user to terminate programme
            string msg = "Connect RTSP client to Port=" + port;
            if (username != null && password != null)
            {
                msg += " Username=" + username + " Password=" + password;
            }
            Console.WriteLine(msg);
            Console.WriteLine("Press ENTER to exit");
            string readline = null;
            while (readline == null)
            {
                readline = Console.ReadLine();

                // Avoid maxing out CPU on systems that instantly return null for ReadLine
                if (readline == null) Thread.Sleep(500);
            }

            s.StopListen();

        }
    }
}

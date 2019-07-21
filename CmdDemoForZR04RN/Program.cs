using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.IO;
using ApiForZR04RN;

namespace CmdDemoForZR04RN
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Run this together with SnoopApp for diagnostics
            DeviceConnection connection = new DeviceConnection();
            await connection.Connect("127.0.0.1", 5000);
            Console.WriteLine("Connected");
            Console.Write("Password: ");
            string password = Console.ReadLine();
            LoginSuccess loginSuccess = await connection.Login("admin", password);
            Console.WriteLine("Logged in");
            Console.WriteLine("Device name: {0}", loginSuccess.ProductInfo.DeviceName);
            Console.WriteLine("Firmware version: {0}", loginSuccess.ProductInfo.FirmwareVersion);
        }
    }
}

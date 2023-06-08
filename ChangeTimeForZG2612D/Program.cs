using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.IO;
using ApiForZR04RN;
using System.Threading;

namespace SnapshotForZR04RN
{
    class Program
    {
        static SequentialScheduler scheduler = new SequentialScheduler();
        static DeviceConnection connection;

        /// <summary>
        /// dotnet ChangeTimeForZG2612D.dll ipaddr 8000
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static async Task Main(string[] args)
        {
            Task task = await Task.Factory.StartNew(() => mainTask(args), CancellationToken.None, TaskCreationOptions.None, scheduler);
            await task;
            scheduler.Dispose();
        }

        static async Task mainTask(string[] args)
        {
            connection = new DeviceConnection(scheduler);
            await connection.Connect(args[0], int.Parse(args[1]));
            LoginSuccess loginSuccess = await connection.Login("admin", "admin");
            await connection.ChangeTime((uint)DateTimeOffset.Now.ToUnixTimeSeconds());
            await connection.Logout();
            connection.Disconnect();
        }
    }
}

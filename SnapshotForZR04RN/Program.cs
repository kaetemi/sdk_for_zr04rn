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
        /// dotnet SnapshotForZR04RN.dll ipaddr 5000 admin passwd 0 | ffmpeg -i - -vframes 1 -f image2pipe -vcodec mjpeg -q 3 - > snapshot.jpg
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
            int channel = args.Length > 4 ? int.Parse(args[4]) : 0;
            int sub = args.Length > 5 ? int.Parse(args[5]) : 0;
            connection = new DeviceConnection(scheduler);
            await connection.Connect(args[0], int.Parse(args[1]));
            LoginSuccess loginSuccess = await connection.Login(args[2], args[3]);
            StreamFrame keyframe = await connection.SnapKeyframe(channel, sub);
            Stream stdout = Console.OpenStandardOutput();
            await stdout.WriteAsync(keyframe.Data, 0, keyframe.Data.Length);
            connection.Disconnect();
            await stdout.FlushAsync();
            stdout.Close();
        }
    }
}

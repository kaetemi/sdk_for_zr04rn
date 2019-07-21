using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ApiForZR04RN
{
    class StructuredDeviceConnection
    {
        RawDeviceConnection connection;

        public event Action Connected;
        public event CommandCallback CommandReceived;
        public event Action Disconnected;

        uint lastCmdId;

        struct Pending
        {
            public TaskCompletionSource<CommandData> Response;
            public CommandType[] CmdTypes;
        }

        Dictionary<uint, Pending> pendingId;

        public StructuredDeviceConnection()
        {
            lastCmdId = 0;
            pendingId = new Dictionary<uint, Pending>();
            connection = new RawDeviceConnection();
            connection.CommandReceived += Connection_CommandReceived;
            connection.Connected += Connection_Connected;
            connection.Disconnected += Connection_Disconnected;
        }

        ~StructuredDeviceConnection()
        {
            connection.Disconnect();
            connection.CommandReceived -= Connection_CommandReceived;
            connection.Connected -= Connection_Connected;
            connection.Disconnected -= Connection_Disconnected;
            foreach (Pending pending in pendingId.Values)
                pending.Response.SetException(new Exception("Disposed"));
            pendingId.Clear();
        }

        private void Connection_Connected()
        {
            if (Connected != null)
                Connected();
        }

        private void Connection_CommandReceived(CommandType cmdType, uint cmdId, uint cmdVer, byte[] data)
        {
            if (pendingId.ContainsKey(cmdId))
            {
                TaskCompletionSource<CommandData> response = pendingId[cmdId].Response;
                pendingId.Remove(cmdId);
                CommandData cmd;
                cmd.Type = cmdType;
                cmd.Id = cmdId;
                cmd.Version = cmdVer;
                cmd.Data = data;
                response.SetResult(cmd);
                return;
            }
            foreach (KeyValuePair<uint, Pending> kvp in pendingId)
            {
                if (kvp.Value.CmdTypes != null)
                {
                    for (int i = 0; i < kvp.Value.CmdTypes.Length; ++i)
                    {
                        if (kvp.Value.CmdTypes[i] == cmdType)
                        {
                            pendingId.Remove(kvp.Key);
                            CommandData cmd;
                            cmd.Type = cmdType;
                            cmd.Id = cmdId;
                            cmd.Version = cmdVer;
                            cmd.Data = data;
                            kvp.Value.Response.SetResult(cmd);
                            return;
                        }
                    }
                }
            }
            if (CommandReceived != null)
                CommandReceived(cmdType, cmdId, cmdVer, data);
        }

        private void Connection_Disconnected()
        {
            foreach (Pending pending in pendingId.Values)
                pending.Response.SetException(new Exception("Disconnected"));
            pendingId.Clear();
            if (Disconnected != null)
                Disconnected();
        }

        public async Task SendCommand(CommandType cmdType, uint cmdVer, byte[] data)
        {
            await connection.SendCommand(cmdType, 0xFFFFFFFF, cmdVer, data);
        }

        public async Task<CommandData> SendRequest(CommandType cmdType, uint cmdVer, byte[] data)
        {
            return await SendRequest(cmdType, cmdVer, data, null);
        }

        public async Task<CommandData> SendRequest(CommandType cmdType, uint cmdVer, byte[] data, CommandType[] responseTypes)
        {
            Pending pending;
            pending.Response = new TaskCompletionSource<CommandData>();
            pending.CmdTypes = responseTypes;
            uint cmdId = ++lastCmdId;
            while (cmdId == 0 || cmdId == 0xFFFFFFFF)
                cmdId = ++lastCmdId;
            pendingId.Add(cmdId, pending);
            await connection.SendCommand(cmdType, cmdId, cmdVer, data);
            return await pending.Response.Task;
        }

        public async Task Connect(string address, int port)
        {
            await connection.Connect(address, port);
        }

        public void Disconnect()
        {
            connection.Disconnect();
        }
    }
}

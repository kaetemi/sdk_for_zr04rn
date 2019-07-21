using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ApiForZR04RN
{
    public class DeviceConnection
    {
        StructuredDeviceConnection connection;

        public event Action Connected;
        public event CommandCallback UnknownCommandReceived;
        public event Action Disconnected;

        public event Action<StreamFrame> StreamFrameReceived;

        uint lastStreamId = 0;
        Dictionary<uint, TaskCompletionSource<StreamFrame>> pendingKeyframe;
        Dictionary<uint, int> streamChannel;

        public DeviceConnection()
        {
            pendingKeyframe = new Dictionary<uint, TaskCompletionSource<StreamFrame>>();
            streamChannel = new Dictionary<uint, int>();
            connection = new StructuredDeviceConnection();
            connection.Connected += Connection_Connected;
            connection.CommandReceived += Connection_CommandReceived;
            connection.Disconnected += Connection_Disconnected;
        }

        ~DeviceConnection()
        {
            foreach (TaskCompletionSource<StreamFrame> pending in pendingKeyframe.Values)
                pending.SetException(new Exception("Disposed"));
            pendingKeyframe.Clear();
            connection.Disconnect();
            connection.CommandReceived -= Connection_CommandReceived;
            connection.Connected -= Connection_Connected;
            connection.Disconnected -= Connection_Disconnected;
        }

        public async Task Connect(string address, int port)
        {
            await connection.Connect(address, port);
        }

        public void Disconnect()
        {
            foreach (TaskCompletionSource<StreamFrame> pending in pendingKeyframe.Values)
                pending.SetException(new Exception("Disconnected"));
            pendingKeyframe.Clear();
            connection.Disconnect();
        }

        private void Connection_Connected()
        {
            if (Connected != null)
                Connected();
        }

        private void Connection_CommandReceived(CommandType cmdType, uint cmdId, uint cmdVer, byte[] data)
        {
            int vi;
            switch (cmdType)
            {
                case CommandType.ReplyDataStream:
                    {
                        StreamFrame streamFrame;
                        vi = 0;
                        streamFrame.KeyFrame = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                        vi = 4;
                        streamFrame.FrameType = (FrameType)(data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24);
                        vi = 8;
                        streamFrame.Length = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                        vi = 12;
                        streamFrame.Width = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                        vi = 16;
                        streamFrame.Height = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                        vi = 20;
                        streamFrame.LData = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                        vi = 24;
                        streamFrame.Channel = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                        vi = 28;
                        streamFrame.BufIndex = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                        vi = 32;
                        streamFrame.FrameIndex = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                        vi = 36;
                        streamFrame.FrameAttrib = (FrameAttrib)(data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24);
                        vi = 40;
                        streamFrame.StreamId = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                        vi = 44;
                        streamFrame.Time = (long)(data[vi] | (ulong)data[vi + 1] << 8 | (ulong)data[vi + 2] << 16 | (ulong)data[vi + 3] << 24
                             | (ulong)data[vi + 3] << 32 | (ulong)data[vi + 3] << 40 | (ulong)data[vi + 3] << 48 | (ulong)data[vi + 3] << 56);
                        vi = 52;
                        streamFrame.RelativeTime = (long)(data[vi] | (ulong)data[vi + 1] << 8 | (ulong)data[vi + 2] << 16 | (ulong)data[vi + 3] << 24
                             | (ulong)data[vi + 3] << 32 | (ulong)data[vi + 3] << 40 | (ulong)data[vi + 3] << 48 | (ulong)data[vi + 3] << 56);
                        streamFrame.Data = data.SubArray(60, (int)Math.Min(streamFrame.Length, data.Length - 60));
                        if (pendingKeyframe.ContainsKey(streamFrame.StreamId))
                        {
                            if (streamFrame.FrameType == FrameType.Video && streamFrame.KeyFrame != 0)
                            {
                                StreamStop(streamFrame.StreamId);
                                TaskCompletionSource<StreamFrame> response = pendingKeyframe[streamFrame.StreamId];
                                pendingKeyframe.Remove(streamFrame.StreamId);
                                response.SetResult(streamFrame);
                            }
                        }
                        else
                        {
                            if (StreamFrameReceived != null)
                                StreamFrameReceived(streamFrame);
                        }
                    }
                    break;
                default:
                    if (UnknownCommandReceived != null)
                        UnknownCommandReceived(cmdType, cmdId, cmdVer, data);
                    break;
            }
        }

        private void Connection_Disconnected()
        {
            if (Disconnected != null)
                Disconnected();
        }

        public async Task<LoginSuccess> Login(string username, string password)
        {
            byte[] usernameBytes = Encoding.ASCII.GetBytes(username);
            byte[] passwordBytes = Encoding.ASCII.GetBytes(password);
            byte[] loginData = new byte[120];
            loginData[0] = 1; // uint ConnectType;
            // uint IP;
            for (int i = 0; i < usernameBytes.Length && i < 32; ++i)
                loginData[8 + i] = usernameBytes[i];
            // uint null;
            for (int i = 0; i < passwordBytes.Length && i < 32; ++i)
                loginData[44 + i] = passwordBytes[i];
            // uint null;
            // char[24] ComputerName;
            // uint null;
            // byte[6] MAC;
            // byte[2] null;
            loginData[116] = 10; // uint NetProtocolVer;

            CommandData cmd = await connection.SendRequest(CommandType.RequestLogin, 10, loginData, new CommandType[] { CommandType.ReplyLoginSuccess, CommandType.ReplyLoginFail });
            byte[] data = cmd.Data;
            int vi;
            switch (cmd.Type)
            {
                case CommandType.ReplyLoginSuccess:
                    LoginSuccess success;
                    // unknown 32 bits
                    vi = 4;
                    success.Authority = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                    vi += 4;
                    success.AuthLiveChannels = data[vi] | (ulong)data[vi + 1] << 8 | (ulong)data[vi + 2] << 16 | (ulong)data[vi + 3] << 24
                         | (ulong)data[vi + 3] << 32 | (ulong)data[vi + 3] << 40 | (ulong)data[vi + 3] << 48 | (ulong)data[vi + 3] << 56;
                    vi += 8;
                    success.AuthRecordChannels = data[vi] | (ulong)data[vi + 1] << 8 | (ulong)data[vi + 2] << 16 | (ulong)data[vi + 3] << 24
                         | (ulong)data[vi + 3] << 32 | (ulong)data[vi + 3] << 40 | (ulong)data[vi + 3] << 48 | (ulong)data[vi + 3] << 56;
                    vi += 8;
                    success.AuthPlaybackChannels = data[vi] | (ulong)data[vi + 1] << 8 | (ulong)data[vi + 2] << 16 | (ulong)data[vi + 3] << 24
                         | (ulong)data[vi + 3] << 32 | (ulong)data[vi + 3] << 40 | (ulong)data[vi + 3] << 48 | (ulong)data[vi + 3] << 56;
                    vi += 8;
                    success.AuthBackupChannels = data[vi] | (ulong)data[vi + 1] << 8 | (ulong)data[vi + 2] << 16 | (ulong)data[vi + 3] << 24
                         | (ulong)data[vi + 3] << 32 | (ulong)data[vi + 3] << 40 | (ulong)data[vi + 3] << 48 | (ulong)data[vi + 3] << 56;
                    vi += 8;
                    success.AuthPTZCtrlChannels = data[vi] | (ulong)data[vi + 1] << 8 | (ulong)data[vi + 2] << 16 | (ulong)data[vi + 3] << 24
                         | (ulong)data[vi + 3] << 32 | (ulong)data[vi + 3] << 40 | (ulong)data[vi + 3] << 48 | (ulong)data[vi + 3] << 56;
                    vi += 8;
                    success.AuthRemoteViewChannels = data[vi] | (ulong)data[vi + 1] << 8 | (ulong)data[vi + 2] << 16 | (ulong)data[vi + 3] << 24
                         | (ulong)data[vi + 3] << 32 | (ulong)data[vi + 3] << 40 | (ulong)data[vi + 3] << 48 | (ulong)data[vi + 3] << 56;
                    vi += 8;
                    success.ProductInfo.LocalVideoInputNum = data[vi++];
                    success.ProductInfo.AudioInputNum = data[vi++];
                    success.ProductInfo.SensorInputNum = data[vi++];
                    success.ProductInfo.RelayOutputNum = data[vi++];
                    success.ProductInfo.DisplayResolutionMask = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                    vi += 4;
                    success.ProductInfo.VideoOuputNum = data[vi++];
                    success.ProductInfo.NetVideoOutputNum = data[vi++];
                    success.ProductInfo.NetVideoInputNum = data[vi++];
                    success.ProductInfo.IVSNum = data[vi++];
                    success.ProductInfo.PresetNumOneChannel = data[vi++];
                    success.ProductInfo.CruiseNumOneChannel = data[vi++];
                    success.ProductInfo.PresetNumOneCruise = data[vi++];
                    success.ProductInfo.TrackNumOneChanel = data[vi++];
                    success.ProductInfo.UserNum = data[vi++];
                    success.ProductInfo.NetClientNum = data[vi++];
                    success.ProductInfo.NetFirstStreamNum = data[vi++];
                    success.ProductInfo.DeviceType = data[vi++];
                    success.ProductInfo.DoblueStream = data[vi++];
                    success.ProductInfo.AudioStream = data[vi++];
                    success.ProductInfo.TalkAudio = data[vi++];
                    success.ProductInfo.PasswordCheck = data[vi++];
                    success.ProductInfo.DefBrightness = data[vi++];
                    success.ProductInfo.DefContrast = data[vi++];
                    success.ProductInfo.DefSaturation = data[vi++];
                    success.ProductInfo.DefHue = data[vi++];
                    success.ProductInfo.VideoInputNum = (ushort)(data[vi] | (uint)data[vi + 1] << 8);
                    vi += 2;
                    success.ProductInfo.DeviceId = (ushort)(data[vi] | (uint)data[vi + 1] << 8);
                    vi += 2;
                    success.ProductInfo.VideoFormat = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                    vi += 4;
                    success.ProductInfo.Function0 = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                    vi += 4;
                    success.ProductInfo.Function1 = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                    vi += 4;
                    success.ProductInfo.Function2 = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                    vi += 4;
                    success.ProductInfo.Function3 = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                    vi += 4;
                    success.ProductInfo.Function4 = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                    vi += 4;
                    success.ProductInfo.Function5 = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                    vi += 4;
                    success.ProductInfo.Function6 = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                    vi += 4;
                    success.ProductInfo.Function7 = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                    vi += 4;
                    success.ProductInfo.DeviceIP = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                    vi += 4;
                    success.ProductInfo.DeviceMAC = new byte[6];
                    for (int i = 0; i < 6; ++i)
                        success.ProductInfo.DeviceMAC[i] = data[vi++];
                    vi += 2; // Reserved
                    success.ProductInfo.BuildDate = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                    vi += 4;
                    success.ProductInfo.BuildTime = data[vi] | (uint)data[vi + 1] << 8 | (uint)data[vi + 2] << 16 | (uint)data[vi + 3] << 24;
                    vi += 4;
                    success.ProductInfo.DeviceName = Encoding.ASCII.GetString(data, vi, 36).NullTerminate();
                    vi += 36;
                    success.ProductInfo.FirmwareVersion = Encoding.ASCII.GetString(data, vi, 36).NullTerminate();
                    vi += 36;
                    success.ProductInfo.KernelVersion = Encoding.ASCII.GetString(data, vi, 64).NullTerminate();
                    vi += 64;
                    success.ProductInfo.HardwareVersion = Encoding.ASCII.GetString(data, vi, 36).NullTerminate();
                    vi += 36;
                    success.ProductInfo.MCUVersion = Encoding.ASCII.GetString(data, vi, 36).NullTerminate();
                    // vi += 36;
                    return success;
                case CommandType.ReplyLoginFail:
                    throw new LoginFail();
                default:
                    throw new Exception("Unknown command type");
            }
        }

        public async Task<StreamFrame> SnapKeyframe(int channel)
        {
            TaskCompletionSource<StreamFrame> response = new TaskCompletionSource<StreamFrame>();
            uint streamId = ++lastStreamId;
            pendingKeyframe[streamId] = response;
            byte[] request = new byte[36];
            // uint StreamID;
            request[0] = (byte)(streamId & 0xFF);
            request[1] = (byte)((streamId >> 8) & 0xFF);
            request[2] = (byte)((streamId >> 16) & 0xFF);
            request[3] = (byte)((streamId >> 24) & 0xFF);
            // ulong MasterVideoChannelBits;
            request[4] = (byte)(1 << channel);
            // ulong SubVideoChannelBits;
            // ulong ThirdVideoChannelBits;
            // ulong AudioChannelBits;
            request[28] = request[4];
            await connection.SendCommand(CommandType.RequestStreamStart, 10, request);
            return await response.Task;
        }

        public async Task<uint> StreamStart(int channel) // returns streamid
        {
            uint streamId = ++lastStreamId;
            byte[] request = new byte[36];
            streamChannel[streamId] = channel;
            // uint StreamID;
            request[0] = (byte)(streamId & 0xFF);
            request[1] = (byte)((streamId >> 8) & 0xFF);
            request[2] = (byte)((streamId >> 16) & 0xFF);
            request[3] = (byte)((streamId >> 24) & 0xFF);
            // ulong MasterVideoChannelBits;
            request[4] = (byte)(1 << channel);
            // ulong SubVideoChannelBits;
            // ulong ThirdVideoChannelBits;
            // ulong AudioChannelBits;
            request[28] = request[4];
            await connection.SendCommand(CommandType.RequestStreamStart, 10, request);
            return streamId;
        }

        public async Task StreamChange(uint streamId)
        {
            byte[] request = new byte[36];
            request[0] = (byte)(streamId & 0xFF);
            request[1] = (byte)((streamId >> 8) & 0xFF);
            request[2] = (byte)((streamId >> 16) & 0xFF);
            request[3] = (byte)((streamId >> 24) & 0xFF);
            request[4] = (byte)(1 << streamChannel[streamId]);
            request[28] = request[4];
            await connection.SendCommand(CommandType.RequestStreamChange, 10, request);
        }

        public async Task StreamStop(uint streamId)
        {
            byte[] request = new byte[36];
            request[0] = (byte)(streamId & 0xFF);
            request[1] = (byte)((streamId >> 8) & 0xFF);
            request[2] = (byte)((streamId >> 16) & 0xFF);
            request[3] = (byte)((streamId >> 24) & 0xFF);
            if (streamChannel.ContainsKey(streamId))
                streamChannel.Remove(streamId);
            await connection.SendCommand(CommandType.RequestStreamStop, 10, request);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace ApiForZR04RN
{
    public struct ProductInfo
    {
        public byte LocalVideoInputNum;
        public byte AudioInputNum;
        public byte SensorInputNum;
        public byte RelayOutputNum;
        public uint DisplayResolutionMask;
        public byte VideoOuputNum;
        public byte NetVideoOutputNum;
        public byte NetVideoInputNum;
        public byte IVSNum;
        public byte PresetNumOneChannel;
        public byte CruiseNumOneChannel;
        public byte PresetNumOneCruise;
        public byte TrackNumOneChanel;
        public byte UserNum;
        public byte NetClientNum;
        public byte NetFirstStreamNum;
        public byte DeviceType;
        public byte DoblueStream;
        public byte AudioStream;
        public byte TalkAudio;
        public byte PasswordCheck;
        public byte DefBrightness;
        public byte DefContrast;
        public byte DefSaturation;
        public byte DefHue;
        public ushort VideoInputNum;
        public ushort DeviceId;
        public uint VideoFormat;
        public uint Function0;
        public uint Function1;
        public uint Function2;
        public uint Function3;
        public uint Function4;
        public uint Function5;
        public uint Function6;
        public uint Function7;
        public uint DeviceIP;
        public byte[] DeviceMAC; // 6 bytes
        //  public ushort Reserved;
        public uint BuildDate;
        public uint BuildTime;
        public string DeviceName; // 36 bytes
        public string FirmwareVersion; // 36 bytes
        public string KernelVersion; // 64 bytes
        public string HardwareVersion; // 36 bytes
        public string MCUVersion; // 36 bytes
    }

    public struct LoginSuccess
    {
        public uint Authority;
        public ulong AuthLiveChannels;
        public ulong AuthRecordChannels;
        public ulong AuthPlaybackChannels;
        public ulong AuthBackupChannels;
        public ulong AuthPTZCtrlChannels;
        public ulong AuthRemoteViewChannels;
        public ProductInfo ProductInfo;
    }

    public class LoginFail : Exception
    {
        public LoginFail() : base("Login failed")
        {
        }
    }
}

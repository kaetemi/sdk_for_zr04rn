using System;
using System.Collections.Generic;
using System.Text;

namespace ApiForZR04RN
{
    struct StreamFrameInfo
    {
        public uint KeyFrame;
        public uint FrameType;
        public uint Length;
        public uint Width;
        public uint Height;
        public uint LData;
        public uint Channel;
        public uint BufIndex;
        public uint FrameIndex;
        public uint FrameAttrib;
        public uint StreamId;
        public long Time;
        public long RelativeTime;
        public byte[] Data;
    }
}

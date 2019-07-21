using System;
using System.Collections.Generic;
using System.Text;

namespace ApiForZR04RN
{
    public enum FrameType : uint
    {
        None = 0,
        Video,
        Audio,
        VideoFormat, // 3, data in BITMAPINFOHEADER format
        AudioFormat,
        Event,
        Text,
        TalkAudio,
        TalkAudioFormat,
        End,
        FileHead,
        FileInfo,
        Jpeg,
    }

    public enum FrameAttrib : uint
    {
        PlayFrameNoShow = 0x01,
        PlayFrameShow = 0x02,
        PlayFrameAllEnd = 0x04,
        PlayFrameSecEnd = 0x08,
        PlayFrameTimeStamp = 0x10,
        LiveFrameFirstStream = 0x20,
        LiveFrameSecondStream = 0x40,
        PlayFrameFF = 0x80,
        LiveFrameJpeg = 0x100,
        LiveFrameTalk = 0x200,
    }

    public struct StreamFrame
    {
        public uint KeyFrame;
        public FrameType FrameType;
        public uint Length;
        public uint Width;
        public uint Height;
        public uint LData;
        public uint Channel;
        public uint BufIndex;
        public uint FrameIndex;
        public FrameAttrib FrameAttrib;
        public uint StreamId;
        public long Time;
        public long RelativeTime;
        public byte[] Data;
    }
}

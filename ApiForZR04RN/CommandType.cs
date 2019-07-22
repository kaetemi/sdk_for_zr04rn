using System;
using System.Collections.Generic;
using System.Text;

namespace ApiForZR04RN
{
    public enum CommandType : uint
    {
        BaseLogin = 0x1100,
        RequestLogin,
        RequestLogout,
        EndLogin,

        BaseReplyLogin = 0x10000,
        ReplyLoginSuccess,
        ReplyLoginFail,
        EndReplyLogin,

        BaseControl = 0x1200,
        RequestStreamStart,
        RequestStreamChange,
        RequestStreamStop,
        RequestKeyframe, //< Supposed to request the next frame to be a keyframe, but no effect on H264 device
        // ...
        EndControl,

        BaseReplyControl = 0x20000,

        EndReplyControl,

        BaseReplyStream = 0xa000000,
        ReplyDataStream,
        EndReplyStream,
    }
}

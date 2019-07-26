using System;
using System.Runtime.CompilerServices;

namespace LibHac.IO.Accessors
{
    public interface IAccessLog
    {
        void Log(TimeSpan startTime, TimeSpan endTime, int handleId, string message, [CallerMemberName] string caller = "");
    }
}
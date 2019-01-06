using System;
using System.Collections.Generic;
using System.Text;

namespace Shared
{
    public interface ILog
    {
        void LogMessage(string msg, bool error);
        void Info(string info);
        void Warn(string warn);
        void Error(string error);
        void Error(Exception ex);
        void Critical(Exception ex);
        void Critical(string error);
        void Start(string info);
        void Stop(string info);
    }
}

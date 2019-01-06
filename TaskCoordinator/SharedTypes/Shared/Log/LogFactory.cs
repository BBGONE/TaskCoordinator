using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;
using Shared.Errors;
using System.Configuration;


namespace Shared
{
    public class LogFactory : ILog
    {
      
        private static ReaderWriterLock _eventSourcesLock = new ReaderWriterLock();
        private static Dictionary<string, LogFactory> _eventSources = new Dictionary<string, LogFactory>();

        static LogFactory()
        {
          
        }
        

        private LogFactory()
        {
            
        }

        private LogFactory(string eventSource)
        {
          
        }

        public static LogFactory GetInstance(string eventSource)
        {
            try
            {
                _eventSourcesLock.AcquireWriterLock(1000);
                try
                {
                    LogFactory log = null;
                    if (_eventSources.TryGetValue("default", out log))
                    {
                        return log;
                    }
                    else
                    {
                        if (!_eventSources.ContainsKey("default"))
                            _eventSources.Add("default", new LogFactory());
                        return new LogFactory("default");
                    }
                }
                finally
                {
                    _eventSourcesLock.ReleaseWriterLock();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("LogFactory innitialization error", ex);
            }
        }

        public static LogFactory Instance
        {
            get
            {
                return GetInstance("default");
            }
        }

        public void LogMessage(string msg, bool error)
        {
            try
            {
                if (error)
                {
                    this.LogMessage(msg, TraceEventType.Error);
                }
                else
                {
                    this.LogMessage(msg, TraceEventType.Information);
                }
            }
            catch
            {
            }
        }

        public void LogMessage(string msg, TraceEventType eventType)
        {
            try
            {
                Console.WriteLine(msg);
            }
            catch
            {
                ;
            }
        }

        public void Info(string info)
        {
            this.LogMessage(info, TraceEventType.Information);
        }

        public void Warn(string warn)
        {
            this.LogMessage(warn, TraceEventType.Warning);
        }

        public void Error(string error)
        {
            this.LogMessage(error, TraceEventType.Error);
        }

        public void Error(Exception ex)
        {
            if (ex != null)
                this.LogMessage(ErrorHelper.GetFullMessage(ex), TraceEventType.Error);
        }

        public void Critical(Exception ex)
        {
            if (ex != null)
                this.LogMessage(ErrorHelper.GetFullMessage(ex), TraceEventType.Critical);
        }

        public void Critical(string error)
        {
            this.LogMessage(error, TraceEventType.Critical);
        }

        public void Start(string info)
        {
            this.LogMessage(string.Format("START! {0}", info), TraceEventType.Start);
        }

        public void Stop(string info)
        {
            this.LogMessage(string.Format("STOP! {0}", info), TraceEventType.Stop);
        }
    }
}

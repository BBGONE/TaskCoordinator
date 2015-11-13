using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Threading;
using Shared.Errors;
using System.Configuration;


namespace Shared
{
    public class Log : ILog
    {
      
        private static ReaderWriterLock _eventSourcesLock = new ReaderWriterLock();
        private static Dictionary<string, Log> _eventSources = new Dictionary<string, Log>();

        static Log()
        {
          
        }
        

        private Log()
        {
            
        }

        private Log(string eventSource)
        {
          
        }

        public static Log GetInstance(string eventSource)
        {
            try
            {
                _eventSourcesLock.AcquireWriterLock(1000);
                try
                {
                    Log log = null;
                    if (_eventSources.TryGetValue("default", out log))
                    {
                        return log;
                    }
                    else
                    {
                        if (!_eventSources.ContainsKey("default"))
                            _eventSources.Add("default", new Log());
                        return new Log("default");
                    }
                }
                finally
                {
                    _eventSourcesLock.ReleaseWriterLock();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static Log Instance
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

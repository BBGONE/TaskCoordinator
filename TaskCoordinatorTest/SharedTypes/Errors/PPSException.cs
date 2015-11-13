using System;
using Shared;
using System.Runtime.Serialization;

namespace Shared.Errors
{
    [Serializable]
    public class PPSException : Exception
    {
        private static ILog _log = Log.Instance;

        private string _shortMessage;
        private string _vocalMessage;

        public PPSException()
            : this(_log)
        {
        }

        public PPSException(ILog log)
            : base()
        {
            log.Error(this);
        }

        public PPSException(string message)
            : this(message, message, message, _log)
        {
        }

        public PPSException(string message, ILog log)
            : this(message, message, message)
        {
        }

        public PPSException(string message, Exception innerException)
            : this(message, innerException, _log)
        {
        }

        public PPSException(string message, Exception innerException, ILog log)
            : this(message, message, message, innerException)
        {
        }

        public PPSException(string message, string shortMessage, string vocalMessage)
            : this(message, shortMessage, vocalMessage, _log)
        {
        }

        public PPSException(string message, string shortMessage, string vocalMessage, ILog log)
            : base(message)
        {
            _shortMessage = shortMessage;
            _vocalMessage = vocalMessage;
            log.Error(this);
        }

        public PPSException(string message, string shortMessage, string vocalMessage, Exception innerException)
            : this(message, shortMessage, vocalMessage, innerException, _log)
        {
        }

        public PPSException(string message, string shortMessage, string vocalMessage, Exception innerException, ILog log)
            : base(message, innerException)
        {
            _shortMessage = shortMessage;
            _vocalMessage = vocalMessage;
            log.Error(this);
        }

        protected PPSException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _shortMessage = (string)info.GetValue("ShortMessage", typeof(string));
            _vocalMessage = (string)info.GetValue("VocalMessage", typeof(string));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("ShortMessage", _shortMessage);
            info.AddValue("VocalMessage", _vocalMessage);
        }

        /// <summary>
        /// Краткое сообщение об ошибке
        /// </summary>
        public string ShortMessage
        {
            get { return _shortMessage; }
        }

        /// <summary>
        /// Голосовое сообщение
        /// </summary>
        public string VocalMessage
        {
            get { return _vocalMessage; }
        }
    }
    
}
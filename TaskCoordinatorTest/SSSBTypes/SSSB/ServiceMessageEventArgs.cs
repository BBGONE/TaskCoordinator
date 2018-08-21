using System;
using System.Threading;

namespace SSSB
{
    /// <summary>
    /// Аргументы события приема сообщения (MessageHandler обрабатывающий сообщения получает сообщение в виде аргументов).
    /// </summary>
    public class ServiceMessageEventArgs : EventArgs
    {
        private ISSSBService _service;
        private SSSBMessage _message;
        private bool _endConversationAfterProcessing;
        private bool _rollbackQue;
        private Exception _ex;
        private bool _sendStepCompletedMessage = false;
        private int _taskID;
        private CancellationToken _cancellation;

        public ServiceMessageEventArgs(SSSBMessage message, ISSSBService svc, CancellationToken cancellation)
        {
            _message = message;
            _service = svc;
            _cancellation = cancellation;
            _endConversationAfterProcessing = false;
            _rollbackQue = false;
            _ex = null;
            _taskID = -1;
        }

        /// <summary>
        /// Сообщение.
        /// </summary>
        public SSSBMessage Message
        {
            get { return _message; }
        }

        /// <summary>
        /// Завершать ли диалог после успешной обработки сообщения
        /// </summary>
        public bool EndConversationAfterProcessing
        {
            get
            {
                return _endConversationAfterProcessing;
            }
            set
            {
                _endConversationAfterProcessing = value;
            }
        }

        /// <summary>
        /// Вернуть ли сообщение в очередь для повторной обработки
        /// </summary>
        public bool RollBackQue
        {
            get
            {
                return _rollbackQue;
            }
            set
            {
                _rollbackQue = value;
            }
        }

        /// <summary>
        /// Ошибка возникшая в результате обработки
        /// </summary>
        public Exception ProcessingException
        {
            get
            {
                return _ex;
            }
            set
            {
                _ex = value;
            }
        }

        public ISSSBService SSSBService
        {
            get
            {
                return this._service;
            }
        }

        public int TaskID
        {
            get { return _taskID; }
            set { _taskID = value; }
        }

        public bool IsSendStepCompletedMessage
        {
            get { return _sendStepCompletedMessage; }
            set { _sendStepCompletedMessage = value; }
        }

        public CancellationToken Cancellation
        {
            get
            {
                return _cancellation;
            }
        }
    }
}

using System;
using System.Threading;

namespace SSSB
{
    /// <summary>
    /// Аргументы обработки ошибки выборки сообщения (т.е. когда сообщение при обработке вызывает ошибку и не может быть успешно обработаным)
    /// </summary>
    public class ErrorMessageEventArgs : EventArgs
    {
        private BaseSSSBService _service;
        private SSSBMessage _message;
        private Exception _processingException;
        private CancellationToken _cancellation;

        public ErrorMessageEventArgs(SSSBMessage message, BaseSSSBService svc, Exception processingException, CancellationToken cancellation)
        {
            this._message = message;
            this._service = svc;
            this._processingException = processingException;
            this._cancellation = cancellation;
        }

        /// <summary>
        /// Сообщение.
        /// </summary>
        public SSSBMessage Message
        {
            get { return _message; }
        }

        public Exception ProcessingException
        {
            get
            {
                return _processingException;
            }
            set
            {
                _processingException = value;
            }
        }

        public BaseSSSBService SSSBService
        {
            get
            {
                return this._service;
            }
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

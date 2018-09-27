using Shared;
using Shared.Errors;
using System;
using System.Collections.Concurrent;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using TasksCoordinator;
using TasksCoordinator.Interface;

namespace SSSB
{
    public class SSSBMessageDispatcher : ISSSBDispatcher
    {
        private readonly ILog _log = LogFactory.GetInstance("SSSBMessageDispatcher");

        private ISSSBService _sssbService;
        private ConcurrentDictionary<string, IMessageHandler<ServiceMessageEventArgs>> _messageHandlers;
        private ConcurrentDictionary<string, IMessageHandler<ErrorMessageEventArgs>> _errorMessageHandlers;

        #region  Constants
        public const int MAX_MESSAGE_ERROR_COUNT = 3;
        /// <summary>
        /// The system defined contract name for echo.
        /// </summary>
        private const string EchoContractName = "http://schemas.microsoft.com/SQL/ServiceBroker/ServiceEcho";
        #endregion

        public SSSBMessageDispatcher(ISSSBService sssbService)
        {
            this._sssbService = sssbService;
            this._messageHandlers = new ConcurrentDictionary<string, IMessageHandler<ServiceMessageEventArgs>>();
            this._errorMessageHandlers = new ConcurrentDictionary<string, IMessageHandler<ErrorMessageEventArgs>>();
        }

        protected virtual ServiceMessageEventArgs CreateServiceMessageEventArgs(SSSBMessage message, CancellationToken cancellation)
        {
            ServiceMessageEventArgs args = new ServiceMessageEventArgs(message, this._sssbService, cancellation);
            return args;
        }

        private async Task DispatchErrorMessage(SqlConnection dbconnection, SSSBMessage message, ErrorMessage msgerr, CancellationToken token)
        {
            try
            {
                //для каждого типа сообщения можно добавить нестандартную обработку 
                //которое не может быть обработано
                //например: сохранить тело сообщения в логе
                IMessageHandler<ErrorMessageEventArgs> errorMessageHandler;

                if (_errorMessageHandlers.TryGetValue(message.MessageType, out errorMessageHandler))
                {
                    using (TransactionScope dispatcherTransactionScope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
                    {
                        ErrorMessageEventArgs errArgs = new ErrorMessageEventArgs(message, this._sssbService, msgerr.FirstError, token);
                        errArgs = await errorMessageHandler.HandleMessage(this._sssbService, errArgs).ConfigureAwait(continueOnCapturedContext: false);
                    }
                }

                StandardMessageHandlers.EndDialogMessageWithErrorHandler(dbconnection, message, msgerr.FirstError.Message, null);

                string error = string.Format("Message {0} caused MAX Number of errors '{1}'. Dialog aborted!", message.MessageType, msgerr.FirstError.Message);
                _log.Error(error);
            }
            catch (Exception ex)
            {
                _log.Critical(ex);
            }
        }

        private async Task<bool> _DispatchMessage(SqlConnection dbconnection, SSSBMessage message, CancellationToken token)
        {
            //возвратить ли сообщение назад в очередь?
            bool rollBack = false;

            IMessageHandler<ServiceMessageEventArgs> messageHandler;
            ServiceMessageEventArgs serviceArgs = this.CreateServiceMessageEventArgs(message, token);

            //if we registered custom handlers for predefined message types
            if (_messageHandlers.TryGetValue(message.MessageType, out messageHandler))
            {
                if (message.MessageType == SSSBMessage.EndDialogMessageType ||
                    message.MessageType == SSSBMessage.ErrorMessageType ||
                    (message.MessageType == SSSBMessage.EchoMessageType && message.ContractName == EchoContractName))
                {
                    //Предопределенные сообщения обрабатываются в той же транзакции, т.к. end conversation блокирует conversation_group
                    //т.е. в другой транзакции они просто не смогут быть выполнены
                    serviceArgs = await messageHandler.HandleMessage(this._sssbService, serviceArgs).ConfigureAwait(continueOnCapturedContext: false); ;

                    if (serviceArgs.EndConversationAfterProcessing)
                    {
                        if (serviceArgs.ProcessingException != null)
                            StandardMessageHandlers.EndDialogMessageWithErrorHandler(dbconnection, message, ErrorHelper.GetFullMessage(serviceArgs.ProcessingException), null);
                        else
                            StandardMessageHandlers.EndDialogMessageHandler(dbconnection, message);
                    }
                }
                else
                {
                    //Прочие сообщения обрабатываются в отдельной транзакции!!!
                    using (TransactionScope dispatcherTransactionScope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
                    {
                        serviceArgs = await messageHandler.HandleMessage(this._sssbService, serviceArgs).ConfigureAwait(continueOnCapturedContext: false);
                    }

                    //если не откатываем транзакцию и хотим завершить диалог тогда отсылаем сообщение о завершении диалога
                    if (!serviceArgs.RollBackQue && serviceArgs.EndConversationAfterProcessing)
                    {
                        if (serviceArgs.ProcessingException != null)
                            StandardMessageHandlers.EndDialogMessageWithErrorHandler(dbconnection, message, ErrorHelper.GetFullMessage(serviceArgs.ProcessingException), null);
                        else
                            StandardMessageHandlers.EndDialogMessageHandler(dbconnection, message);
                    }
                    else if (!serviceArgs.RollBackQue && serviceArgs.IsSendStepCompletedMessage)
                    {
                        StandardMessageHandlers.SendStepCompleted(dbconnection, message);
                    }

                    //если откатываем транзакцию, то нет смысла завершать диалог в той же транзакции
                    //так как при откате сообщение об окончании диалога не отошлется и диалог не завершится
                    rollBack = serviceArgs.RollBackQue;
                }
            }
            else if (message.MessageType == SSSBMessage.EndDialogMessageType)
            {
                StandardMessageHandlers.EndDialogMessageHandler(dbconnection, message);
            }
            else if (message.MessageType == SSSBMessage.ErrorMessageType)
            {
                StandardMessageHandlers.ErrorMessageHandler(dbconnection, message);
            }
            else if (message.MessageType == SSSBMessage.EchoMessageType && message.ContractName == EchoContractName)
            {
                StandardMessageHandlers.EchoMessageHandler(dbconnection, message);
            }
            else if (message.MessageType == SSSBMessage.PPS_EmptyMessageType)
            {
                StandardMessageHandlers.EndDialogMessageHandler(dbconnection, message);
            }
            else if (message.MessageType == SSSBMessage.PPS_StepCompleteMessageType)
            {
                //just awake from sleep
            }
            else
            {
                throw new PPSException(string.Format(ServiceBrokerResources.UnknownMessageTypeErrMsg, message.MessageType), _log);
            }

            return rollBack;
        }

        async Task<MessageProcessingResult> IMessageDispatcher<SSSBMessage, SqlConnection>.DispatchMessage(SSSBMessage message, int taskId, CancellationToken token, SqlConnection dbconnection)
        {
            bool rollBack = false;

            ErrorMessage msgerr = null;
            bool end_dialog_with_error = false;
            //определяем сообщение по ConversationHandle
            if (message.ConversationHandle.HasValue)
            {
                // оканчивалась ли ранее обработка этого сообщения с ошибкой?
                msgerr = _sssbService.GetError(message.ConversationHandle.Value);
                if (msgerr != null)
                    end_dialog_with_error = msgerr.ErrorCount >= MAX_MESSAGE_ERROR_COUNT;
            }
            if (end_dialog_with_error)
                await this.DispatchErrorMessage(dbconnection, message, msgerr, token).ConfigureAwait(continueOnCapturedContext: false);
            else
                rollBack = await this._DispatchMessage(dbconnection, message, token).ConfigureAwait(continueOnCapturedContext: false);

            return new MessageProcessingResult() { isRollBack = rollBack };
        }

        public void RegisterMessageHandler(string messageType, IMessageHandler<ServiceMessageEventArgs> handler)
        {
            _messageHandlers[messageType] = handler;
        }

        public void RegisterErrorMessageHandler(string messageType, IMessageHandler<ErrorMessageEventArgs> handler)
        {
            _errorMessageHandlers[messageType] = handler;
        }

        public void UnregisterMessageHandler(string messageType)
        {
            IMessageHandler<ServiceMessageEventArgs> res;
            _messageHandlers.TryRemove(messageType, out res);
        }

        public void UnregisterErrorMessageHandler(string messageType)
        {
            IMessageHandler<ErrorMessageEventArgs> res;
            _errorMessageHandlers.TryRemove(messageType, out res);
        }
    }
}

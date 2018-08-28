using System.Text;
using Shared;
using System.Data.SqlClient;
using Bell.PPS.SSSB;

namespace SSSB
{
	public static class StandardMessageHandlers
	{
        private static ILog _log = Log.GetInstance("SSSB");

        #region Standard MessageHandlers
        /// <summary>
		/// Стандартная обработка ECHO сообщения
		/// </summary>
		/// <param name="receivedMessage"></param>
		public static void EchoMessageHandler(SqlConnection dbconnection, SSSBMessage receivedMessage)
        {
            ServiceBrokerHelper.SendMessage(dbconnection, receivedMessage);
        }

        /// <summary>
        /// Стандартная обработка сообщения об ошибке
        /// </summary>
        /// <param name="receivedMessage"></param>
        public static void ErrorMessageHandler(SqlConnection dbconnection, SSSBMessage receivedMessage)
        {
            if (receivedMessage.ConversationHandle.HasValue)
            {
                ServiceBrokerHelper.EndConversation(dbconnection, receivedMessage.ConversationHandle.Value);
                _log.Error(string.Format(ServiceBrokerResources.ErrorMessageReceivedErrMsg, receivedMessage.ConversationHandle.Value, Encoding.Unicode.GetString(receivedMessage.Body)));
            }
        }

        /// <summary>
        /// Стандартная обработка сообщения о завершении диалога
        /// </summary>
        /// <param name="receivedMessage"></param>
        public static void EndDialogMessageHandler(SqlConnection dbconnection, SSSBMessage receivedMessage)
        {
            if (receivedMessage.ConversationHandle.HasValue)
                ServiceBrokerHelper.EndConversation(dbconnection, receivedMessage.ConversationHandle.Value);
        }

        /// <summary>
        /// Отправка ответного сообщения о завершении задачи
        /// </summary>
        /// <param name="receivedMessage"></param>
        public static void SendStepCompleted(SqlConnection dbconnection, SSSBMessage receivedMessage)
        {
            if (receivedMessage.ConversationHandle.HasValue)
                ServiceBrokerHelper.SendStepCompletedMessage(dbconnection, receivedMessage.ConversationHandle.Value);
        }

        /// <summary>
        /// Завершение диалога с отправкой сообщения об ошибке
        /// </summary>
        /// <param name="receivedMessage"></param>
        public static void EndDialogMessageWithErrorHandler(SqlConnection dbconnection, SSSBMessage receivedMessage, string message, int? errorNumber)
        {
            ServiceBrokerHelper.EndConversationWithError(dbconnection, receivedMessage.ConversationHandle.Value, errorNumber, message);
        }
        #endregion
    }
}

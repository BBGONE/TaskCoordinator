using System;
using Shared;
using Shared.Errors;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Bell.PPS.SSSB;

namespace SSSB
{
	/// <summary>
	/// Вспомогательный класс для работы с SQL Service Broker.
	/// </summary>
	public static class ServiceBrokerHelper
	{
        private static ILog _log = Log.GetInstance("SSSB");
		
		/// <summary>
		/// Запуск диалога обмена сообщениями.
		/// </summary>
		/// <param name="fromService"></param>
		/// <param name="toService"></param>
		/// <param name="contractName"></param>
		/// <param name="lifetime"></param>
		/// <param name="withEncryption"></param>
		/// <param name="relatedConversationHandle"></param>
		/// <param name="relatedConversationGroupID"></param>
		/// <returns></returns>
		public static Guid BeginDialogConversation(SqlConnection dbconnection, string fromService, string toService, string contractName, 
			TimeSpan lifetime, bool withEncryption,	Guid? relatedConversationHandle, Guid? relatedConversationGroupID)
		{
			_log.Info("Выполнение метода BeginDialogConversation(fromService, toService, contractName, lifetime, withEncryption, relatedConversationID, relatedConversationGroupID)");
			try
			{
                Guid? conversationHandle = null;
                SSSBManager.BeginDialogConversation(dbconnection, fromService, toService, contractName,
                    lifetime == TimeSpan.Zero ? (int?)null : (int)lifetime.TotalSeconds,
                    withEncryption, relatedConversationHandle, relatedConversationGroupID, ref conversationHandle);
                return conversationHandle.Value;
            }
            catch (SqlException ex)
            {
                DBWrapperExceptionsHelper.ThrowError(ex, ServiceBrokerResources.BeginDialogConversationErrMsg, _log);
                return Guid.Empty;
            }
            catch (Exception ex)
            {
                throw new PPSException(ServiceBrokerResources.BeginDialogConversationErrMsg, ex, _log);
            }
		}
		
		/// <summary>
		/// Завершение диалога
		/// </summary>
		/// <param name="conversationHandle"></param>
		/// <param name="withCleanup"></param>
		/// <param name="errorCode"></param>
		/// <param name="errorDescription"></param>
		private static void EndConversation(SqlConnection dbconnection, Guid conversationHandle, bool withCleanup, int? errorCode, string errorDescription)
		{
			_log.Info("Выполнение метода EndConversation(conversationHandle, withCleanup, errorCode, errorDescription)");
			try
			{
                SSSBManager.EndConversation(dbconnection, conversationHandle, withCleanup, errorCode, errorDescription);
            }
            catch(SqlException ex)
            {
                DBWrapperExceptionsHelper.ThrowError(ex, ServiceBrokerResources.EndConversationErrMsg, _log);
            }
			catch (Exception ex)
			{
				throw new PPSException(ServiceBrokerResources.EndConversationErrMsg, ex, _log);
			}
		}

        /// <summary>
        /// Послать ответное сообщение об окончании выполнения шага
        /// </summary>
        /// <param name="conversationHandle"></param>
        public static void SendStepCompletedMessage(SqlConnection dbconnection, Guid conversationHandle)
        {
            _log.Info("Выполнение метода SendStepCompletedMessage");
            try
            {

                SSSBManager.SendMessage(dbconnection, conversationHandle, SSSBMessage.PPS_StepCompleteMessageType, new byte[0]);
            }
            catch (SqlException ex)
            {
                DBWrapperExceptionsHelper.ThrowError(ex, ServiceBrokerResources.SendMessageErrMsg, _log);
            }
            catch (Exception ex)
            {
                throw new PPSException(ServiceBrokerResources.SendMessageErrMsg, ex, _log);
            }
        }

		/// <summary>
		/// Завершение диалога.
		/// </summary>
		/// <param name="conversationHandle"></param>
		public static void EndConversation(SqlConnection dbconnection, Guid conversationHandle)
		{
			EndConversation(dbconnection, conversationHandle, false, null, null);
		}

		/// <summary>
		/// Завершение диалога с опцией CLEANUP.
		/// </summary>
		/// <param name="conversationHandle"></param>
		public static void EndConversationWithCleanup(SqlConnection dbconnection, Guid conversationHandle)
		{
			EndConversation(dbconnection, conversationHandle, true, null, null);
		}

		/// <summary>
		/// Завершение диалога с возвратом сообщения об ошибке.
		/// </summary>
		/// <param name="conversationHandle"></param>
		/// <param name="errorCode"></param>
		/// <param name="errorDescription"></param>
		public static void EndConversationWithError(SqlConnection dbconnection, Guid conversationHandle, int? errorCode, string errorDescription)
		{
			EndConversation(dbconnection, conversationHandle, false, errorCode, errorDescription);
		}

		/// <summary>
		/// Отправка сообщения. Сообщение содержит информацию о типе, идентификатор диалога и пр. необходимую для отправки информацию.
		/// </summary>
		/// <param name="message"></param>
		public static void SendMessage(SqlConnection dbconnection, SSSBMessage message)
		{
			_log.Info("Выполнение метода SendMessage(message)");
			try
			{

                SSSBManager.SendMessage(dbconnection, message.ConversationHandle, message.MessageType, message.Body);
			}
            catch (SqlException ex)
            {
                DBWrapperExceptionsHelper.ThrowError(ex, ServiceBrokerResources.SendMessageErrMsg, _log);
            }
            catch (Exception ex)
            {
                throw new PPSException(ServiceBrokerResources.SendMessageErrMsg, ex, _log);
            }
		}

		/// <summary>
		/// Отправка отложенного сообщения. Сообщение содержит информацию о типе, идентификатор диалога и пр. необходимую для отправки информацию.
		/// </summary>
		/// <param name="fromService"></param>
		/// <param name="message"></param>
		/// <param name="lifetime"></param>
		/// <param name="isWithEncryption"></param>
		/// <param name="activationTime"></param>
		/// <param name="objectID"></param>
		public static long SendPendingMessage(SqlConnection dbconnection, string fromService, SSSBMessage message, TimeSpan lifetime, bool isWithEncryption, Guid? initiatorConversationGroupID, DateTime activationTime, string objectID)
		{
			_log.Info("Выполнение метода SendPendingMessage(..)");
			try
			{
                long? pendingMessageID = null;
                SSSBManager.SendPendingMessage(dbconnection, objectID, activationTime, fromService, message.ServiceName, message.ContractName, (int)lifetime.TotalMilliseconds, isWithEncryption, message.ConversationGroupID, message.ConversationHandle, message.Body, message.MessageType, initiatorConversationGroupID, ref pendingMessageID);
                return pendingMessageID.Value;
            }
            catch (SqlException ex)
            {
                DBWrapperExceptionsHelper.ThrowError(ex, ServiceBrokerResources.PendingMessageErrMsg, _log);
                return 0;
            }
            catch (Exception ex)
            {
                throw new PPSException(ServiceBrokerResources.PendingMessageErrMsg, ex, _log);
            }
		}
		
		/// <summary>
		/// Возвращает название очереди сообщений для сервиса
		/// </summary>
		/// <param name="serviceName"></param>
		/// <returns></returns>
		public static async Task<string> GetServiceQueueName(string serviceName)
		{
			_log.Info("Выполнение метода GetServiceQueueName(serviceName)");
			try
			{
                return await SSSBManager.GetServiceQueueName(serviceName).ConfigureAwait(false);
            }
            catch (SqlException ex)
            {
                DBWrapperExceptionsHelper.ThrowError(ex, ServiceBrokerResources.GetServiceQueueNameErrMsg, _log);
                return null;
            }
            catch (Exception ex)
            {
                throw new PPSException(ServiceBrokerResources.GetServiceQueueNameErrMsg, ex, _log);
            }
		}
	}
}

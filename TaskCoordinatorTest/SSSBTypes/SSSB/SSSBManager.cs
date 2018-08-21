using System;
using Shared.Errors;
using System.Data.SqlClient;
using System.Data;
using Database.Shared;
using System.Threading;
using Shared;
using System.Threading.Tasks;
using System.Transactions;

namespace SSSB
{
    /// <summary>
    /// Инкапсулирует обращение к процедурам БД работающими с SSSB
    /// </summary>
    public static class SSSBManager
    {
        public const string RETURN_VALUE_PARAMETER_NAME = "@RETURN_VALUE";
        private static readonly ILog _log = Log.GetInstance("SSSBManager");

        public static int BeginDialogConversation(
                SqlConnection dbconnection,
                String fromService,
                String toService,
                String contractName,
                int? lifetime,
                bool? withEncryption,
                Guid? relatedConversationID,
                Guid? relatedConversationGroupID,
                ref Guid? conversationHandle)
        {
            try
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = dbconnection;
                    command.CommandText = "SSSB.BeginDialogConversation";
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.Add(new SqlParameter("@RETURN_VALUE", SqlDbType.Int, 0, ParameterDirection.ReturnValue, true, 0, 0, "RETURN_VALUE", DataRowVersion.Current, null));
                    command.Parameters.Add(new SqlParameter("@fromService", SqlDbType.NVarChar, 255, ParameterDirection.Input, true, 0, 0, "fromService", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(fromService)));
                    command.Parameters.Add(new SqlParameter("@toService", SqlDbType.NVarChar, 255, ParameterDirection.Input, true, 0, 0, "toService", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(toService)));
                    command.Parameters.Add(new SqlParameter("@contractName", SqlDbType.NVarChar, 128, ParameterDirection.Input, true, 0, 0, "contractName", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(contractName)));
                    command.Parameters.Add(new SqlParameter("@lifetime", SqlDbType.Int, 0, ParameterDirection.Input, true, 0, 0, "lifetime", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(lifetime)));
                    command.Parameters.Add(new SqlParameter("@withEncryption", SqlDbType.Bit, 0, ParameterDirection.Input, true, 0, 0, "withEncryption", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(withEncryption)));
                    command.Parameters.Add(new SqlParameter("@relatedConversationID", SqlDbType.UniqueIdentifier, 0, ParameterDirection.Input, true, 0, 0, "relatedConversationID", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(relatedConversationID)));
                    command.Parameters.Add(new SqlParameter("@relatedConversationGroupID", SqlDbType.UniqueIdentifier, 0, ParameterDirection.Input, true, 0, 0, "relatedConversationGroupID", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(relatedConversationGroupID)));
                    command.Parameters.Add(new SqlParameter("@conversationHandle", SqlDbType.UniqueIdentifier, 0, ParameterDirection.InputOutput, true, 0, 0, "conversationHandle", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(conversationHandle)));

                    command.ExecuteNonQuery();
                    if (command.Parameters["@conversationHandle"].Value == DBNull.Value)
                        conversationHandle = null;
                    else
                        conversationHandle = (Guid)(command.Parameters["@conversationHandle"].Value);
                    return (Int32)command.Parameters[RETURN_VALUE_PARAMETER_NAME].Value;
                }
            }
            catch (SqlException ex)
            {
                DBWrapperExceptionsHelper.ThrowError(ex);
                throw;
            }
        }

        public static int EndConversation(
                SqlConnection dbconnection,
                Guid? conversationHandle,
                bool? withCleanup,
                int? errorCode,
                String errorDescription)
        {
            try
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = dbconnection;
                    command.CommandText = "SSSB.EndConversation";
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.Add(new SqlParameter("@RETURN_VALUE", SqlDbType.Int, 0, ParameterDirection.ReturnValue, true, 0, 0, "RETURN_VALUE", DataRowVersion.Current, null));
                    command.Parameters.Add(new SqlParameter("@conversationHandle", SqlDbType.UniqueIdentifier, 0, ParameterDirection.Input, true, 0, 0, "conversationHandle", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(conversationHandle)));
                    command.Parameters.Add(new SqlParameter("@withCleanup", SqlDbType.Bit, 0, ParameterDirection.Input, true, 0, 0, "withCleanup", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(withCleanup)));
                    command.Parameters.Add(new SqlParameter("@errorCode", SqlDbType.Int, 0, ParameterDirection.Input, true, 0, 0, "errorCode", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(errorCode)));
                    command.Parameters.Add(new SqlParameter("@errorDescription", SqlDbType.NVarChar, 255, ParameterDirection.Input, true, 0, 0, "errorDescription", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(errorDescription)));

                    command.ExecuteNonQuery();
                    return (Int32)command.Parameters[RETURN_VALUE_PARAMETER_NAME].Value;
                }
            }
            catch (SqlException ex)
            {
                DBWrapperExceptionsHelper.ThrowError(ex);
                throw;
            }
        }

        
        public static int SendMessage(
                SqlConnection dbconnection,
                Guid? conversationHandle,
                String messageType,
                byte[] body)
        {
            try
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = dbconnection;
                    command.CommandText = "SSSB.SendMessage";
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.Add(new SqlParameter("@RETURN_VALUE", SqlDbType.Int, 0, ParameterDirection.ReturnValue, true, 0, 0, "RETURN_VALUE", DataRowVersion.Current, null));
                    command.Parameters.Add(new SqlParameter("@conversationHandle", SqlDbType.UniqueIdentifier, 0, ParameterDirection.Input, true, 0, 0, "conversationHandle", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(conversationHandle)));
                    command.Parameters.Add(new SqlParameter("@messageType", SqlDbType.NVarChar, 255, ParameterDirection.Input, true, 0, 0, "messageType", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(messageType)));
                    command.Parameters.Add(new SqlParameter("@body", SqlDbType.VarBinary, -1, ParameterDirection.Input, true, 0, 0, "body", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(body)));

                    command.ExecuteNonQuery();
                    return (Int32)command.Parameters[RETURN_VALUE_PARAMETER_NAME].Value;
                }
            }
            catch (SqlException ex)
            {
                DBWrapperExceptionsHelper.ThrowError(ex);
                throw;
            }
        }

        public static int SendPendingMessage(
                SqlConnection dbconnection,
                String objectID,
                DateTime? activationDate,
                String fromService,
                String toService,
                String contractName,
                int? lifeTime,
                bool? isWithEncryption,
                Guid? relatedConversationGroupID,
                Guid? relatedConversationHandle,
                byte[] messageBody,
                String messageType,
                Guid? initiatorConversationGroupID,
                ref long? pendingMessageID)
        {
            try
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = dbconnection;
                    command.CommandText = "SSSB.SendPendingMessage";
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.Add(new SqlParameter("@RETURN_VALUE", SqlDbType.Int, 0, ParameterDirection.ReturnValue, true, 0, 0, "RETURN_VALUE", DataRowVersion.Current, null));
                    command.Parameters.Add(new SqlParameter("@objectID", SqlDbType.VarChar, 50, ParameterDirection.Input, true, 0, 0, "objectID", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(objectID)));
                    command.Parameters.Add(new SqlParameter("@activationDate", SqlDbType.DateTime, 0, ParameterDirection.Input, true, 0, 0, "activationDate", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(activationDate)));
                    command.Parameters.Add(new SqlParameter("@fromService", SqlDbType.NVarChar, 255, ParameterDirection.Input, true, 0, 0, "fromService", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(fromService)));
                    command.Parameters.Add(new SqlParameter("@toService", SqlDbType.NVarChar, 255, ParameterDirection.Input, true, 0, 0, "toService", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(toService)));
                    command.Parameters.Add(new SqlParameter("@contractName", SqlDbType.NVarChar, 255, ParameterDirection.Input, true, 0, 0, "contractName", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(contractName)));
                    command.Parameters.Add(new SqlParameter("@lifeTime", SqlDbType.Int, 0, ParameterDirection.Input, true, 0, 0, "lifeTime", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(lifeTime)));
                    command.Parameters.Add(new SqlParameter("@isWithEncryption", SqlDbType.Bit, 0, ParameterDirection.Input, true, 0, 0, "isWithEncryption", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(isWithEncryption)));
                    command.Parameters.Add(new SqlParameter("@relatedConversationGroupID", SqlDbType.UniqueIdentifier, 0, ParameterDirection.Input, true, 0, 0, "relatedConversationGroupID", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(relatedConversationGroupID)));
                    command.Parameters.Add(new SqlParameter("@relatedConversationHandle", SqlDbType.UniqueIdentifier, 0, ParameterDirection.Input, true, 0, 0, "relatedConversationHandle", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(relatedConversationHandle)));
                    command.Parameters.Add(new SqlParameter("@messageBody", SqlDbType.VarBinary, -1, ParameterDirection.Input, true, 0, 0, "messageBody", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(messageBody)));
                    command.Parameters.Add(new SqlParameter("@messageType", SqlDbType.NVarChar, 255, ParameterDirection.Input, true, 0, 0, "messageType", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(messageType)));
                    command.Parameters.Add(new SqlParameter("@initiatorConversationGroupID", SqlDbType.UniqueIdentifier, 0, ParameterDirection.Input, true, 0, 0, "initiatorConversationGroupID", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(initiatorConversationGroupID)));
                    command.Parameters.Add(new SqlParameter("@pendingMessageID", SqlDbType.BigInt, 0, ParameterDirection.InputOutput, true, 0, 0, "pendingMessageID", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(pendingMessageID)));

                    command.ExecuteNonQuery();
                    if (command.Parameters["@pendingMessageID"].Value == DBNull.Value)
                        pendingMessageID = null;
                    else
                        pendingMessageID = (long)(command.Parameters["@pendingMessageID"].Value);
                    return (Int32)command.Parameters[RETURN_VALUE_PARAMETER_NAME].Value;
                }
            }
            catch (SqlException ex)
            {
                DBWrapperExceptionsHelper.ThrowError(ex);
                throw;
            }
        }

        #region Recieving Messages
        internal static async Task<IDataReader> ReceiveMessagesAsync(
            SqlConnection dbconnection,
            String queueName,
            int? fetchSize,
            int? waitTimeout,
            CommandBehavior procedureResultBehaviour, CancellationToken cancellation)
        {
            try
            {
                //Transaction tran = Transaction.Current;
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = dbconnection;
                    command.CommandTimeout = 300;
                    command.CommandText = "SSSB.ReceiveMessagesFromQueue";
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.Add(new SqlParameter("@RETURN_VALUE", SqlDbType.Int, 0, ParameterDirection.ReturnValue, true, 0, 0, "RETURN_VALUE", DataRowVersion.Current, null));
                    command.Parameters.Add(new SqlParameter("@queueName", SqlDbType.NVarChar, 128, ParameterDirection.Input, true, 0, 0, "queueName", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(queueName)));
                    command.Parameters.Add(new SqlParameter("@fetchSize", SqlDbType.Int, 0, ParameterDirection.Input, true, 0, 0, "fetchSize", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(fetchSize)));
                    command.Parameters.Add(new SqlParameter("@waitTimeout", SqlDbType.Int, 0, ParameterDirection.Input, true, 0, 0, "waitTimeout", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(waitTimeout)));
                    return await command.ExecuteReaderAsync(procedureResultBehaviour, cancellation);
                }
            }
            catch (SqlException ex)
            {
                if (cancellation.IsCancellationRequested)
                    return null;
                DBWrapperExceptionsHelper.ThrowError(ex);
            }
            return null;
        }

        internal static async Task<IDataReader> ReceiveMessagesNoWaitAsync(
        SqlConnection dbconnection,
        String queueName,
        int? fetchSize,
        CommandBehavior procedureResultBehaviour, CancellationToken cancellation)
        {
            try
            {
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = dbconnection;
                    command.CommandTimeout = 20;
                    command.CommandText = "SSSB.ImmediateReceiveMessagesFromQueue";
                    command.CommandType = CommandType.StoredProcedure;

                    command.Parameters.Add(new SqlParameter("@RETURN_VALUE", SqlDbType.Int, 0, ParameterDirection.ReturnValue, true, 0, 0, "RETURN_VALUE", DataRowVersion.Current, null));
                    command.Parameters.Add(new SqlParameter("@queueName", SqlDbType.NVarChar, 128, ParameterDirection.Input, true, 0, 0, "queueName", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(queueName)));
                    command.Parameters.Add(new SqlParameter("@fetchSize", SqlDbType.Int, 0, ParameterDirection.Input, true, 0, 0, "fetchSize", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(fetchSize)));

                    return await command.ExecuteReaderAsync(procedureResultBehaviour, cancellation);
                }
            }
            catch (SqlException ex)
            {
                if (cancellation.IsCancellationRequested)
                    return null;
                DBWrapperExceptionsHelper.ThrowError(ex);
            }
            return null;
        }
        #endregion
        
        #region Queue Helpers
        public static async Task<string> GetServiceQueueName(String serviceName)
        {
            string queueName = string.Empty;
            using (TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                var dbconnection = await ConnectionManager.GetNewPPSConnectionAsync();
                try
                {
                    using (dbconnection)
                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = dbconnection;
                        command.CommandText = "SSSB.GetServiceQueueName";
                        command.CommandType = CommandType.StoredProcedure;

                        command.Parameters.Add(new SqlParameter("@RETURN_VALUE", SqlDbType.Int, 0, ParameterDirection.ReturnValue, true, 0, 0, "RETURN_VALUE", DataRowVersion.Current, null));
                        command.Parameters.Add(new SqlParameter("@serviceName", SqlDbType.NVarChar, 128, ParameterDirection.Input, true, 0, 0, "serviceName", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(serviceName)));
                        command.Parameters.Add(new SqlParameter("@queueName", SqlDbType.NVarChar, 128, ParameterDirection.InputOutput, true, 0, 0, "queueName", DataRowVersion.Current, NullableHelper.DBNullConvertFrom(queueName)));

                        await command.ExecuteNonQueryAsync();
                        if (command.Parameters["@queueName"].Value == DBNull.Value)
                            queueName = null;
                        else
                            queueName = (String)(command.Parameters["@queueName"].Value);
                        //return (Int32)command.Parameters[RETURN_VALUE_PARAMETER_NAME].Value;
                        return queueName;
                    }
                }
                catch (SqlException ex)
                {
                    DBWrapperExceptionsHelper.ThrowError(ex);
                    throw;
                }
            }
        }
        public static async Task<bool> IsQueueEnabled(string queueName)
        {
            using (TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                var dbconnection = await ConnectionManager.GetNewPPSConnectionAsync();
                try
                {
                    using (dbconnection)
                    using (SqlCommand command = new SqlCommand())
                    {
                        command.Connection = dbconnection;
                        command.CommandText = "select CAST(is_receive_enabled as BIT) as IsEnabled from sys.service_queues where Name=@queueName";
                        SqlParameter qnParam = command.Parameters.Add(new SqlParameter("@queueName", SqlDbType.NVarChar, 128));
                        qnParam.Value = queueName;

                        SqlDataReader dr = await command.ExecuteReaderAsync();
                        try
                        {
                            if (dr.Read())
                            {
                                return dr.GetBoolean(0);
                            }
                        }
                        finally
                        {
                            dr.Close();
                        }
                        throw new PPSException(string.Format("Queue {0} not found in sys.service_queues", queueName));
                    }
                }
                catch (SqlException ex)
                {
                    DBWrapperExceptionsHelper.ThrowError(ex);
                    throw;
                }
            }
        }
        public static async Task EnableQueue(string queueName)
        {
            using (TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                var dbconnection = await ConnectionManager.GetNewPPSConnectionAsync();
                try
                {
                    using (dbconnection)
                    using (SqlCommand command = new SqlCommand())
                    {
                        string sql = string.Format("alter queue {0} with status = on", queueName);
                        command.Connection = dbconnection;
                        command.CommandText = sql;
                        await command.ExecuteNonQueryAsync();
                    }
                }
                catch (SqlException ex)
                {
                    DBWrapperExceptionsHelper.ThrowError(ex);
                    throw;
                }
            }
        }
        #endregion

    }
}

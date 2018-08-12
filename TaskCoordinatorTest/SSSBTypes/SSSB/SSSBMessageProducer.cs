using Shared;
using Shared.Database;
using Shared.Errors;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using TasksCoordinator;
using TasksCoordinator.Interface;

namespace SSSB
{
    public class SSSBMessageProducer: IMessageProducer<SSSBMessage>
    {
        internal static ILog _log = Log.GetInstance("SSSBMessageProducer");
        private static ConnectionErrorHandler _errorHandler = new ConnectionErrorHandler();

        /// <summary>
        /// Число сообщений в пакете по умолчанию
        /// </summary>
        public const int DEFAULT_FETCH_SIZE = 1;
        private TimeSpan DefaultWaitForTimeout = TimeSpan.FromSeconds(30);
        private bool _IsQueueActivationEnabled =false;
        private CancellationToken _cancellation;
        private readonly string _QueueName;
        private readonly string _ServiceName;

        public SSSBMessageProducer(string QueueName, string ServiceName)
        {
            this._QueueName = QueueName;
            this._ServiceName = ServiceName;
            this._cancellation = CancellationToken.None;
        }

        async Task<int> IMessageProducer<SSSBMessage>.GetMessages(IMessageWorker<SSSBMessage> worker, bool isPrimaryReader)
        {
            SSSBMessage[] messages = new SSSBMessage[0];
            bool beforeWorkCalled = false;
            int cnt = 0;
         
            MessageProcessingResult res = new MessageProcessingResult() { isRollBack = false };
            TransactionOptions tranOptions = new TransactionOptions();
            tranOptions.IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted;
            tranOptions.Timeout = TimeSpan.FromMinutes(60);
            try
            {
                SqlConnection dbconnection = null;
                using (TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.RequiresNew, tranOptions, TransactionScopeAsyncFlowOption.Enabled))
                {
                    Exception error = null;
                    try
                    {
                        dbconnection = await ConnectionFactory.GetNewConnectionAsync(this._cancellation);
                    }
                    catch(Exception ex)
                    {
                        error = ex;
                    }

                    if (error != null)
                    {
                        await _errorHandler.Handle(_log, error, this._cancellation); 
                        return 0;
                    }

                    using (dbconnection)
                    {
                        messages = await this.ReadMessages(dbconnection, isPrimaryReader, worker.taskId).ConfigureAwait(false);
                        cnt = messages.Length;

                        beforeWorkCalled = worker.OnBeforeDoWork();
                        if (cnt > 0)
                        {
                            res = await worker.OnDoWork(messages, dbconnection);
                        }
                        if (!res.isRollBack && cnt > 0)
                        {
                            transactionScope.Complete();
                        }
                    }
                }
            }
            finally
            {
                if (beforeWorkCalled)
                    worker.OnAfterDoWork();
            }
            return cnt;
        }

        private async Task<SSSBMessage[]> ReadMessages(SqlConnection dbconnection, bool isPrimaryReader, int taskId)
        {
            //reading messages from the queue
            IDataReader reader = null;
            SSSBMessage[] messages = new SSSBMessage[0];
            try
            {
                if (isPrimaryReader)
                    reader = await SSSBManager.ReceiveMessagesAsync(dbconnection, this._QueueName, 
                        DEFAULT_FETCH_SIZE, 
                        (int)DefaultWaitForTimeout.TotalMilliseconds, 
                        CommandBehavior.SingleResult | CommandBehavior.SequentialAccess, 
                        this._cancellation);
                else
                    reader = await SSSBManager.ReceiveMessagesNoWaitAsync(dbconnection, this._QueueName, 
                        DEFAULT_FETCH_SIZE, 
                        CommandBehavior.SingleResult | CommandBehavior.SequentialAccess,
                        this._cancellation);

                //no result after cancellation
                if (reader == null)
                {
                    //return empty array
                    return messages;
                }

                var list = new LinkedList<SSSBMessage>();
                while (reader.Read())
                {
                    list.AddLast(this.FillMessageFromReader(reader));
                }

                messages = list.ToArray();
                return messages;
            }
            catch (PPSException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PPSException(string.Format("ReadMessages error on queue: {0}, isPrimaryListener: {1}", this._QueueName, isPrimaryReader), ex);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }


        /// <summary>
        /// Load message data from the data reader
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private SSSBMessage FillMessageFromReader(IDataReader reader)
        {
            SSSBMessage message = new SSSBMessage();
            //conversation_group_id, 
            //conversation_handle,
            //message_sequence_number, 
            //service_name, 
            //service_contract_name, 
            //message_type_name, 
            //validation, 
            //message_body 
            message.ConversationGroupID = reader.GetGuid(0);
            message.ConversationHandle = reader.GetGuid(1);
            message.SequenceNumber = reader.GetInt64(2);
            message.ServiceName = reader.GetString(3);
            message.ContractName = reader.GetString(4);
            message.MessageType = reader.GetString(5);
            string validation = reader.GetString(6);
            if (validation == "X")
                message.ValidationType = MessageValidationType.XML;
            else if (validation == "E")
                message.ValidationType = MessageValidationType.Empty;
            else
                message.ValidationType = MessageValidationType.None;

            if (!reader.IsDBNull(7))
            {
                //Получаем размер сообщения
                message.Body = new byte[reader.GetBytes(7, 0, null, 0, 0)];
                reader.GetBytes(7, 0, message.Body, 0, message.Body.Length);
            }
            else
                message.Body = null;

            message.ServiceName = this._ServiceName;

            return message;
        }

        public bool IsQueueActivationEnabled
        {
            get { return _IsQueueActivationEnabled; }
            set
            {
                _IsQueueActivationEnabled = value;
                this.DefaultWaitForTimeout = this._IsQueueActivationEnabled ? TimeSpan.FromMilliseconds(7000) : TimeSpan.FromSeconds(30);
            }
        }
        public CancellationToken Cancellation
        {
            get { return _cancellation; }
            set { _cancellation = value; }
        }
    }
}

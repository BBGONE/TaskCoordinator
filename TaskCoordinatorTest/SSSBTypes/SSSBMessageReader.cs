using Shared.Database;
using Shared;
using Shared.Errors;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using TasksCoordinator;

namespace SSSB
{
    public class SSSBMessageReader : MessageReader<SSSBMessage, SqlConnection>
    {
        public const int DEFAULT_FETCH_SIZE = 1;
        public static readonly TimeSpan DefaultWaitForTimeout = TimeSpan.FromSeconds(10);
        private static readonly ConnectionErrorHandler _errorHandler = new ConnectionErrorHandler();

        private readonly ISSSBService _service;
        private readonly ISSSBDispatcher _dispatcher;

        public SSSBMessageReader(int taskId, BaseTasksCoordinator<SSSBMessage> tasksCoordinator, ILog log,
            ISSSBService service, ISSSBDispatcher dispatcher) :
            base(taskId, tasksCoordinator, log)
        {
            this._service = service;
            this._dispatcher = dispatcher;
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

            message.ServiceName = this._service.Name;

            return message;
        }

        protected override async Task<SSSBMessage> ReadMessage(bool isPrimaryReader, int taskId, CancellationToken token, SqlConnection state)
        {
            SqlConnection dbconnection = state;
            // reading messages from the queue
            IDataReader reader = null;
            try
            {
                if (isPrimaryReader)
                    reader = await SSSBManager.ReceiveMessagesAsync(dbconnection, this._service.QueueName,
                        DEFAULT_FETCH_SIZE,
                        (int)DefaultWaitForTimeout.TotalMilliseconds,
                        CommandBehavior.SingleResult | CommandBehavior.SequentialAccess,
                        token).ConfigureAwait(false);
                else
                    reader = await SSSBManager.ReceiveMessagesNoWaitAsync(dbconnection, this._service.QueueName,
                        DEFAULT_FETCH_SIZE,
                        CommandBehavior.SingleResult | CommandBehavior.SequentialAccess,
                        token).ConfigureAwait(false);

                // no result after cancellation
                if (reader == null)
                {
                    return null;
                }

                return reader.Read() ? this.FillMessageFromReader(reader) : null;
            }
            catch (Exception ex)
            {
                throw new PPSException(string.Format("ReadMessages error on queue: {0}, isPrimaryListener: {1}", this._service.QueueName, isPrimaryReader), ex);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }

        protected override async Task<MessageProcessingResult> DispatchMessage(SSSBMessage message, int taskId, CancellationToken token, SqlConnection state)
        {
            var res = await this._dispatcher.DispatchMessage(message, taskId, token, state).ConfigureAwait(false);
            return res;
        }

        protected async Task<SqlConnection> TryGetConnection(CancellationToken token)
        {
            SqlConnection dbconnection = null;
            Exception error = null;
            try
            {
                dbconnection = await ConnectionManager.GetNewPPSConnectionAsync(token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            if (error != null)
            {
                await _errorHandler.Handle(Log, error, token).ConfigureAwait(false);
                throw error;
            }

            return dbconnection;
        }

        protected override async Task<int> DoWork(bool isPrimaryReader, CancellationToken token)
        {
            int cnt = 0;
            SSSBMessage msg = null;
            SqlConnection dbconnection = null;
            TransactionScope transactionScope = null;

            bool canRead = this.Coordinator.TryBeginRead(this);
            if (!canRead)
            {
                return cnt;
            }
            try
            {
                TransactionOptions tranOptions = new TransactionOptions();
                tranOptions.IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted;
                tranOptions.Timeout = TimeSpan.FromMinutes(60);
                transactionScope = new TransactionScope(TransactionScopeOption.RequiresNew, tranOptions, TransactionScopeAsyncFlowOption.Enabled);
            }
            catch (Exception)
            {
                this.Coordinator.EndRead();
                throw;
            }

            using (transactionScope)
            {
                try
                {
                    dbconnection = await this.TryGetConnection(token);
                    msg = await this.ReadMessage(isPrimaryReader, this.taskId, token, dbconnection).ConfigureAwait(false);
                    cnt = msg == null ? 0 : 1;
                }
                finally
                {
                    this.Coordinator.EndRead();
                }

                using (dbconnection)
                {
                    if (msg != null)
                    {
                        bool isOk = this.Coordinator.OnBeforeDoWork(this);
                        try
                        {
                            MessageProcessingResult res = await this.DispatchMessage(msg, this.taskId, token, dbconnection).ConfigureAwait(false);
                            if (res.isRollBack)
                            {
                                this.OnRollback(msg, token);
                                return cnt;
                            }
                        }
                        catch (Exception ex)
                        {
                            this.OnProcessMessageException(ex, msg);
                            throw;
                        }
                        finally
                        {
                            if (isOk)
                                this.Coordinator.OnAfterDoWork(this);
                        }

                        transactionScope.Complete();
                    }
                }
            }

            return cnt;
        }

        protected override void OnProcessMessageException(Exception ex, SSSBMessage msg)
        {
            if (msg != null && msg.ConversationHandle.HasValue)
            {
                this._service.AddError(msg.ConversationHandle.Value, ex);
            }
        }
    }
}
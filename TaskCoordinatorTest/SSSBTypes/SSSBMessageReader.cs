using Database.Shared;
using Shared.Errors;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using TasksCoordinator;
using TasksCoordinator.Interface;

namespace SSSB
{
    public class SSSBMessageReader : MessageReader<SSSBMessage>
    {
        private ISSSBService _service;
        private static ConnectionErrorHandler _errorHandler = new ConnectionErrorHandler();

        public SSSBMessageReader(ISSSBService service, int taskId, BaseTasksCoordinator<SSSBMessage> tasksCoordinator,
            IMessageProducer<SSSBMessage> messageProducer) :
            base(taskId, tasksCoordinator, messageProducer)
        {
            this._service = service;
        }

        protected override void OnProcessMessageException(Exception ex, SSSBMessage msg)
        {
            if (msg != null && msg.ConversationHandle.HasValue)
            {
                this._service.AddError(msg.ConversationHandle.Value, ex);
            }
        }

        protected override async Task<int> DoWork(bool isPrimaryReader, CancellationToken cancellation)
        {
            bool beforeWorkCalled = false;
            int cnt = 0;

            try
            {
                TransactionOptions tranOptions = new TransactionOptions();
                tranOptions.IsolationLevel = IsolationLevel.ReadCommitted;
                tranOptions.Timeout = TimeSpan.FromMinutes(60);
                using (TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.RequiresNew, tranOptions, TransactionScopeAsyncFlowOption.Enabled))
                {
                    SqlConnection dbconnection = null;
                    Exception error = null;
                    try
                    {
                        dbconnection = await ConnectionManager.GetNewPPSConnectionAsync(cancellation).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }

                    if (error != null)
                    {
                        await _errorHandler.Handle(_log, error, cancellation).ConfigureAwait(false);
                        return 0;
                    }

                    using (dbconnection)
                    {
                        //dbconnection
                        IEnumerable<SSSBMessage> messages = messages = await this.Producer.ReadMessages(isPrimaryReader, this.taskId, cancellation, dbconnection).ConfigureAwait(false);
                        cnt = messages.Count();
                        if (cnt > 0)
                        {
                            beforeWorkCalled = this.Coordinator.OnBeforeDoWork(this);
                            foreach (SSSBMessage msg in messages)
                            {
                                try
                                {
                                    MessageProcessingResult res = await this.Coordinator.OnDoWork(msg, dbconnection, this.taskId).ConfigureAwait(false);
                                    if (res.isRollBack)
                                        return cnt;
                                }
                                catch (Exception ex)
                                {
                                    this.OnProcessMessageException(ex, msg);
                                    throw;
                                }
                            }

                            transactionScope.Complete();
                        }
                    }
                }
            }
            finally
            {
                if (beforeWorkCalled)
                    this.Coordinator.OnAfterDoWork(this);
            }

            return cnt;
        }
    }
}
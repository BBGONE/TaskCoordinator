using System;
using TasksCoordinator;
using TasksCoordinator.Interface;

namespace SSSB
{
    public class SSSBMessageReader : MessageReader<SSSBMessage, ISSSBDispatcher>
    {
        private ISSSBService _service;

        public SSSBMessageReader(ISSSBService service, int taskId, IMessageProducer<SSSBMessage> messageProducer,
            BaseTasksCoordinator<SSSBMessage, ISSSBDispatcher> tasksCoordinator) :
            base(taskId, messageProducer, tasksCoordinator)
        {
            this._service = service;
        }

        protected override void OnProcessMessageException(Exception ex)
        {
            var msg = this.CurrentMessage;
            if (msg != null && msg.ConversationHandle.HasValue)
            {
                this._service.AddError(msg.ConversationHandle.Value, ex);
            }
        }
    }
}
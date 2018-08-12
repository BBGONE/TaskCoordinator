using System;
using TasksCoordinator;
using TasksCoordinator.Interface;

namespace SSSB
{
    public class SSSBMessageReader : MessageReader<SSSBMessage, ISSSBDispatcher>
    {
        private ErrorMessages ErrorMessages;

        public SSSBMessageReader(int taskId, IMessageProducer<SSSBMessage> messageProducer, 
            BaseTasksCoordinator<SSSBMessage, ISSSBDispatcher> tasksCoordinator, ErrorMessages errorMessages):
            base(taskId, messageProducer, tasksCoordinator)
        {
            this.ErrorMessages = errorMessages;
        }
  
        protected override void OnProcessMessageException(Exception ex)
        {
            var msg = this.CurrentMessage;
            if (msg != null && msg.ConversationHandle.HasValue)
            {
                this.ErrorMessages.AddError(msg.ConversationHandle.Value, ex);
            }
        }
    }
}
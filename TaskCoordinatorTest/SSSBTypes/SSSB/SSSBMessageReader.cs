using System;
using TasksCoordinator;
using TasksCoordinator.Interface;

namespace SSSB
{
    public class SSSBMessageReader : MessageReader<SSSBMessage, ISSSBDispatcher>
    {
        public SSSBMessageReader(int taskId, IMessageProducer<SSSBMessage> messageProducer, 
            BaseTasksCoordinator<SSSBMessage, ISSSBDispatcher> tasksCoordinator):
            base(taskId, messageProducer, tasksCoordinator)
        {
        }
  
        protected override void OnProcessMessageException(Exception ex)
        {
            var msg = this.CurrentMessage;
            if (msg != null && msg.ConversationHandle.HasValue)
            {
                BaseSSSBService.AddError(msg.ConversationHandle.Value, ex);
            }
        }
    }
}
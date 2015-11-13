using System;
using TasksCoordinator;

namespace SSSB
{
    public class SSSBMessageReader : MessageReader<SSSBMessage, ISSSBDispatcher>
    {

        public SSSBMessageReader(int taskId, SSSBTasksCoordinator tasksCoordinator):
            base(taskId, tasksCoordinator)
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
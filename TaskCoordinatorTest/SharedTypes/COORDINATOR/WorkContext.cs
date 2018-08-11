using System.Threading;
using TasksCoordinator.Interface;

namespace TasksCoordinator
{
    public class WorkContext
    {
        public WorkContext(int taskId, object state, CancellationToken cancellation, ITaskCoordinator coordinator)
        {
            this.taskId = taskId;
            this.state = state;
            this.Cancellation = cancellation;
            this.Coordinator = coordinator;
        }

        public int taskId
        {
            get;
            private set;
        }

        public object state
        {
            get;
            private set;
        }

        public CancellationToken Cancellation
        {
            get;
            private set;
        }

        public ITaskCoordinator Coordinator
        {
            get;
            private set;
        }
    }
}

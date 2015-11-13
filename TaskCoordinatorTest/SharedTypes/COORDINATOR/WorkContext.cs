
using System.Threading;

namespace TasksCoordinator
{
    public class WorkContext
    {
        public WorkContext(int taskId, object state, CancellationToken cancellation)
        {
            this.taskId = taskId;
            this.state = state;
            this.cancellation = cancellation;
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

        public CancellationToken cancellation
        {
            get;
            private set;
        }
    }
}

using TasksCoordinator.Interface;

namespace SSSB
{
    public interface ISSSBDispatcher : IMessageDispatcher<SSSBMessage>
    {
        string Name {  get; }
        string QueueName { get; }
    }
}

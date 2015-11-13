using TasksCoordinator;
namespace SSSB
{
    public interface ISSSBDispatcher : IMessageDispatcher<SSSBMessage>
    {
        string Name {  get; }
        string QueueName { get; }
    }
}

using TasksCoordinator.Interface;

namespace SSSB
{
    public interface ISSSBDispatcher : IMessageDispatcher<SSSBMessage>
    {
        string Name {  get; }
        string QueueName { get; }

        void RegisterMessageHandler(string messageType, IMessageHandler<ServiceMessageEventArgs> handler);
        void RegisterErrorMessageHandler(string messageType, IMessageHandler<ErrorMessageEventArgs> handler);
        void UnregisterMessageHandler(string messageType);
        void UnregisterErrorMessageHandler(string messageType);
    }
}

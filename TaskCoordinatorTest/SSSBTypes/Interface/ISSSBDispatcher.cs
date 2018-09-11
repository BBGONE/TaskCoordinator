﻿using TasksCoordinator.Interface;

namespace SSSB
{
    public interface ISSSBDispatcher : IMessageDispatcher<SSSBMessage>
    {
        void RegisterMessageHandler(string messageType, IMessageHandler<ServiceMessageEventArgs> handler);
        void RegisterErrorMessageHandler(string messageType, IMessageHandler<ErrorMessageEventArgs> handler);
        void UnregisterMessageHandler(string messageType);
        void UnregisterErrorMessageHandler(string messageType);
    }
}
using Shared.Services;
using System;

namespace SSSB
{
    public interface ISSSBService : ITaskService
    {
        string QueueName
        {
            get;
        }

        ErrorMessage GetError(Guid messageID);
        int AddError(Guid messageID, Exception err);
    }
}

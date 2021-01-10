using Microsoft.Extensions.Logging;
using Shared.Errors;
using System;
using System.Threading.Tasks;
using TasksCoordinator.Callbacks;

namespace TPLBlocks
{
    class TCallBack<TMsg> : BaseCallback<TMsg>
    {
        private readonly ILogger _logger;

        public TCallBack(ILogger logger)
        {
            _logger = logger;
        }

        public override void TaskSuccess(TMsg message)
        {
            // NOOP
        }
        public override async Task<bool> TaskError(TMsg message, Exception error)
        {
            await Task.CompletedTask;
            _logger.LogError(ErrorHelper.GetFullMessage(error));
            return false;
        }
    }
}

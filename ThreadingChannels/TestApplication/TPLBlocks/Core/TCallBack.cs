using Microsoft.Extensions.Logging;
using Common.Errors;
using System;
using System.Threading.Tasks;

namespace TPLBlocks.Core
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

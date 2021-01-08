using System.Threading.Tasks;

namespace TestApplication
{
    public static class TransformBlockExtensions
    {
        public static void LinkTo<TInput, TOutput, TResult>(this TransformBlock<TInput, TOutput> inputBlock, TransformBlock<TOutput, TResult> outputBlock)
        {
            inputBlock.OutputSink = (async (output) => { await outputBlock.Post(output); });
            // TO DO : Should Propagate exceptions too
            inputBlock.Completion.ContinueWith((antecedent) => outputBlock.Complete());
        }
    }
}

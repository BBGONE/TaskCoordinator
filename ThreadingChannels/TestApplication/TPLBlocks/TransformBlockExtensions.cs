namespace TPLBlocks
{
    public static class TransformBlockExtensions
    {
        public static ITransformBlock<TOutput, TResult> LinkTo<TInput, TOutput, TResult>(this ITransformBlock<TInput, TOutput> inputBlock, ITransformBlock<TOutput, TResult> outputBlock)
        {
            inputBlock.OutputSink += (async (output) => { await outputBlock.Post(output); });
            // TO DO : Should Propagate exceptions too
            inputBlock.Completion.ContinueWith((antecedent) => outputBlock.Complete());

            return outputBlock;
        }
    }
}

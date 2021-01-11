using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TPLBlocks
{
    public static class TransformBlockExtensions
    {
        public static ITargetBlock<TOutput> LinkTo<TOutput>(this ISourceBlock<TOutput> inputBlock, ITargetBlock<TOutput> outputBlock)
        {
            inputBlock.OutputSink += (async (output) => { await outputBlock.Post(output); });

            inputBlock.Completion.ContinueWith((antecedent) => {
                outputBlock.Complete(antecedent.Exception);
            });

            return outputBlock;
        }

        public static ITargetBlock<TOutput> LinkWithPredicateTo<TOutput>(this ISourceBlock<TOutput> inputBlock, ITargetBlock<TOutput> outputBlock, Predicate<TOutput> predicate)
        {
            inputBlock.OutputSink += (async (output) => {
                if (predicate(output))
                {
                    await outputBlock.Post(output);
                }
            });

            inputBlock.Completion.ContinueWith((antecedent) => {
                outputBlock.Complete(antecedent.Exception);
            });

            return outputBlock;
        }

        public static ITargetBlock<TOutput> LinkManyTo<TOutput>(this IEnumerable<ISourceBlock<TOutput>> inputBlocks, ITargetBlock<TOutput> outputBlock)
        {
            foreach (var inputBlock in inputBlocks)
            {
                inputBlock.OutputSink += (async (output) => { await outputBlock.Post(output); });
            }

            var inputBlocksCompletions = inputBlocks.Select(ib => ib.Completion).ToArray();

            var completeTask = Task.WhenAll(inputBlocksCompletions);

            completeTask.ContinueWith((antecedent) => {
                outputBlock.Complete(antecedent.Exception);
            });

            return outputBlock;
        }

        public static ITransformBlock<TOutput, TResult> LinkTo<TOutput, TResult>(this ISourceBlock<TOutput> inputBlock, ITransformBlock<TOutput, TResult> outputBlock)
        {
            return (ITransformBlock<TOutput, TResult>)inputBlock.LinkTo<TOutput>(outputBlock);
        }

        public static ITransformBlock<TOutput, TResult> LinkWithPredicateTo<TOutput, TResult>(this ISourceBlock<TOutput> inputBlock, ITransformBlock<TOutput, TResult> outputBlock, Predicate<TOutput> predicate)
        {
            return (ITransformBlock<TOutput, TResult>)inputBlock.LinkWithPredicateTo<TOutput>(outputBlock, predicate);
        }

        public static ITransformBlock<TOutput, TResult> LinkManyTo<TOutput, TResult>(this IEnumerable<ISourceBlock<TOutput>> inputBlocks, ITransformBlock<TOutput, TResult> outputBlock)
        {
            return (ITransformBlock<TOutput, TResult>)inputBlocks.LinkManyTo<TOutput>(outputBlock);
        }
    }
}

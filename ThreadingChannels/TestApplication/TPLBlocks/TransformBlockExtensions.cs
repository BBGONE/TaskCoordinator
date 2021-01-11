using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TPLBlocks
{
    public static class TransformBlockExtensions
    {
        public static ITransformBlock<TOutput, TResult> LinkTo<TInput, TOutput, TResult>(this ITransformBlock<TInput, TOutput> inputBlock, ITransformBlock<TOutput, TResult> outputBlock)
        {
            inputBlock.OutputSink += (async (output) => { await outputBlock.Post(output); });
    
            inputBlock.Completion.ContinueWith((antecedent) => { 
                outputBlock.Complete(antecedent.Exception); 
            });

            return outputBlock;
        }

        public static ITransformBlock<TOutput, TResult> LinkWithPredicateTo<TInput, TOutput, TResult>(this ITransformBlock<TInput, TOutput> inputBlock, ITransformBlock<TOutput, TResult> outputBlock, Predicate<TOutput> predicate)
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

        public static ITransformBlock<TOutput, TResult> LinkManyTo<TInput, TOutput, TResult>(this IEnumerable<ITransformBlock<TInput, TOutput>> inputBlocks, ITransformBlock<TOutput, TResult> outputBlock)
        {
            foreach (var inputBlock in inputBlocks)
            {
                inputBlock.OutputSink += (async (output) => { await outputBlock.Post(output); });
            }

            var inputBlocksCompletions = inputBlocks.Select(ib=> ib.Completion).ToArray();

            var completeTask = Task.WhenAll(inputBlocksCompletions);

            completeTask.ContinueWith((antecedent) => {
                outputBlock.Complete(antecedent.Exception);
            });

            return outputBlock;
        }
    }
}

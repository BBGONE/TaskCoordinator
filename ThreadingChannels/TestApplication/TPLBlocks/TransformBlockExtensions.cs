using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TPLBlocks
{
    public static class TransformBlockExtensions
    {
        public static ITransformBlock<TInput, TResult> LinkTo<TInput, TResult>(this ISource<TInput> inputBlock, ITransformBlock<TInput, TResult> outputBlock)
        {
            inputBlock.OutputSink += (async (output) => { await outputBlock.Post(output); });
    
            inputBlock.Completion.ContinueWith((antecedent) => { 
                outputBlock.Complete(antecedent.Exception); 
            });

            return outputBlock;
        }

        public static ITransformBlock<TInput, TResult> LinkWithPredicateTo<TInput, TResult>(this ISource<TInput> inputBlock, ITransformBlock<TInput, TResult> outputBlock, Predicate<TInput> predicate)
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

        public static ITransformBlock<TInput, TResult> LinkManyTo<TInput, TResult>(this IEnumerable<ISource<TInput>> inputBlocks, ITransformBlock<TInput, TResult> outputBlock)
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

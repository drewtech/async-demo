using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace async_demo
{ 
    public class AsyncDataManager<TSource>// where TSource: new()
    {
        private BufferBlock<TSource> _queue;
        private DataflowLinkOptions _linkOptions;
        private List<ActionBlock<TSource>> _consumerGroup;

        private void SetupProducerConsumer(int capacity)
        {
            _queue = new BufferBlock<TSource>(new DataflowBlockOptions { BoundedCapacity = capacity });
            _linkOptions = new DataflowLinkOptions(){ PropagateCompletion = true };
            _consumerGroup = new List<ActionBlock<TSource>>();
        }

        private void InitialiseConsumer(IEnumerable<ActionBlock<TSource>> consumerList)
        {
            foreach (var consumer in consumerList)
            {
                _queue.LinkTo(consumer, _linkOptions);
                _consumerGroup.Add(consumer);
            }
        }

        public async Task Produce(TSource value, CancellationToken token)
        {
            await _queue.SendAsync(value, token);
        }

        public async Task Produce(TSource value)
        {
            await _queue.SendAsync(value);
        }

        public void Completed()
        {
            _queue.Complete();
            Task.WhenAll(_consumerGroup.Select(c => c.Completion)).Wait();
        }

        public void Completed(CancellationToken token)
        {
            _queue.Complete();
            Task.WhenAll(_consumerGroup.Select(c => c.Completion)).Wait(token);
        }

        public AsyncDataManager(int boundedCapacity, IEnumerable<ActionBlock<TSource>> consumerList)
        {
            SetupProducerConsumer(boundedCapacity);
            InitialiseConsumer(consumerList);
        }
    }
}
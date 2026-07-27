using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;

using FoundrySharePointKnowledge.Domain.Contracts;

namespace FoundrySharePointKnowledge.Infrastructure.Managers
{
    /// <summary>
    /// This holds work handed off by requests that must not wait for it, backed by an unbounded channel so
    /// enqueueing never blocks the request doing it.
    /// </summary>
    public class BackgroundQueue : IBackgroundQueue
    {
        #region Members
        private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _workItems = Channel.CreateUnbounded<Func<IServiceProvider, CancellationToken, Task>>();
        #endregion
        #region Public Methods
        /// <summary>
        /// Queues a work item to be run once a background worker picks it up.
        /// </summary>
        public void Enqueue(Func<IServiceProvider, CancellationToken, Task> workItem)
        {
            //initialization
            ArgumentNullException.ThrowIfNull(workItem);

            //return
            this._workItems.Writer.TryWrite(workItem);
        }

        /// <summary>
        /// Waits for the next queued work item.
        /// </summary>
        public async Task<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            //return
            return await this._workItems.Reader.ReadAsync(cancellationToken);
        }

        /// <summary>
        /// Returns a textual representation of an instance of this object.
        /// </summary>
        public override string ToString()
        {
            //return
            return $"{this._workItems.Reader.Count} queued work items.";
        }
        #endregion
    }
}

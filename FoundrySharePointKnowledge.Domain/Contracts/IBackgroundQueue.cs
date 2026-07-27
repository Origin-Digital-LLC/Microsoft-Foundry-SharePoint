using System;
using System.Threading;
using System.Threading.Tasks;

namespace FoundrySharePointKnowledge.Domain.Contracts
{
    /// <summary>
    /// Queues work that outlives the request enqueueing it; each work item is handed the service provider of
    /// its own scope, since the request's scope is disposed long before the work runs.
    /// </summary>
    public interface IBackgroundQueue
    {
        #region Methods
        void Enqueue(Func<IServiceProvider, CancellationToken, Task> workItem);
        Task<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
        #endregion
    }
}

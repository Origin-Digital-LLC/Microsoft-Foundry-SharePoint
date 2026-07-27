using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using FoundrySharePointKnowledge.Domain.Contracts;

namespace FoundrySharePointKnowledge.Infrastructure.Managers
{
    /// <summary>
    /// This runs the work queued by requests that must not wait for it, giving each work item its own service
    /// scope, since the scope belonging to the request that queued it is disposed as soon as it responds.
    /// </summary>
    public class BackgroundQueueProcessor : BackgroundService
    {
        #region Members
        private readonly IBackgroundQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BackgroundQueueProcessor> _logger;
        #endregion
        #region Initialization
        public BackgroundQueueProcessor(IBackgroundQueue queue,
                                        IServiceScopeFactory scopeFactory,
                                        ILogger<BackgroundQueueProcessor> logger)
        {
            //initialization
            this._queue = queue ?? throw new ArgumentNullException(nameof(queue));
            this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this._scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        }
        #endregion
        #region Protected Methods
        /// <summary>
        /// Runs each queued work item in turn until the host shuts down.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //initialization
            this._logger.LogInformation($"{nameof(BackgroundQueueProcessor)} is waiting for work.");

            //run until the host stops
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    //wait for work, then run it inside a scope of its own
                    Func<IServiceProvider, CancellationToken, Task> workItem = await this._queue.DequeueAsync(stoppingToken);
                    using (IServiceScope scope = this._scopeFactory.CreateScope())
                        await workItem(scope.ServiceProvider, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    //the host is shutting down, so stop draining the queue
                    break;
                }
                catch (Exception ex)
                {
                    //a failed work item must never take the worker down with it
                    this._logger.LogError(ex, "A queued background work item failed.");
                }
            }

            //return
            this._logger.LogInformation($"{nameof(BackgroundQueueProcessor)} has stopped.");
        }
        #endregion
    }
}

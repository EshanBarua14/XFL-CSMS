namespace XFLCSMS.Services
{
    /// <summary>
    /// Runs auto-assign (idle engineers get the oldest queued ticket) and lapses old transfer requests, every
    /// "Run every" minutes of Admin > Ticket Assignment. <see cref="Wake"/> runs it at once (e.g. "Run now").
    /// </summary>
    public class AutoAssignWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<AutoAssignWorker> _logger;
        private static readonly SemaphoreSlim Signal = new(0, 1);

        public AutoAssignWorker(IServiceScopeFactory scopes, ILogger<AutoAssignWorker> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        /// <summary>Asks the worker to run now instead of at the next interval.</summary>
        public static void Wake()
        {
            try { Signal.Release(); }
            catch (SemaphoreFullException) { /* already woken */ }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); // let the start-up migrations finish
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var minutes = 2;
                try
                {
                    using var scope = _scopes.CreateScope();
                    var assignment = scope.ServiceProvider.GetRequiredService<AssignmentService>();
                    await assignment.ExpireTransfersAsync(stoppingToken);
                    var settings = await assignment.SettingsAsync();
                    minutes = Math.Clamp(settings.AutoAssignIntervalMinutes, 1, 1440);
                    var count = await assignment.AutoAssignAsync(ignoreOnline: false, stoppingToken);
                    if (count > 0)
                    {
                        _logger.LogInformation("Auto-assign gave {Count} queued ticket(s) to idle engineers.", count);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Auto-assign failed.");
                }

                try
                {
                    await Signal.WaitAsync(TimeSpan.FromMinutes(minutes), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}

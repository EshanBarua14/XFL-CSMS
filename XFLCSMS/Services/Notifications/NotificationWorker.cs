using XFLCSMS.Models.Notifications;

namespace XFLCSMS.Services.Notifications
{
    /// <summary>
    /// The background queue: turns events into messages, sends Pending messages (retrying failures after
    /// 1, 5, 15, 60, 240 minutes), checks open tickets against the SLA targets and trims the log.
    /// </summary>
    public class NotificationWorker : BackgroundService
    {
        private static readonly int[] RetryMinutes = { 1, 5, 15, 60, 240 };

        private readonly IServiceScopeFactory _scopes;
        private readonly NotificationQueue _queue;
        private readonly NotificationSettingsStore _settings;
        private readonly ILogger<NotificationWorker> _logger;

        public NotificationWorker(IServiceScopeFactory scopes, NotificationQueue queue, NotificationSettingsStore settings, ILogger<NotificationWorker> logger)
        {
            _scopes = scopes;
            _queue = queue;
            _settings = settings;
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.WhenAll(DispatchLoop(stoppingToken), SendLoop(stoppingToken), SlaLoop(stoppingToken));
        }

        private async Task DispatchLoop(CancellationToken token)
        {
            await foreach (var data in _queue.Events.ReadAllAsync(token))
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var created = await scope.ServiceProvider.GetRequiredService<NotificationDispatcher>().DispatchAsync(data, token);
                    if (created > 0) _queue.Wake();
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not dispatch notification event {Event}.", data.EventKey);
                }
            }
        }

        private async Task SendLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var sent = await SendDueAsync(token);
                    if (sent >= 50) continue; // more waiting
                    await _queue.WaitAsync(TimeSpan.FromSeconds(15), token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // e.g. the database is not reachable: wait and try again
                    _logger.LogError(ex, "Notification send loop failed.");
                    await Task.Delay(TimeSpan.FromSeconds(30), token);
                }
            }
        }

        private async Task<int> SendDueAsync(CancellationToken token)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var sender = scope.ServiceProvider.GetRequiredService<NotificationSender>();
            var now = DateTime.Now;
            var due = await db.NotificationMessages
                .Where(m => m.Status == NotificationStatus.Pending && m.NextAttemptOn <= now)
                .OrderBy(m => m.Channel == NotificationChannel.InApp ? 0 : 1).ThenBy(m => m.Id)
                .Take(50)
                .ToListAsync(token);

            foreach (var message in due)
            {
                await DeliverAsync(message, sender, _settings.GetInt(SettingKeys.MaxAttempts, 5), token);
                await db.SaveChangesAsync(token);
            }

            return due.Count;
        }

        /// <summary>One attempt; updates the row (status, attempts, next attempt, error).</summary>
        public static async Task DeliverAsync(NotificationMessage message, NotificationSender sender, int maxAttempts, CancellationToken token)
        {
            message.Attempts++;
            try
            {
                message.ProviderMessageId = await sender.SendAsync(message, token);
                message.Status = NotificationStatus.Sent;
                message.SentOn = DateTime.Now;
                message.LastError = null;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                message.Attempts--;
                throw;
            }
            catch (Exception ex)
            {
                var error = ex is PermanentNotificationException ? ex.Message : ex.GetType().Name + ": " + ex.Message;
                message.LastError = error.Length > 2000 ? error.Substring(0, 2000) : error;
                if (ex is PermanentNotificationException || message.Attempts >= Math.Max(1, maxAttempts) || message.IsTest)
                {
                    message.Status = NotificationStatus.Failed;
                }
                else
                {
                    message.Status = NotificationStatus.Pending;
                    message.NextAttemptOn = DateTime.Now.AddMinutes(RetryMinutes[Math.Min(message.Attempts - 1, RetryMinutes.Length - 1)]);
                }
            }
        }

        private async Task SlaLoop(CancellationToken token)
        {
            var lastCleanup = DateTime.MinValue;
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), token); // let the start-up migrations finish
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<SlaMonitor>().ScanAsync(token);

                    if (DateTime.Now - lastCleanup > TimeSpan.FromHours(12))
                    {
                        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
                        var cutoff = DateTime.Now.AddDays(-Math.Max(7, _settings.GetInt(SettingKeys.RetentionDays, 90)));
                        var old = await db.NotificationMessages.Where(m => m.CreatedOn < cutoff && m.Status != NotificationStatus.Pending).Take(5000).ToListAsync(token);
                        db.NotificationMessages.RemoveRange(old);
                        await db.SaveChangesAsync(token);
                        lastCleanup = DateTime.Now;
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SLA check failed.");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(Math.Clamp(_settings.GetInt(SettingKeys.SlaScanMinutes, 5), 1, 1440)), token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}

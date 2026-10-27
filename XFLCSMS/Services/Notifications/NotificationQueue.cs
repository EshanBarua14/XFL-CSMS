using System.Threading.Channels;

namespace XFLCSMS.Services.Notifications
{
    /// <summary>Something that happened, waiting to be turned into messages by NotificationDispatcher.</summary>
    public sealed class NotificationEventData
    {
        public string EventKey { get; set; } = string.Empty;
        public DateTime OccurredOn { get; set; } = DateTime.Now;

        public int? ActorUserId { get; set; }
        public string? ActorName { get; set; }

        /// <summary>Scheme + host of the request, for links when no public address is configured.</summary>
        public string? BaseUrl { get; set; }

        /// <summary>Ticket the event is about (links, Maker audience = users of HouseId).</summary>
        public int? IssueId { get; set; }
        public int? HouseId { get; set; }

        /// <summary>User an account event is about (also notified when that account is disabled).</summary>
        public int? SubjectUserId { get; set; }

        /// <summary>Users the event is about: ticket owner, assigned engineer, edited user, to-do owner...</summary>
        public HashSet<int> RelatedUserIds { get; } = new();

        /// <summary>Placeholder values ({{Name}} in the templates).</summary>
        public Dictionary<string, string?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Action of the role controller the message links to, e.g. TicketView (id = LinkId).</summary>
        public string? LinkAction { get; set; }
        public int? LinkId { get; set; }

        /// <summary>Makers open a closed ticket on the rating page instead.</summary>
        public string? MakerLinkAction { get; set; }

        /// <summary>A fixed path instead of a role page, e.g. the sign-in page.</summary>
        public string? LinkPath { get; set; }

        /// <summary>Ticket fields captured when the event happened (names are looked up by the dispatcher).</summary>
        public TicketSnapshot? Ticket { get; set; }
    }

    public sealed class TicketSnapshot
    {
        public int IssueId { get; set; }
        public string? TNumber { get; set; }
        public string? Title { get; set; }
        public string? Priority { get; set; }
        public string? Status { get; set; }
        public int OwnerUserId { get; set; }
        public int BrokerageId { get; set; }
        public int? ProductId { get; set; }
        public int? EngineerUserId { get; set; }
        public string? EngineerName { get; set; }
    }

    /// <summary>
    /// In-memory hand-over from the web requests to NotificationWorker: events to dispatch and a signal that
    /// new messages are waiting. Messages themselves are stored in the database, so nothing is lost on restart
    /// except events of the last milliseconds.
    /// </summary>
    public class NotificationQueue
    {
        private readonly Channel<NotificationEventData> _events = Channel.CreateUnbounded<NotificationEventData>(new UnboundedChannelOptions { SingleReader = true });
        private readonly SemaphoreSlim _wake = new(0);

        public void Publish(NotificationEventData data) => _events.Writer.TryWrite(data);

        public ChannelReader<NotificationEventData> Events => _events.Reader;

        /// <summary>Tells the sender loop to look for pending messages now.</summary>
        public void Wake()
        {
            if (_wake.CurrentCount == 0)
            {
                _wake.Release();
            }
        }

        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken token) => _wake.WaitAsync(timeout, token);
    }
}

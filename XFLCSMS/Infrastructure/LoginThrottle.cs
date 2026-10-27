using System.Collections.Concurrent;

namespace XFLCSMS.Infrastructure
{
    /// <summary>
    /// Slows down password guessing: after <see cref="MaxFailures"/> wrong attempts for the same user name
    /// (or from the same address) within <see cref="Window"/>, further attempts are refused until the window ends.
    /// Also used for the verify / reset-token forms. In memory: one web server, reset on restart.
    /// </summary>
    public sealed class LoginThrottle
    {
        public const int MaxFailures = 5;
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

        private readonly ConcurrentDictionary<string, List<DateTime>> _failures = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Minutes until the key may try again, or 0 when it is not blocked.</summary>
        public int BlockedMinutes(params string?[] keys)
        {
            var now = DateTime.UtcNow;
            var wait = TimeSpan.Zero;
            foreach (var key in keys.Where(k => !string.IsNullOrWhiteSpace(k)))
            {
                if (!_failures.TryGetValue(key!, out var list))
                {
                    continue;
                }

                lock (list)
                {
                    list.RemoveAll(t => now - t > Window);
                    // the address limit is wider: several people may share one office address
                    var limit = key!.StartsWith("ip:", StringComparison.Ordinal) ? MaxFailures * 4 : MaxFailures;
                    if (list.Count >= limit)
                    {
                        var until = list[list.Count - limit] + Window - now;
                        if (until > wait) { wait = until; }
                    }
                }
            }

            return wait > TimeSpan.Zero ? (int)Math.Ceiling(wait.TotalMinutes) : 0;
        }

        public void Fail(params string?[] keys)
        {
            var now = DateTime.UtcNow;
            foreach (var key in keys.Where(k => !string.IsNullOrWhiteSpace(k)))
            {
                var list = _failures.GetOrAdd(key!, _ => new List<DateTime>());
                lock (list)
                {
                    list.Add(now);
                }
            }
        }

        public void Reset(params string?[] keys)
        {
            foreach (var key in keys.Where(k => !string.IsNullOrWhiteSpace(k)))
            {
                _failures.TryRemove(key!, out _);
            }
        }

        public static string Ip(HttpContext context) => "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }
}

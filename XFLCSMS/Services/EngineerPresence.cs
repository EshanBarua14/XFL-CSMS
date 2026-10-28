using System.Collections.Concurrent;

namespace XFLCSMS.Services
{
    /// <summary>
    /// When each XFL staff member last used CSMS (any page or the keep-alive of an open page). Auto-assign only gives
    /// tickets to engineers seen recently, so nobody on leave or signed out gets work. Kept in memory: after a restart
    /// everybody counts as away until their next request.
    /// </summary>
    public class EngineerPresence
    {
        private readonly ConcurrentDictionary<int, DateTime> _seen = new();

        public void Touch(int userId) => _seen[userId] = DateTime.Now;

        public DateTime? LastSeen(int userId) => _seen.TryGetValue(userId, out var on) ? on : null;

        public bool IsOnline(int userId, int windowMinutes) =>
            _seen.TryGetValue(userId, out var on) && DateTime.Now - on <= TimeSpan.FromMinutes(Math.Max(1, windowMinutes));

        public Dictionary<int, DateTime> Snapshot() => new(_seen);
    }
}

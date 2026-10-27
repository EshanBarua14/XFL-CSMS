using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.SignalR;

namespace XFLCSMS.Services.Notifications
{
    /// <summary>
    /// Real-time channel for the in-app toasts. The page gets a signed, time-limited token for the signed-in
    /// user (NotificationHub.CreateToken, printed by the layout) and connects with it; the hub puts the
    /// connection in the user's group. Without a valid token the connection is refused.
    /// </summary>
    public class NotificationHub : Hub
    {
        public const string Path = "/hubs/notifications";
        private const string Purpose = "XFLCSMS.Notifications.Hub";

        private readonly IDataProtectionProvider _protection;

        public NotificationHub(IDataProtectionProvider protection)
        {
            _protection = protection;
        }

        public static string GroupFor(int userId) => "user-" + userId;

        public static string CreateToken(IDataProtectionProvider protection, int userId)
        {
            return protection.CreateProtector(Purpose).ToTimeLimitedDataProtector().Protect(userId.ToString(), TimeSpan.FromHours(12));
        }

        public override async Task OnConnectedAsync()
        {
            var token = Context.GetHttpContext()?.Request.Query["access_token"].ToString();
            int? userId = null;
            try
            {
                if (!string.IsNullOrEmpty(token) && int.TryParse(_protection.CreateProtector(Purpose).ToTimeLimitedDataProtector().Unprotect(token), out var id))
                {
                    userId = id;
                }
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                // expired or forged
            }

            if (userId == null)
            {
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId.Value));
            await base.OnConnectedAsync();
        }
    }
}

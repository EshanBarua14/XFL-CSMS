using XFLCSMS.Services.Chat;

namespace XFLCSMS.Models.Chat
{
    public class ChatPageView
    {
        public string Layout { get; set; } = "_Layout";
        public string Controller { get; set; } = string.Empty;
        public int MeId { get; set; }
        public string MeName { get; set; } = string.Empty;
        public bool CanCreateGroups { get; set; }

        /// <summary>Open the chat with this user on load (?with=), e.g. from a ticket.</summary>
        public int? OpenWith { get; set; }

        /// <summary>Open this chat on load (?c=).</summary>
        public int? OpenConversation { get; set; }
    }

    public class ChatSettingsAdminView
    {
        public Dictionary<string, string?> Values { get; set; } = new();
        public Dictionary<string, string> Rules { get; set; } = new();
        public string[] BlockedExtensions { get; set; } = ChatSettingsStore.BlockedExtensions;
    }
}

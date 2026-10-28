using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace XFLCSMS.Models.Chat
{
    /// <summary>A one-to-one or group chat. Who may talk to whom is decided by ChatPolicy (Admin > Setup > Chat Rules).</summary>
    public class ChatConversation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public bool IsGroup { get; set; }

        /// <summary>Name of a group; null for a one-to-one chat (shown as the other person's name).</summary>
        [MaxLength(100)]
        public string? Title { get; set; }

        /// <summary>One-to-one chats only: "smallerId-largerId", unique, so two people always share one chat.</summary>
        [MaxLength(30)]
        public string? DirectKey { get; set; }

        public int CreatedByUserId { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.Now;

        /// <summary>Time of the latest message (the list is sorted by it).</summary>
        public DateTime? LastMessageOn { get; set; }

        public ICollection<ChatParticipant> Participants { get; set; } = new List<ChatParticipant>();
    }

    public class ChatParticipant
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int ConversationId { get; set; }
        public ChatConversation? Conversation { get; set; }

        public int UserId { get; set; }

        /// <summary>Created the group: may rename it and remove members.</summary>
        public bool IsOwner { get; set; }

        public DateTime JoinedOn { get; set; } = DateTime.Now;

        /// <summary>Left or was removed from the group; the chat is no longer in their list.</summary>
        public DateTime? LeftOn { get; set; }

        /// <summary>Latest message this member has seen (unread counts and "Seen").</summary>
        public long? LastReadMessageId { get; set; }
    }

    public class ChatMessage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public int ConversationId { get; set; }
        public ChatConversation? Conversation { get; set; }

        /// <summary>Null for system lines ("Rahim added Karim") and for messages of deleted users.</summary>
        public int? SenderUserId { get; set; }

        [MaxLength(4000)]
        public string Body { get; set; } = string.Empty;

        public bool IsSystem { get; set; }

        public DateTime SentOn { get; set; } = DateTime.Now;
        public DateTime? EditedOn { get; set; }
        public DateTime? DeletedOn { get; set; }

        public ICollection<ChatAttachment> Attachments { get; set; } = new List<ChatAttachment>();
    }

    /// <summary>A file shared in a chat. Stored under the upload folder in "chat" with a generated name.</summary>
    public class ChatAttachment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public long MessageId { get; set; }
        public ChatMessage? Message { get; set; }

        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string StoredName { get; set; } = string.Empty;

        [MaxLength(150)]
        public string ContentType { get; set; } = "application/octet-stream";

        public long Size { get; set; }
    }

    /// <summary>Chat switches and the who-may-chat-with-whom rules (key / value, Admin > Setup > Chat Rules).</summary>
    public class ChatSetting
    {
        [Key]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Value { get; set; }
    }
}

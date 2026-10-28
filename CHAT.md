# XFL CSMS - Messages (chat and file sharing)

One-to-one and group chats with documents and images, for every role. Messages arrive in real time (same SignalR
connection as the notification bell) and the unread count shows on the chat icon in the top bar.

## Where

| Page | Who | What |
|---|---|---|
| Side menu > **Messages** (or the chat icon in the top bar) | Everybody | Chat list, one-to-one chats, groups, files. |
| Ticket page > **Chat about this ticket** | Everybody | Opens the chat with the client / assigned engineer / developer of the ticket (only the ones you may write to). |
| Admin > Setup > **Chat Rules** | Admin | Who may chat with whom, groups, files, limits. |

## Rules (Admin > Setup > Chat Rules)

For every pair of roles (Administrator, Support Manager, Support Engineer, other XFL staff, Brokerage House User)
the administrator picks one rule; it works both ways:

- **Always**
- **Only through a ticket**: one of the two raised a ticket that is assigned to the other (or the other is its
  developer), while the ticket is open and for *N* days after it is closed (setting, default 7).
- **Same brokerage house**
- **Not allowed**

Defaults: XFL employees chat with each other freely; a brokerage house user (Maker) chats with the Support Managers
and with the engineer assigned to their own ticket(s) only - not with administrators, other engineers or other makers.

The rules are checked on the server when a chat is started, a member is added and **every time a message is sent**,
so a rule change or a reassigned ticket takes effect straight away. An old chat stays readable, but nobody can write in
it while the rules do not allow it. In a group, every member must be allowed to chat with every other member.

Other switches: chat on/off, group chats, makers may create groups (off), file sharing, makers may send files,
largest file (20 MB), allowed file types, edit / delete own messages within *N* minutes (15), largest group (50),
pop-up toasts for new messages. Executable and browser-active types (.exe, .js, .html, .svg, ...) are always refused.

## How it works (for developers)

- Tables: `ChatConversations`, `ChatParticipants`, `ChatMessages`, `ChatAttachments`, `ChatSettings`
  (migration `ChatMessaging`, applied at start-up like the others).
- `Services/Chat/ChatService.cs`: rules, conversations, messages, files; pushes `chat` events to the users' SignalR
  groups (`NotificationHub`). `Services/Chat/ChatSettings.cs`: roles, rules, switches (cached store).
- Actions for all roles: `Controllers/CsmsController.Chat.cs` (`/{Role}/Chat`, `ChatSend`, `ChatFile`, ...).
  Admin page: `Controllers/AdminController.Chat.cs`, `Views/Admin/ChatSettings.cshtml`.
- Page: `Views/Shared/Chat/Chat.cshtml`, `wwwroot/js/xfl-chat.js`, `wwwroot/css/xfl-chat.css`. Top bar icon:
  `Views/Shared/Chat/_ChatLink.cshtml`; live events and the badge: `wwwroot/js/xfl-notify.js`. Without a live
  connection the page polls every 5 seconds.
- Files are stored in `<upload folder>/chat` (`Storage:UploadPath`, default `wwwroot/Uplods/chat`) under generated
  names and are only served to members of the chat. Back the folder up with the database.

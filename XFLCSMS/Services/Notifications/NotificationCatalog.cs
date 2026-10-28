using XFLCSMS.Models.Notifications;
using C = XFLCSMS.Models.Notifications.NotificationChannel;
using R = XFLCSMS.Models.Notifications.NotificationRole;

namespace XFLCSMS.Services.Notifications
{
    /// <summary>A role's default for an event: which channels are on and who of the role gets it.</summary>
    public sealed record RoleDefault(string Role, string[] Channels, string Audience = NotificationAudience.Related);

    /// <summary>One workflow step that can send notifications.</summary>
    public sealed class NotificationEventDefinition
    {
        public string Key { get; init; } = string.Empty;
        public string Group { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;

        /// <summary>Default toast title / e-mail subject.</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>Default one-line text (toast, SMS, WhatsApp, and the main line of the e-mail).</summary>
        public string Text { get; init; } = string.Empty;

        public string[] Placeholders { get; init; } = Array.Empty<string>();
        public RoleDefault[] Defaults { get; init; } = Array.Empty<RoleDefault>();

        /// <summary>Security alerts go to the user who did it as well (e.g. "your password was changed").</summary>
        public bool NotifyActor { get; init; }

        public RoleDefault? DefaultFor(string role) => Defaults.FirstOrDefault(d => d.Role == role);
    }

    /// <summary>
    /// Every event of the application, with its default switches and texts. The administrator changes the
    /// switches and texts under Admin > Notifications; what is not changed there comes from here.
    /// </summary>
    public static class NotificationCatalog
    {
        public static readonly string[] Common = { "RecipientName", "ActorName", "EventName", "Time", "Link", "AppName" };
        private static readonly string[] Ticket = { "TicketNumber", "TicketTitle", "Priority", "Status", "OldStatus", "HouseName", "ProductName", "EngineerName", "OwnerName", "Comment", "Changes" };
        private static readonly string[] Rating = { "Stars", "Solved", "RatingComment" };
        private static readonly string[] Sla = { "SlaKind", "DueAt", "Overdue" };
        private static readonly string[] UserFields = { "UserFullName", "UserName", "UserRole", "UserEmail", "UserPhone", "HouseName" };
        private static readonly string[] Setup = { "ItemType", "ItemName", "Action" };
        private static readonly string[] Award = { "Month", "EngineerName", "AwardStatus", "Reason", "Score" };
        private static readonly string[] Todo = { "TodoTitle", "TodoStatus", "OwnerName" };
        private static readonly string[] Tier = { "Level", "LevelName", "OldLevel", "Reason" };
        private static readonly string[] Transfer = { "FromName", "ToName", "TransferNote", "Decision", "DecisionNote" };
        private static readonly string[] Dev = { "DevStage", "OldDevStage", "DevOwner", "DevEta", "DevReference", "DevNote" };

        public const string TicketsGroup = "Tickets";
        public const string SlaGroup = "SLA";
        public const string FeedbackGroup = "Ratings & awards";
        public const string AccountsGroup = "Users & accounts";
        public const string SetupGroup = "Setup";
        public const string TodoGroup = "To-dos";

        private static readonly string[] InApp = { C.InApp };
        private static readonly string[] InAppEmail = { C.InApp, C.Email };
        private static readonly string[] EmailOnly = { C.Email };

        private static NotificationEventDefinition T(string key, string name, string description, string title, string text, params RoleDefault[] defaults) => new()
        {
            Key = key, Group = TicketsGroup, Name = name, Description = description, Title = title, Text = text,
            Placeholders = Common.Concat(Ticket).ToArray(), Defaults = defaults
        };

        /// <summary>Setup items that have created / updated / deleted events.</summary>
        public static readonly (string Key, string Label)[] SetupItems =
        {
            ("BrokerageHouse", "Brokerage house"), ("Branch", "Branch"), ("Product", "Product"), ("SupportType", "Support type"),
            ("SupportCategory", "Support category"), ("SupportSubCategory", "Support sub-category"), ("AffectedSection", "Affected section")
        };

        // declared after everything Build() uses: static fields are initialised in order
        public static readonly IReadOnlyList<NotificationEventDefinition> Events = Build();

        private static readonly Dictionary<string, NotificationEventDefinition> ByKey = Events.ToDictionary(e => e.Key, StringComparer.OrdinalIgnoreCase);

        public static NotificationEventDefinition? Find(string key) => ByKey.TryGetValue(key, out var e) ? e : null;

        public static IEnumerable<IGrouping<string, NotificationEventDefinition>> Grouped() => Events.GroupBy(e => e.Group);

        private static List<NotificationEventDefinition> Build()
        {
            var staffAll = new[] { new RoleDefault(R.Admin, InApp, NotificationAudience.All), new RoleDefault(R.SupportManager, InApp, NotificationAudience.All) };
            var list = new List<NotificationEventDefinition>
            {
                // ---- Tickets ----------------------------------------------------------------------------------
                T("TicketCreated", "Ticket raised", "A new ticket is saved (by a client or by XFL staff for a house).",
                    "New ticket {{TicketNumber}}", "{{OwnerName}} raised {{TicketNumber}} ({{Priority}}): {{TicketTitle}}",
                    new RoleDefault(R.Admin, InApp, NotificationAudience.All), new RoleDefault(R.SupportManager, InAppEmail, NotificationAudience.All),
                    new RoleDefault(R.SupportEngineer, InApp, NotificationAudience.All), new RoleDefault(R.Maker, EmailOnly)),
                T("TicketAssigned", "Ticket assigned", "An engineer is assigned (or the ticket moves to another engineer).",
                    "{{TicketNumber}} assigned to {{EngineerName}}", "{{TicketNumber}} ({{Priority}}) \"{{TicketTitle}}\" is assigned to {{EngineerName}}.",
                    new RoleDefault(R.SupportManager, InApp, NotificationAudience.All), new RoleDefault(R.SupportEngineer, InAppEmail), new RoleDefault(R.Maker, InAppEmail)),
                T("TicketUnassigned", "Ticket taken off an engineer", "The engineer is removed from the ticket or replaced by another one. Goes to the previous engineer.",
                    "{{TicketNumber}} is no longer assigned to you", "{{TicketNumber}} \"{{TicketTitle}}\" was taken off you by {{ActorName}}.",
                    new RoleDefault(R.SupportEngineer, InApp)),
                T("TicketStatusChanged", "Ticket status changed", "The status changes (other than closing and reopening, which have their own events).",
                    "{{TicketNumber}} is now {{Status}}", "{{TicketNumber}} \"{{TicketTitle}}\" moved from {{OldStatus}} to {{Status}} ({{ActorName}}).",
                    new RoleDefault(R.SupportEngineer, InApp), new RoleDefault(R.Maker, InAppEmail)),
                T("TicketCommented", "Comment / answer added", "The comments of the ticket change (an answer or a note).",
                    "New comment on {{TicketNumber}}", "{{ActorName}} on {{TicketNumber}}: {{Comment}}",
                    new RoleDefault(R.SupportEngineer, InApp), new RoleDefault(R.Maker, InAppEmail)),
                T("TicketUpdated", "Ticket edited", "Title, details, priority or product change.",
                    "{{TicketNumber}} was edited", "{{ActorName}} edited {{TicketNumber}}: {{Changes}}",
                    new RoleDefault(R.SupportEngineer, InApp), new RoleDefault(R.Maker, InApp)),
                T("TicketClosed", "Ticket closed", "The ticket is closed. The client's message asks for a rating of the support.",
                    "{{TicketNumber}} is closed", "{{TicketNumber}} \"{{TicketTitle}}\" was closed by {{ActorName}}. See the answer and rate the support (or reopen it within 14 days if the problem is not solved): {{Link}}",
                    new RoleDefault(R.SupportManager, InApp, NotificationAudience.All), new RoleDefault(R.SupportEngineer, InApp), new RoleDefault(R.Maker, InAppEmail)),
                T("TicketReopened", "Ticket reopened", "A closed ticket is opened again (by the client or by staff).",
                    "{{TicketNumber}} was reopened", "{{ActorName}} reopened {{TicketNumber}} \"{{TicketTitle}}\".",
                    new RoleDefault(R.Admin, InApp, NotificationAudience.All), new RoleDefault(R.SupportManager, InAppEmail, NotificationAudience.All),
                    new RoleDefault(R.SupportEngineer, InAppEmail), new RoleDefault(R.Maker, InApp)),
                T("TicketDeleted", "Ticket deleted", "An administrator deletes a ticket.",
                    "{{TicketNumber}} was deleted", "{{ActorName}} deleted ticket {{TicketNumber}} \"{{TicketTitle}}\".",
                    new RoleDefault(R.Admin, InApp, NotificationAudience.All), new RoleDefault(R.SupportEngineer, InApp), new RoleDefault(R.Maker, InApp)),
                new()
                {
                    Key = "AttachmentAdded", Group = TicketsGroup, Name = "Attachment added", Description = "Files are attached to a ticket.",
                    Title = "New attachment on {{TicketNumber}}", Text = "{{ActorName}} attached {{FileNames}} to {{TicketNumber}}.",
                    Placeholders = Common.Concat(Ticket).Append("FileNames").ToArray(),
                    Defaults = new[] { new RoleDefault(R.SupportEngineer, InApp), new RoleDefault(R.Maker, InApp) }
                },

                // ---- Support levels and development ---------------------------------------------------------------
                new()
                {
                    Key = "TicketEscalated", Group = TicketsGroup, Name = "Ticket escalated / handed back",
                    Description = "A ticket moves to another support level (L1-L5). Goes to the engineers of the new level when nobody is assigned yet.",
                    Title = "{{TicketNumber}} moved to {{Level}}", Text = "{{ActorName}} moved {{TicketNumber}} \"{{TicketTitle}}\" from {{OldLevel}} to {{Level}} ({{LevelName}}): {{Reason}}",
                    Placeholders = Common.Concat(Ticket).Concat(Tier).ToArray(),
                    Defaults = new[] { new RoleDefault(R.SupportManager, InApp, NotificationAudience.All), new RoleDefault(R.SupportEngineer, InAppEmail), new RoleDefault(R.Maker, InApp) }
                },
                new()
                {
                    Key = "TransferRequested", Group = TicketsGroup, Name = "Transfer requested",
                    Description = "An engineer asks another engineer to take over a ticket. Goes to the engineer who is asked.",
                    Title = "{{FromName}} asks you to take {{TicketNumber}}", Text = "{{FromName}} asks you to take over {{TicketNumber}} \"{{TicketTitle}}\": {{TransferNote}}. Accept or decline on the ticket: {{Link}}",
                    Placeholders = Common.Concat(Ticket).Concat(Transfer).ToArray(),
                    Defaults = new[] { new RoleDefault(R.SupportEngineer, InAppEmail) }
                },
                new()
                {
                    Key = "TransferAnswered", Group = TicketsGroup, Name = "Transfer accepted / declined",
                    Description = "A transfer request is accepted, declined, approved or rejected. Goes to the engineers of the request.",
                    Title = "Transfer of {{TicketNumber}}: {{Decision}}", Text = "Transfer of {{TicketNumber}} from {{FromName}} to {{ToName}}: {{Decision}} ({{ActorName}}). {{DecisionNote}}",
                    Placeholders = Common.Concat(Ticket).Concat(Transfer).ToArray(),
                    Defaults = new[] { new RoleDefault(R.SupportEngineer, InApp) }
                },
                new()
                {
                    Key = "TransferAwaitingApproval", Group = TicketsGroup, Name = "Transfer waiting for approval",
                    Description = "An engineer accepted a transfer and a support manager must approve it (when that is switched on under Ticket Assignment).",
                    Title = "Approve transfer of {{TicketNumber}}", Text = "{{ToName}} accepted to take {{TicketNumber}} \"{{TicketTitle}}\" from {{FromName}}: {{TransferNote}}. Approve or reject: {{Link}}",
                    Placeholders = Common.Concat(Ticket).Concat(Transfer).ToArray(),
                    Defaults = new[] { new RoleDefault(R.SupportManager, InAppEmail, NotificationAudience.All), new RoleDefault(R.Admin, InApp, NotificationAudience.All) }
                },
                new()
                {
                    Key = "DevelopmentUpdated", Group = TicketsGroup, Name = "Development status changed",
                    Description = "A ticket that needs a code change moves to another development stage (analysis, development, code review, QA / UAT, ready to deploy, deployed) or gets a new ETA. Internal notes are not sent to the client.",
                    Title = "{{TicketNumber}}: {{DevStage}}", Text = "{{TicketNumber}} \"{{TicketTitle}}\" is now in {{DevStage}}. Owner {{DevOwner}}, ETA {{DevEta}}. {{DevNote}}",
                    Placeholders = Common.Concat(Ticket).Concat(Dev).ToArray(),
                    Defaults = new[] { new RoleDefault(R.SupportManager, InApp, NotificationAudience.All), new RoleDefault(R.SupportEngineer, InApp), new RoleDefault(R.Maker, InAppEmail) }
                },

                // ---- SLA -----------------------------------------------------------------------------------------
                new()
                {
                    Key = "EscalationDue", Group = SlaGroup, Name = "Escalation due (time at level used up)",
                    Description = "An open ticket stayed at its support level longer than the level allows (Admin > Support Levels).",
                    Title = "Escalation due: {{TicketNumber}} at {{Level}}", Text = "{{TicketNumber}} ({{Priority}}) has been at {{Level}} ({{LevelName}}) longer than allowed, overdue by {{Overdue}}. Escalate it or solve it.",
                    Placeholders = Common.Concat(Ticket).Concat(Tier).Concat(Sla).ToArray(),
                    Defaults = new[] { new RoleDefault(R.Admin, InApp, NotificationAudience.All), new RoleDefault(R.SupportManager, InAppEmail, NotificationAudience.All), new RoleDefault(R.SupportEngineer, InApp) }
                },
                SlaEvent("SlaFirstResponseWarning", "First response due soon", "No first response yet and most of the first-response time is used.",
                    "SLA: {{TicketNumber}} needs a first response by {{DueAt}}", "{{TicketNumber}} ({{Priority}}) has no first response yet. Due {{DueAt}}.", false),
                SlaEvent("SlaFirstResponseBreach", "First response overdue", "The first-response target passed without a response.",
                    "SLA breached: {{TicketNumber}} first response overdue", "{{TicketNumber}} ({{Priority}}) first response is overdue by {{Overdue}}.", true),
                SlaEvent("SlaResolutionWarning", "Resolution due soon", "The ticket is still open and most of the resolution time is used.",
                    "SLA: {{TicketNumber}} must be solved by {{DueAt}}", "{{TicketNumber}} ({{Priority}}) \"{{TicketTitle}}\" must be solved by {{DueAt}}.", false),
                SlaEvent("SlaResolutionBreach", "Resolution overdue", "The resolution target passed and the ticket is still open.",
                    "SLA breached: {{TicketNumber}} is overdue", "{{TicketNumber}} ({{Priority}}) \"{{TicketTitle}}\" is overdue by {{Overdue}}.", true),

                // ---- Ratings & awards --------------------------------------------------------------------------
                new()
                {
                    Key = "TicketRated", Group = FeedbackGroup, Name = "Ticket rated", Description = "The client rates a closed ticket (or changes the rating).",
                    Title = "{{TicketNumber}} rated {{Stars}}/5", Text = "{{OwnerName}} rated {{TicketNumber}} {{Stars}}/5. {{RatingComment}}",
                    Placeholders = Common.Concat(Ticket).Concat(Rating).ToArray(),
                    Defaults = new[] { new RoleDefault(R.SupportManager, InApp, NotificationAudience.All), new RoleDefault(R.SupportEngineer, InApp) }
                },
                new()
                {
                    Key = "TicketRatedLow", Group = FeedbackGroup, Name = "Rating needs follow-up", Description = "1 or 2 stars, or the client says the problem is not solved.",
                    Title = "Follow up {{TicketNumber}}: {{Stars}}/5, solved: {{Solved}}", Text = "{{OwnerName}} rated {{TicketNumber}} {{Stars}}/5 (solved: {{Solved}}). Engineer: {{EngineerName}}. {{RatingComment}} Please follow up within 48 hours.",
                    Placeholders = Common.Concat(Ticket).Concat(Rating).ToArray(),
                    Defaults = new[] { new RoleDefault(R.Admin, InAppEmail, NotificationAudience.All), new RoleDefault(R.SupportManager, InAppEmail, NotificationAudience.All) }
                },
                new()
                {
                    Key = "FeedbackReplied", Group = FeedbackGroup, Name = "Reply to client feedback", Description = "XFL management answers the client's rating.",
                    Title = "Reply to your feedback on {{TicketNumber}}", Text = "XFL support management replied to your feedback on {{TicketNumber}}: {{Reply}}",
                    Placeholders = Common.Concat(Ticket).Concat(Rating).Append("Reply").ToArray(),
                    Defaults = new[] { new RoleDefault(R.Maker, InAppEmail) }
                },
                new()
                {
                    Key = "RatingReviewed", Group = FeedbackGroup, Name = "Low rating reviewed", Description = "A manager marks a rating as followed up.",
                    Title = "Rating on {{TicketNumber}} reviewed", Text = "{{ActorName}} followed up the {{Stars}}/5 rating on {{TicketNumber}}.",
                    Placeholders = Common.Concat(Ticket).Concat(Rating).ToArray(),
                    Defaults = new[] { new RoleDefault(R.SupportEngineer, InApp) }
                },
                new()
                {
                    Key = "AwardNominated", Group = FeedbackGroup, Name = "Engineer of the month nominated", Description = "The scorecard nominates the best engineer when a month is frozen.",
                    Title = "Engineer of {{Month}}: {{EngineerName}} nominated", Text = "{{EngineerName}} is nominated as Best Support Engineer of {{Month}} (score {{Score}}).",
                    Placeholders = Common.Concat(Award).ToArray(),
                    Defaults = new[] { new RoleDefault(R.Admin, InApp, NotificationAudience.All), new RoleDefault(R.SupportManager, InAppEmail, NotificationAudience.All) }
                },
                new()
                {
                    Key = "AwardDecided", Group = FeedbackGroup, Name = "Engineer of the month decided", Description = "A manager confirms or overrides the award.",
                    Title = "Best Support Engineer of {{Month}}: {{EngineerName}}", Text = "{{EngineerName}} is Best Support Engineer of {{Month}} ({{AwardStatus}}). Congratulations!",
                    Placeholders = Common.Concat(Award).ToArray(),
                    Defaults = new[] { new RoleDefault(R.Admin, InApp, NotificationAudience.All), new RoleDefault(R.SupportManager, InApp, NotificationAudience.All), new RoleDefault(R.SupportEngineer, InAppEmail, NotificationAudience.All) }
                },
                new()
                {
                    Key = "ScorecardFrozen", Group = FeedbackGroup, Name = "Monthly scorecard frozen", Description = "The scores of a month are frozen (final).",
                    Title = "Scorecard for {{Month}} is final", Text = "The engineer scorecard for {{Month}} is frozen.",
                    Placeholders = Common.Concat(new[] { "Month" }).ToArray(),
                    Defaults = new[] { new RoleDefault(R.SupportManager, InApp, NotificationAudience.All) }
                },
                new()
                {
                    Key = "SlaTargetsUpdated", Group = SetupGroup, Name = "SLA targets changed", Description = "An administrator changes the SLA targets.",
                    Title = "SLA targets changed", Text = "{{ActorName}} changed the SLA targets.",
                    Placeholders = Common, Defaults = new[] { new RoleDefault(R.Admin, InApp, NotificationAudience.All), new RoleDefault(R.SupportManager, InApp, NotificationAudience.All) }
                },

                // ---- Users & accounts ----------------------------------------------------------------------------
                U("UserRegistered", "User registered", "Somebody registers on the sign-up page (before verifying the e-mail).",
                    "New registration: {{UserFullName}}", "{{UserFullName}} ({{HouseName}}) registered as {{UserName}}.",
                    new RoleDefault(R.Admin, InAppEmail, NotificationAudience.All)),
                U("UserVerified", "E-mail verified", "A registered user confirms the e-mail address.",
                    "{{UserFullName}} verified the account", "{{UserFullName}} ({{HouseName}}) verified the e-mail address.",
                    new RoleDefault(R.Admin, InApp, NotificationAudience.All)),
                U("UserCreated", "User created", "An administrator creates a user. The new user gets a welcome message.",
                    "Welcome to Xpert CSMS", "Hello {{UserFullName}}, an account {{UserName}} ({{UserRole}}) was created for you. Sign in at {{Link}}",
                    new RoleDefault(R.Admin, InApp, NotificationAudience.All), new RoleDefault(R.SupportManager, EmailOnly), new RoleDefault(R.SupportEngineer, EmailOnly), new RoleDefault(R.Maker, EmailOnly)),
                U("UserUpdated", "User edited", "Name, e-mail, phone, designation or branch of a user change.",
                    "Account of {{UserFullName}} updated", "{{ActorName}} updated the account of {{UserFullName}}: {{Changes}}",
                    new RoleDefault(R.Admin, InApp, NotificationAudience.All)),
                U("UserRoleChanged", "Role changed", "The role / department of a user changes.",
                    "Your role is now {{UserRole}}", "{{ActorName}} changed the role of {{UserFullName}} to {{UserRole}}.",
                    new RoleDefault(R.Admin, InApp, NotificationAudience.All), new RoleDefault(R.SupportManager, InAppEmail), new RoleDefault(R.SupportEngineer, InAppEmail), new RoleDefault(R.Maker, InAppEmail)),
                U("UserActivated", "Account enabled", "A user is enabled.",
                    "Your account is active", "Hello {{UserFullName}}, your Xpert CSMS account {{UserName}} is active. Sign in at {{Link}}",
                    new RoleDefault(R.Admin, InApp, NotificationAudience.All), new RoleDefault(R.SupportManager, EmailOnly), new RoleDefault(R.SupportEngineer, EmailOnly), new RoleDefault(R.Maker, EmailOnly)),
                U("UserDeactivated", "Account disabled", "A user is disabled.",
                    "Account {{UserName}} disabled", "{{ActorName}} disabled the account of {{UserFullName}}.",
                    new RoleDefault(R.Admin, InApp, NotificationAudience.All)),
                U("UserDeleted", "User deleted", "An administrator deletes a user.",
                    "User {{UserFullName}} deleted", "{{ActorName}} deleted the user {{UserFullName}} ({{UserName}}).",
                    new RoleDefault(R.Admin, InApp, NotificationAudience.All)),
                Security("PasswordChanged", "Password changed", "A user's password is changed or reset. A security alert to the account owner.",
                    "Your password was changed", "Hello {{UserFullName}}, the password of your Xpert CSMS account {{UserName}} was changed at {{Time}}. If this was not you, contact XFL support at once."),
                Security("PasswordResetRequested", "Password reset requested", "Somebody asks for a password reset link (the link itself is always e-mailed).",
                    "Password reset requested", "A password reset was requested for your Xpert CSMS account {{UserName}} at {{Time}}. Ignore this if it was you.", on: false),
                Security("UserSignedIn", "Signed in", "A user signs in. Off by default; useful as a security alert.",
                    "New sign-in to your account", "Your Xpert CSMS account {{UserName}} signed in at {{Time}}.", on: false),

                // ---- To-dos -------------------------------------------------------------------------------------
                D("TodoCreated", "To-do added", "A to-do is added.", "New to-do", "{{OwnerName}} added the to-do \"{{TodoTitle}}\"."),
                D("TodoUpdated", "To-do changed", "A to-do is edited or its status changes.", "To-do updated", "\"{{TodoTitle}}\" is now {{TodoStatus}}."),
                D("TodoCompleted", "To-do done", "A to-do is marked Done.", "To-do done", "{{OwnerName}} finished \"{{TodoTitle}}\"."),
            };

            foreach (var (key, label) in SetupItems)
            {
                foreach (var (action, verb) in new[] { ("Created", "added"), ("Updated", "changed"), ("Deleted", "deleted") })
                {
                    list.Add(new NotificationEventDefinition
                    {
                        Key = key + action, Group = SetupGroup, Name = label + " " + verb,
                        Description = "An administrator " + verb + " a " + label.ToLowerInvariant() + ".",
                        Title = "Setup: {{ItemType}} {{Action}} ({{ItemName}})", Text = "{{ActorName}} {{Action}} the {{ItemType}} \"{{ItemName}}\".",
                        Placeholders = Common.Concat(Setup).ToArray(),
                        Defaults = action == "Updated" ? new[] { staffAll[0] } : staffAll
                    });
                }
            }

            return list;
        }

        private static NotificationEventDefinition SlaEvent(string key, string name, string description, string title, string text, bool breach) => new()
        {
            Key = key, Group = SlaGroup, Name = name, Description = description, Title = title, Text = text,
            Placeholders = Common.Concat(Ticket).Concat(Sla).ToArray(),
            Defaults = breach
                ? new[] { new RoleDefault(R.Admin, InApp, NotificationAudience.All), new RoleDefault(R.SupportManager, InAppEmail, NotificationAudience.All), new RoleDefault(R.SupportEngineer, InAppEmail) }
                : new[] { new RoleDefault(R.SupportManager, InApp, NotificationAudience.All), new RoleDefault(R.SupportEngineer, InApp) }
        };

        private static NotificationEventDefinition U(string key, string name, string description, string title, string text, params RoleDefault[] defaults) => new()
        {
            Key = key, Group = AccountsGroup, Name = name, Description = description, Title = title, Text = text,
            Placeholders = Common.Concat(UserFields).Append("Changes").ToArray(), Defaults = defaults
        };

        private static NotificationEventDefinition Security(string key, string name, string description, string title, string text, bool on = true) => new()
        {
            Key = key, Group = AccountsGroup, Name = name, Description = description, Title = title, Text = text,
            Placeholders = Common.Concat(UserFields).ToArray(), NotifyActor = true,
            Defaults = on ? R.All.Select(r => new RoleDefault(r, EmailOnly)).ToArray() : Array.Empty<RoleDefault>()
        };

        private static NotificationEventDefinition D(string key, string name, string description, string title, string text) => new()
        {
            Key = key, Group = TodoGroup, Name = name, Description = description, Title = title, Text = text,
            Placeholders = Common.Concat(Todo).ToArray(), Defaults = Array.Empty<RoleDefault>()
        };
    }
}

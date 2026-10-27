# XFL CSMS - notifications

Every step of the workflow can notify people on four channels: **in-app toast** (real time, with a bell in the
top bar), **e-mail**, **WhatsApp** and **SMS**. Everything is set up by an administrator in the application; no
code change and no restart is needed.

## Where

| Page | Who | What |
|---|---|---|
| Admin > Notifications > **Who Gets What** | Admin | For every step: which role is told, on which channel, and whether only the *related* people or *everyone* of the role. |
| Admin > Notifications > **Templates** | Admin | Text of every message per channel, with `{{Placeholders}}`, preview and "send me a test". |
| Admin > Notifications > **Providers & Test** | Admin | Channel on/off switches, SMTP, WhatsApp, SMS gateway, SLA warning level, test-send per channel. |
| Admin > Notifications > **Delivery Log** | Admin | Every message with status (Pending / Sent / Failed / Skipped), the provider's error, and Retry. |
| Bell in the top bar, user menu > **My Notifications** | Everybody | Real-time toasts, unread count, history. |
| User menu > **Notification Settings** | Everybody | Opt out per step and channel, or mute a whole channel. |

"Related" means the people the step is about: the ticket owner and the assigned engineer (the previous engineer
for "taken off an engineer"), the user whose account changed, the to-do owner. "Everyone" means every active user
of the role; for brokerage house users it is limited to the users of the ticket's house. The person who did the
step is not notified about their own action (switch under Providers > General), except for security alerts.

## Steps that notify

- **Tickets:** raised, assigned, taken off an engineer, status changed, comment / answer added, edited (title,
  details, priority, product), closed (the client's link opens the rating page), reopened, deleted, attachment added.
- **SLA:** first response due soon / overdue, resolution due soon / overdue. Checked every 5 minutes against
  Admin > SLA Targets; the warning comes at 80 % of the target (both configurable). Each is sent once per ticket.
- **Ratings & awards:** ticket rated, rating needs follow-up (1-2 stars or "not solved"), follow-up reviewed,
  reply to the client's feedback, engineer of the month nominated / decided, monthly scorecard frozen.
- **Users & accounts:** registered, e-mail verified, created by an admin (welcome), edited, role changed, enabled,
  disabled, deleted, password changed, password reset requested and signed in (the last two off by default).
- **Setup:** brokerage house, branch, product, support type / category / sub-category, affected section added,
  changed or deleted; SLA targets changed.
- **To-dos:** added, changed, done (all off by default).

Out of the box the in-app toast and e-mail are on for the important steps; WhatsApp and SMS are off until a
provider is set up and the switches are turned on.

The e-mails that used to be hard-coded (ticket closed / please rate, low rating to managers, reply to the client's
feedback) are now these configurable steps. The verification e-mail at registration and the password-reset link
are still always sent (they are part of signing up / signing in); they now use the SMTP settings of the Providers
page too.

## How it works (for developers)

- `Services/Notifications/NotificationSaveInterceptor.cs` watches every `SaveChanges` made during a web request and
  turns the changes into events. New pages that change tickets, users or setup data notify automatically.
- `NotificationDispatcher` applies the switches, finds the recipients, drops opt-outs, renders the templates and
  stores one `NotificationMessages` row per recipient and channel.
- `NotificationWorker` (background service) sends Pending rows, retries failures after 1, 5, 15, 60 and 240
  minutes (up to "Delivery attempts"), runs the SLA check and deletes log rows older than 90 days.
- In-app toasts go over SignalR (`/hubs/notifications`, client in `wwwroot/js/xfl-notify.js`). Without a
  connection the bell refreshes every minute.
- Event list, default switches and default texts: `Services/Notifications/NotificationCatalog.cs`.
- Run one instance of the application per database: two instances would both send the queue.

## What you need to get from each provider

Secrets (passwords, tokens, API keys) are typed into **Providers & Test**; they are stored **encrypted** in the
database (ASP.NET Core Data Protection) and are never shown again. Nothing goes into the repository.
If you prefer, any setting can also come from an environment variable named `Notifications__<key>`; it wins over
the page, e.g. in Git Bash:

```bash
cd /e/XFL-CSMS-main/XFL-CSMS-main
env 'Notifications__Email.Password=your-app-password' DOTNET_ROLL_FORWARD=Major ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project XFLCSMS/XFLCSMS.csproj --urls http://127.0.0.1:5200
```

The encryption keys live in the Windows user profile of the account that runs the app
(`%LOCALAPPDATA%\ASP.NET\DataProtection-Keys`). If the database is moved to another server, type the secrets again.

### E-mail (SMTP)

- **Gmail / Google Workspace:** turn on 2-step verification for the mailbox, create an **App password**
  (Google Account > Security > App passwords). Server `smtp.gmail.com`, port `587`, security `StartTls`,
  user name = the address, password = the app password.
- **Microsoft 365:** `smtp.office365.com`, `587`, `StartTls`; the mailbox needs "Authenticated SMTP" enabled.
- **Any other mail server:** host, port, user name, password from your mail admin.

> `appsettings.json` still contains the old Gmail app password (`EmailPassword`) and it is in the git history.
> Revoke it in the Google account, create a new one and enter it on the Providers page; then remove
> `EmailPassword` from `appsettings.json`. Empty SMTP fields on the page fall back to `EmailHost`,
> `EmailUsername` and `EmailPassword` from `appsettings.json`, so e-mail keeps working until you switch.

### WhatsApp - Meta WhatsApp Cloud API (recommended, no middleman)

1. A **Meta Business account** (business.facebook.com), verified for production volumes.
2. In **developers.facebook.com** create an app of type *Business* and add the **WhatsApp** product.
3. Add and verify the **phone number** that will send (it must not be in use on the normal WhatsApp app).
   Copy its **Phone number ID** (WhatsApp > API Setup) - not the phone number itself.
4. Create a **System User** (Business settings > Users > System users), give it the app and the WhatsApp
   account with full control, and generate a **permanent access token** with the permissions
   `whatsapp_business_messaging` and `whatsapp_business_management`.
5. In WhatsApp Manager > Message templates create a **Utility** template, e.g. name `csms_notification`,
   language English, body `{{1}}` (one variable), and wait for approval. Business-initiated messages
   outside a 24-hour chat window are only delivered as approved templates; CSMS puts the whole text into `{{1}}`.
6. On Providers & Test: WhatsApp provider `Meta`, phone number ID, access token, message type `Template`,
   template name and language. Turn "WhatsApp on" and the WhatsApp switches you want.

Meta charges per conversation (utility rate for Bangladesh); there is a free test number for development.

### WhatsApp - Twilio (alternative)

Twilio Account SID and Auth token (console.twilio.com), a WhatsApp-enabled sender (or the sandbox number
`+14155238886` for testing, after each recipient joins the sandbox). For production, an approved Content
template (`HX...` Content SID) with one variable `{{1}}`.

### SMS

**Bangladeshi gateways** (cheaper locally, masking / sender ID approved by BTRC through the gateway):
choose provider `Http` and pick a preset, then enter your **API key** and **sender ID**:

| Gateway | You need | Preset |
|---|---|---|
| BulkSMSBD (bulksmsbd.net) | API key, approved sender ID | GET `.../api/smsapi?api_key=..&number=..&senderid=..&message=..`, success text `202` |
| SSL Wireless (sslwireless.com) | API token, SID | POST JSON to `smsplus.sslwireless.com/api/v3/send-sms`, success text `SUCCESS` |
| Alpha SMS (sms.net.bd) | API key, sender ID (optional) | POST form to `api.sms.net.bd/sendsms` |
| Infobip | base URL, API key, sender | POST JSON with `Authorization: App {apikey}` |
| Any other HTTP gateway | its API document | type URL / body / headers with `{to} {message} {apikey} {senderid} {username} {password} {id}` |

Check the preset against your gateway's API document; gateways change their APIs. Set the number format your
gateway expects (`880...`, `+880...` or `01...`) and, if it answers HTTP 200 even on errors, a success text.

**Twilio SMS:** Account SID, Auth token and a Twilio number or alphanumeric sender allowed for Bangladesh.

### Test

After saving, use **Send** next to each channel on Providers & Test (or "Send me a test" on a template).
The result (or the provider's error) is shown at the top of the page and in the Delivery Log.

Users' phone numbers come from their profile (`PhonNumber`). Local numbers like `01712345678` get the default
country code `880`.

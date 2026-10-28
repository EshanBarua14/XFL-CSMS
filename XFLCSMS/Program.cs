global using Microsoft.EntityFrameworkCore;
global using XFLCSMS.Models;
global using XFLCSMS.Data;
global using XFLCSMS.Services.EmailService;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using XFLCSMS.EmailService;
using Microsoft.AspNetCore.DataProtection;
using XFLCSMS.Infrastructure;
using XFLCSMS.Services;
using XFLCSMS.Services.Notifications;
using System.Net;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Machine specific settings (mail password, connection string) go in appsettings.Local.json, which git ignores,
// or in environment variables. Nothing secret belongs in appsettings.json: that file is in the repository.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Logging: console plus daily log files (Serilog section of appsettings.json; path, retention and levels are settings).
// On IIS the files are the first place to look when the site shows the error page or does not start.
builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// no "Server: Kestrel" header (IIS removes its own through web.config)
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    // The project has <Nullable>enable</Nullable>. Without this switch every non-nullable
    // reference property (navigation collections, Designation, Department, ...) is treated
    // as [Required] by model binding, so ModelState.IsValid can never be true for most forms.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;

    // Every POST / DELETE must carry the anti-forgery token (CSRF). Forms built with asp-action get it
    // automatically; AJAX and fetch() calls send it in a header (wwwroot/js/xfl-csrf.js).
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.Cookie.Name = "XFLCSMS.Antiforgery";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(option =>
{
    // 10 minutes signed people out in the middle of writing a ticket; the page now also keeps the session alive
    option.IdleTimeout = TimeSpan.FromMinutes(builder.Configuration.GetValue("Session:IdleMinutes", 30));
    option.Cookie.Name = "XFLCSMS.Session";
    option.Cookie.HttpOnly = true;
    option.Cookie.IsEssential = true;
    option.Cookie.SameSite = SameSiteMode.Lax;
    option.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // behind IIS / a reverse proxy / a tunnel that ends HTTPS, so Request.Scheme (links in e-mails) is right
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Only proxies on this machine (IIS ARR, a local tunnel) are trusted by default. Trusting every sender let any
    // client fake its IP address (and so get round the sign-in lock-out) with an X-Forwarded-For header.
    // A proxy on another machine: list it under ReverseProxy:KnownProxies (IPs) or ReverseProxy:KnownNetworks (CIDR).
    foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
    {
        if (IPAddress.TryParse(proxy, out var address)) { options.KnownProxies.Add(address); }
    }

    foreach (var network in builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
    {
        var parts = network.Split('/');
        if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var prefix) && int.TryParse(parts[1], out var length))
        {
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, length));
        }
    }
});

builder.Services.AddScoped<IEmailServices, EmailService>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<TicketCountService>();
builder.Services.AddScoped<FeedbackService>();
builder.Services.AddScoped<ScorecardService>();
builder.Services.AddScoped<TicketReportService>();
builder.Services.AddScoped<TierService>();
builder.Services.AddScoped<AssignmentService>();
builder.Services.AddSingleton<EngineerPresence>();
builder.Services.AddHostedService<AutoAssignWorker>();
builder.Services.AddSingleton<LoginThrottle>();

// Connection string lives in appsettings.json -> ConnectionStrings:DefaultConnection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is missing. Add it under ConnectionStrings in appsettings.json.");
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

// Notifications (in-app toasts, e-mail, WhatsApp, SMS): see Services/Notifications and NOTIFICATIONS.md.
// Every SaveChanges of a web request is watched by NotificationSaveInterceptor; NotificationWorker sends.
// The data protection keys encrypt the session and anti-forgery cookies and the notification secrets stored in the
// database. Outside Development they are kept in a folder (DataProtection:KeysPath), so an IIS app pool recycle or
// a new version does not sign everybody out or make the stored secrets unreadable. Back this folder up.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("XFLCSMS");
var keysPath = DataProtectionKeys.Folder(builder.Configuration, builder.Environment);
if (keysPath != null)
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
    if (OperatingSystem.IsWindows())
    {
        dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
    }
}
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("notifications", c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSignalR();
builder.Services.AddSingleton<NotificationQueue>();
builder.Services.AddSingleton<NotificationSaveInterceptor>();
builder.Services.AddSingleton<NotificationSettingsStore>();
builder.Services.AddScoped<NotificationDispatcher>();
builder.Services.AddScoped<NotificationSender>();
builder.Services.AddScoped<SlaMonitor>();
builder.Services.AddHostedService<NotificationWorker>();

// Messages (one-to-one and group chats with files): see Services/Chat and CHAT.md. Pushed over the same hub.
builder.Services.AddSingleton<XFLCSMS.Services.Chat.ChatSettingsStore>();
builder.Services.AddScoped<XFLCSMS.Services.Chat.ChatService>();

builder.Services.AddDbContext<DataContext>((services, options) =>
    options.UseSqlServer(connectionString).AddInterceptors(services.GetRequiredService<NotificationSaveInterceptor>()));

var app = builder.Build();

app.Logger.LogInformation("Xpert CSMS {Version} starting in {Environment}. Data protection keys: {Keys}. Attachments: {Uploads}.",
    typeof(Program).Assembly.GetName().Version, app.Environment.EnvironmentName, keysPath ?? "default location",
    TicketService.ConfiguredUploadFolder(app.Configuration, app.Environment));

// Apply migrations and, on an empty database, create the first administrator (see Data/DbInitializer.cs).
DbInitializer.Initialize(app.Services, app.Configuration, app.Logger);

if (string.IsNullOrWhiteSpace(app.Configuration["EmailPassword"]))
{
    app.Logger.LogWarning("EmailPassword is not set: registration, password reset and rating e-mails cannot be sent. Put it in appsettings.Local.json or the EmailPassword environment variable.");
}

app.UseForwardedHeaders();

// one line per request (method, path, status, time) instead of the framework's several
app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (context, _, exception) =>
        exception != null || context.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
        : context.Request.Path.StartsWithSegments("/healthz") || context.Request.Path.StartsWithSegments("/lib") ? Serilog.Events.LogEventLevel.Verbose
        : Serilog.Events.LogEventLevel.Information;
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// 404 / 403 / ... get the application's error page instead of an empty browser page
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");

if (app.Configuration.GetValue("HttpsRedirection", !app.Environment.IsDevelopment()))
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();

// Ticket attachments live in wwwroot/Uplods. They are handed out by the DownloadAttachment actions
// (which check the session), so the folder itself must not be browsable by URL without signing in.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/" + TicketService.UploadFolderName, StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});

var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".webmanifest"] = "application/manifest+json";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypes,
    OnPrepareResponse = ctx =>
    {
        // the service worker and the manifest must be re-checked on every visit so app updates arrive
        var name = ctx.File.Name;
        if (name == "sw.js" || name.EndsWith(".webmanifest", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache";
        }
    }
});

app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapHub<NotificationHub>(NotificationHub.Path);

// Readiness for IIS / monitoring: 200 when the app runs and the database answers, 503 otherwise.
app.MapGet("/healthz", async (DataContext db, ILogger<Program> logger) =>
{
    var ok = false;
    try { ok = await db.Database.CanConnectAsync(); }
    catch (Exception ex) { logger.LogWarning(ex, "Health check: database unreachable"); }
    return ok ? Results.Text("Healthy") : Results.Problem("Database unreachable", statusCode: StatusCodes.Status503ServiceUnavailable);
});

// Liveness: the process answers (no database call), for load balancers that should not drop the site during a database restart.
app.MapGet("/healthz/live", () => Results.Text("Alive"));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=RegisterLogin}/{action=Login}/{id?}");

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Xpert CSMS stopped unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

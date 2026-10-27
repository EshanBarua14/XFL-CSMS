global using Microsoft.EntityFrameworkCore;
global using XFLCSMS.Models;
global using XFLCSMS.Data;
global using XFLCSMS.Services.EmailService;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using XFLCSMS.EmailService;
using XFLCSMS.Infrastructure;
using XFLCSMS.Services;

var builder = WebApplication.CreateBuilder(args);

// Machine specific settings (mail password, connection string) go in appsettings.Local.json, which git ignores,
// or in environment variables. Nothing secret belongs in appsettings.json: that file is in the repository.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

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
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddScoped<IEmailServices, EmailService>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<TicketCountService>();
builder.Services.AddScoped<FeedbackService>();
builder.Services.AddScoped<ScorecardService>();
builder.Services.AddScoped<TicketReportService>();
builder.Services.AddSingleton<LoginThrottle>();

// Connection string lives in appsettings.json -> ConnectionStrings:DefaultConnection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is missing. Add it under ConnectionStrings in appsettings.json.");
builder.Services.AddDbContext<DataContext>(options => options.UseSqlServer(connectionString));

builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

var app = builder.Build();

// Apply migrations and, on an empty database, create the first administrator (see Data/DbInitializer.cs).
DbInitializer.Initialize(app.Services, app.Configuration, app.Logger);

if (string.IsNullOrWhiteSpace(app.Configuration["EmailPassword"]))
{
    app.Logger.LogWarning("EmailPassword is not set: registration, password reset and rating e-mails cannot be sent. Put it in appsettings.Local.json or the EmailPassword environment variable.");
}

app.UseForwardedHeaders();

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

// Liveness for IIS / monitoring: 200 when the app runs and the database answers, 503 otherwise.
app.MapGet("/healthz", async (DataContext db) =>
{
    var ok = false;
    try { ok = await db.Database.CanConnectAsync(); } catch { ok = false; }
    return ok ? Results.Text("Healthy") : Results.Problem("Database unreachable", statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=RegisterLogin}/{action=Login}/{id?}");

app.Run();

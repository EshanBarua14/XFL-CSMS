namespace XFLCSMS.Infrastructure
{
    /// <summary>
    /// Standard browser protections on every response: no MIME sniffing, no framing by other sites (click-jacking),
    /// no full URLs leaked to other sites, and signed-in pages are never kept in shared caches.
    /// </summary>
    public sealed class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public Task Invoke(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "SAMEORIGIN";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

                // pages (not static files) can show tickets and personal data
                var type = context.Response.ContentType ?? string.Empty;
                if (type.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) && !headers.ContainsKey("Cache-Control"))
                {
                    headers["Cache-Control"] = "no-store, max-age=0";
                }

                return Task.CompletedTask;
            });

            return _next(context);
        }
    }
}

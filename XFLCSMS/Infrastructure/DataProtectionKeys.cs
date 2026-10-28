namespace XFLCSMS.Infrastructure
{
    /// <summary>Where the data protection keys are kept (see Program.cs).</summary>
    public static class DataProtectionKeys
    {
        /// <summary>
        /// "DataProtection:KeysPath" when set. Otherwise, outside Development: %ProgramData%\XFLCSMS\keys on Windows,
        /// App_Data/keys next to the application elsewhere. Null in Development: the framework default (the user
        /// profile) stays, so secrets saved while developing remain readable.
        /// </summary>
        public static string? Folder(IConfiguration configuration, IWebHostEnvironment environment)
        {
            var configured = configuration["DataProtection:KeysPath"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                var path = Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured);
                Directory.CreateDirectory(path);
                return Path.GetFullPath(path);
            }

            if (environment.IsDevelopment())
            {
                return null;
            }

            if (OperatingSystem.IsWindows())
            {
                var programData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "XFLCSMS", "keys");
                try
                {
                    Directory.CreateDirectory(programData);
                    return programData;
                }
                catch (UnauthorizedAccessException)
                {
                    // the app pool identity may not create folders there: fall back to the application folder
                }
                catch (IOException)
                {
                }
            }

            var local = Path.Combine(environment.ContentRootPath, "App_Data", "keys");
            Directory.CreateDirectory(local);
            return local;
        }
    }
}

using System.Security.Cryptography;

namespace XFLCSMS.Services
{
    /// <summary>
    /// Password hashing shared by every controller.
    /// New and changed passwords: PBKDF2-SHA256, 210,000 iterations, 32-byte random salt (OWASP 2023 guidance).
    /// Older accounts were hashed with a single HMAC-SHA512 round (128-byte key used as salt): those still verify,
    /// and <see cref="NeedsUpgrade"/> tells the sign-in to re-hash them with PBKDF2 while the password is at hand.
    /// </summary>
    public static class PasswordHasher
    {
        private const int Iterations = 210_000;
        private const int SaltBytes = 32;
        private const int HashBytes = 32;

        // HMACSHA512's default key, which the legacy scheme stored as the salt, is 128 bytes
        private const int LegacySaltBytes = 128;

        public static void Create(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            passwordSalt = RandomNumberGenerator.GetBytes(SaltBytes);
            passwordHash = Rfc2898DeriveBytes.Pbkdf2(password, passwordSalt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        }

        public static bool Verify(string? password, byte[]? passwordHash, byte[]? passwordSalt)
        {
            if (string.IsNullOrEmpty(password) || passwordHash == null || passwordSalt == null || passwordSalt.Length == 0)
            {
                return false;
            }

            if (passwordSalt.Length == LegacySaltBytes)
            {
                using var hmac = new HMACSHA512(passwordSalt);
                var legacy = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return CryptographicOperations.FixedTimeEquals(legacy, passwordHash);
            }

            var computed = Rfc2898DeriveBytes.Pbkdf2(password, passwordSalt, Iterations, HashAlgorithmName.SHA256, passwordHash.Length);
            return CryptographicOperations.FixedTimeEquals(computed, passwordHash);
        }

        /// <summary>True for a hash made with the old single-round scheme.</summary>
        public static bool NeedsUpgrade(byte[]? passwordSalt)
        {
            return passwordSalt != null && passwordSalt.Length == LegacySaltBytes;
        }
    }
}

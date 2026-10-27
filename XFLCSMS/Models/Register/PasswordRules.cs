namespace XFLCSMS.Models.Register
{
    /// <summary>
    /// One password rule for every place a password is set (register, create user, change, reset).
    /// They used to differ: 6 characters with no rule on reset and create user, 4+ with a short list of
    /// allowed symbols on register / change.
    /// </summary>
    public static class PasswordRules
    {
        public const string Pattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z\d\s]).{8,100}$";

        public const string Message = "At least 8 characters, with an uppercase letter, a lowercase letter, a digit and a symbol (for example @ # $ !).";
    }
}

using System.Security.Cryptography;
using MyCookbook.Data;

namespace MyCookbook.Services;

public class ApiTokenService(CookbookDatabaseService db)
{
    private const string PreferenceKey = "ApiToken";

    public async Task<string> GenerateTokenAsync(string user)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var hash = HashToken(rawToken);
        await db.UpdateUserPreference(PreferenceKey, hash, user);
        return rawToken;
    }

    public async Task<string?> GetExistingTokenHashAsync(string user) =>
        await db.GetUserPreference(PreferenceKey, user);

    public async Task RevokeTokenAsync(string user) =>
        await db.UpdateUserPreference(PreferenceKey, "", user);

    public async Task<string?> LookupUserByTokenAsync(string rawToken)
    {
        var hash = HashToken(rawToken);
        var context = await db.GetContext();
        var pref = context.UserPreferences
            .FirstOrDefault(p => p.Key == PreferenceKey && p.Value == hash);
        return pref?.UserName;
    }

    private static string HashToken(string rawToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}

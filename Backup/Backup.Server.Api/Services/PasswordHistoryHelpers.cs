using System.Text.Json;

namespace Backup.Server.Api.Services;

// AppUser.PasswordHistory is a JSON array of recent password hashes,
// most-recent-first. Parse/serialize helpers keep the JSON encoding
// confined to one place.
internal static class PasswordHistoryHelpers
{
    public static List<string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    public static string Serialize(IEnumerable<string> history)
    {
        return JsonSerializer.Serialize(history);
    }
}

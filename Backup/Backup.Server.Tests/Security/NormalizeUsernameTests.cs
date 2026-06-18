using Backup.Server.Api.Services;

namespace Backup.Server.Tests.Security;

public class NormalizeUsernameTests
{
    [Theory]
    [InlineData("admin", "ADMIN")]
    [InlineData("  ivan  ", "IVAN")]
    [InlineData("MixedCase", "MIXEDCASE")]
    [InlineData("Ivan.Zaykov", "IVAN.ZAYKOV")]
    public void Trims_and_uppercases(string input, string expected)
    {
        Assert.Equal(expected, AuthService.NormalizeUsername(input));
    }
}

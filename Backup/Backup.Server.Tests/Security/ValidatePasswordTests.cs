using Backup.Server.Api.Services;

namespace Backup.Server.Tests.Security;

public class ValidatePasswordTests
{
    [Theory]
    [InlineData("Admin123!")]
    [InlineData("ZXcvbn1234$")]
    [InlineData("LongPassword9@")]
    public void Strong_passwords_pass(string password)
    {
        var ex = Record.Exception(() => UsersService.ValidatePassword(password));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("Shrt1A!")]            // < 8 chars
    [InlineData("alllowercase1!")]     // no uppercase
    [InlineData("NoDigitsHere!")]      // no digit
    [InlineData("nodigits!")]          // no digit, no uppercase
    [InlineData("NoSpecial123")]       // no special character
    public void Weak_passwords_are_rejected(string password)
    {
        Assert.Throws<InvalidOperationException>(() => UsersService.ValidatePassword(password));
    }
}

using System.Security.Claims;
using System.Text.Encodings.Web;
using Backup.Server.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Backup.Server.Api.Security;

public class AgentEnrollmentAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AgentEnrollmentOptions _options;

    public AgentEnrollmentAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<AgentEnrollmentOptions> enrollmentOptions)
        : base(options, logger, encoder)
    {
        _options = enrollmentOptions.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AuthConstants.AgentEnrollmentHeader, out var values))
        {
            return Task.FromResult(AuthenticateResult.Fail("Enrollment token header is missing."));
        }

        var providedToken = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedToken))
        {
            return Task.FromResult(AuthenticateResult.Fail("Enrollment token is empty."));
        }

        if (!string.Equals(providedToken, _options.EnrollmentToken, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Enrollment token is invalid."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "agent-bootstrap"),
            new Claim(ClaimTypes.Role, AuthConstants.AgentRole),
            new Claim(AuthConstants.TokenTypeClaim, "agent_enrollment")
        };

        var identity = new ClaimsIdentity(claims, AuthConstants.AgentEnrollmentScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthConstants.AgentEnrollmentScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

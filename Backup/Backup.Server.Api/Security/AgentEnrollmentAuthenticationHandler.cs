using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Backup.Server.Application.Interfaces;
using Backup.Server.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Backup.Server.Api.Security;

public class AgentEnrollmentAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    // Keys we drop on HttpContext.Items so controllers know which auth
    // path got them in. Controllers consume an install-token via the
    // service to ensure atomic single-use.
    public const string TokenKindItemKey = "Enrollment.TokenKind";
    public const string TokenRawItemKey = "Enrollment.TokenRaw";

    public const string KindInstall = "install";
    public const string KindShared = "shared";

    private readonly AgentEnrollmentOptions _options;
    private readonly IAgentInstallTokenRepository _installTokens;

    public AgentEnrollmentAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<AgentEnrollmentOptions> enrollmentOptions,
        IAgentInstallTokenRepository installTokens)
        : base(options, logger, encoder)
    {
        _options = enrollmentOptions.Value;
        _installTokens = installTokens;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AuthConstants.AgentEnrollmentHeader, out var values))
        {
            return AuthenticateResult.Fail("Enrollment token header is missing.");
        }

        var providedToken = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedToken))
        {
            return AuthenticateResult.Fail("Enrollment token is empty.");
        }

        // 1) Try install-token first. The controller layer will consume it
        //    atomically once it has the machine name from the request body.
        var installKindAuthenticated = await TryAuthenticateInstallTokenAsync(providedToken);
        if (installKindAuthenticated is not null)
        {
            return installKindAuthenticated;
        }

        // 2) Fall back to the legacy shared token. Constant-time compare
        //    so the rejection latency doesn't leak prefix characters.
        if (IsSharedTokenMatch(providedToken))
        {
            Logger.LogWarning(
                "Agent enrolled using the legacy shared AgentEnrollment:EnrollmentToken. " +
                "Move new agents to per-agent install tokens (POST /api/agents/install-tokens).");

            Context.Items[TokenKindItemKey] = KindShared;
            Context.Items[TokenRawItemKey] = providedToken;
            return BuildSuccess();
        }

        return AuthenticateResult.Fail("Enrollment token is invalid.");
    }

    private async Task<AuthenticateResult?> TryAuthenticateInstallTokenAsync(string presentedToken)
    {
        byte[] tokenBytes;
        try
        {
            tokenBytes = Base64UrlDecode(presentedToken);
        }
        catch (FormatException)
        {
            return null;
        }

        // The install-token service generates 32-byte secrets. Anything
        // shorter/longer can't be one — skip the DB hit.
        if (tokenBytes.Length != 32)
        {
            return null;
        }

        var hash = SHA256.HashData(tokenBytes);
        var record = await _installTokens.FindUsableByHashAsync(hash, Context.RequestAborted);
        if (record is null)
        {
            return null;
        }

        Context.Items[TokenKindItemKey] = KindInstall;
        Context.Items[TokenRawItemKey] = presentedToken;
        return BuildSuccess();
    }

    private bool IsSharedTokenMatch(string presentedToken)
    {
        var configured = _options.EnrollmentToken ?? string.Empty;
        var providedBytes = Encoding.UTF8.GetBytes(presentedToken);
        var expectedBytes = Encoding.UTF8.GetBytes(configured);

        // FixedTimeEquals requires same length to be useful; if lengths
        // differ we still return false, but compare same-length padding so
        // the early-out doesn't leak length via timing on the fast path.
        if (providedBytes.Length != expectedBytes.Length)
        {
            // Burn some work over a fixed-size buffer so length mismatch
            // takes about the same time as a real compare.
            var pad = new byte[Math.Max(expectedBytes.Length, 64)];
            CryptographicOperations.FixedTimeEquals(pad, pad);
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }

    private AuthenticateResult BuildSuccess()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "agent-bootstrap"),
            new Claim(ClaimTypes.Role, AuthConstants.AgentRole),
            new Claim(AuthConstants.TokenTypeClaim, "agent_enrollment"),
        };

        var identity = new ClaimsIdentity(claims, AuthConstants.AgentEnrollmentScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthConstants.AgentEnrollmentScheme);
        return AuthenticateResult.Success(ticket);
    }

    private static byte[] Base64UrlDecode(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String(padded);
    }
}

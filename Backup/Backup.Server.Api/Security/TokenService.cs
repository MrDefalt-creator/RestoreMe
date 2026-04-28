using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Infrastructure.Options;
using Backup.Shared.Contracts.DTOs.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Backup.Server.Api.Security;

public class TokenService
{
    private readonly JwtOptions _jwtOptions;

    public TokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    public AuthResponse CreateUserAuthResponse(AppUser user)
    {
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtOptions.UserTokenLifetimeMinutes);
        var token = CreateToken(
            user.Id,
            user.Username,
            MapUserRole(user.Role),
            AuthConstants.UserTokenType,
            expiresAtUtc);

        return new AuthResponse(
            token,
            expiresAtUtc,
            new CurrentUserResponse(user.Id, user.Username, MapUserRole(user.Role)));
    }

    public string CreateAgentToken(Agent agent)
    {
        var expiresAtUtc = DateTime.UtcNow.AddDays(_jwtOptions.AgentTokenLifetimeDays);
        return CreateToken(
            agent.Id,
            agent.MachineName,
            AuthConstants.AgentRole,
            AuthConstants.AgentTokenType,
            expiresAtUtc);
    }

    private string CreateToken(
        Guid subjectId,
        string subjectName,
        string role,
        string tokenType,
        DateTime expiresAtUtc)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subjectId.ToString()),
            new(ClaimTypes.Name, subjectName),
            new(ClaimTypes.Role, role),
            new(AuthConstants.TokenTypeClaim, tokenType)
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAtUtc,
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            SigningCredentials = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }

    private static string MapUserRole(AppUserRole role)
    {
        return role switch
        {
            AppUserRole.Viewer => AuthConstants.ViewerRole,
            AppUserRole.Operator => AuthConstants.OperatorRole,
            AppUserRole.Admin => AuthConstants.AdminRole,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
        };
    }
}

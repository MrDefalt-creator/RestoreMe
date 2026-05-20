using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Backup.Server.Api.HealthChecks;
using Backup.Server.Api.Security;
using Backup.Server.Api.Services;
using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Configuration;
using Backup.Server.Infrastructure.Options;
using Backup.Server.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Minio;

var builder = WebApplication.CreateBuilder(args);

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                  ?? [
                      "http://localhost:5173",
                      "http://127.0.0.1:5173",
                      "https://localhost:5173",
                      "https://127.0.0.1:5173",
                      "http://localhost:4173",
                      "http://127.0.0.1:4173",
                      "https://localhost:4173",
                      "https://127.0.0.1:4173"
                  ];

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendClient", policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod();

        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin => IsAllowedDevelopmentOrigin(origin, corsOrigins));
            return;
        }

        policy.WithOrigins(corsOrigins);
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services
    .AddOptions<AgentEnrollmentOptions>()
    .Bind(builder.Configuration.GetSection(AgentEnrollmentOptions.SectionName));
builder.Services
    .AddOptions<SecuritySeedOptions>()
    .Bind(builder.Configuration.GetSection(SecuritySeedOptions.SectionName));

var connectionString = ResolveConfigValue(
    builder.Configuration,
    "ConnectionStrings:DefaultConnection",
    "ConnectionStrings:DefaultConnection_FILE");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured and no secret file path was provided.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<AgentService>();
builder.Services.AddScoped<PoliciesService>();
builder.Services.AddScoped<BackupJobsService>();
builder.Services.AddScoped<BackupArtifactsService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<UsersService>();
builder.Services.AddScoped<SecuritySeedService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

builder.Services.AddScoped<IAgentRepository, AgentRepository>();
builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();
builder.Services.AddScoped<IPolicyRepository, PolicyRepository>();
builder.Services.AddScoped<IPendingAgentsRepository, PendingAgentsRepository>();
builder.Services.AddScoped<IBackupJobRepository, BackupJobRepository>();
builder.Services.AddScoped<IBackupArtifactRepository, BackupArtifactRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IRestoreJobRepository, RestoreJobRepository>();
builder.Services.AddScoped<RestoreJobsService>();

builder.Services
    .AddOptions<NotificationOptions>()
    .Bind(builder.Configuration.GetSection(NotificationOptions.SectionName));
builder.Services.AddHttpClient<INotificationService, WebhookNotificationService>();
builder.Services.AddScoped<IStorageAccessService, StorageAccessService>();
builder.Services.AddHostedService<RetentionCleanupService>();
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
ValidateProductionConfiguration(builder.Configuration, builder.Environment, jwtOptions);
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrEmpty(context.Token) &&
                    context.Request.Cookies.TryGetValue("access_token", out var cookieToken))
                {
                    context.Token = cookieToken;
                }
                return Task.CompletedTask;
            }
        };
    })
    .AddScheme<AuthenticationSchemeOptions, AgentEnrollmentAuthenticationHandler>(
        AuthConstants.AgentEnrollmentScheme,
        _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthConstants.AdminReadPolicy, policy =>
        policy.RequireAuthenticatedUser()
            .RequireClaim(AuthConstants.TokenTypeClaim, AuthConstants.UserTokenType)
            .RequireRole(AuthConstants.ViewerRole, AuthConstants.OperatorRole, AuthConstants.AdminRole));

    options.AddPolicy(AuthConstants.AdminWritePolicy, policy =>
        policy.RequireAuthenticatedUser()
            .RequireClaim(AuthConstants.TokenTypeClaim, AuthConstants.UserTokenType)
            .RequireRole(AuthConstants.OperatorRole, AuthConstants.AdminRole));

    options.AddPolicy(AuthConstants.UserManagementPolicy, policy =>
        policy.RequireAuthenticatedUser()
            .RequireClaim(AuthConstants.TokenTypeClaim, AuthConstants.UserTokenType)
            .RequireRole(AuthConstants.AdminRole));

    options.AddPolicy(AuthConstants.AgentPolicy, policy =>
        policy.RequireAuthenticatedUser()
            .RequireClaim(AuthConstants.TokenTypeClaim, AuthConstants.AgentTokenType)
            .RequireRole(AuthConstants.AgentRole));

    options.AddPolicy(AuthConstants.AgentEnrollmentPolicy, policy =>
        policy.AddAuthenticationSchemes(AuthConstants.AgentEnrollmentScheme)
            .RequireAuthenticatedUser());
});

builder.Services
    .AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
    .PostConfigure(options =>
    {
        options.AccessKey = ResolveConfigValue(
            builder.Configuration,
            "Storage:AccessKey",
            "Storage:AccessKey_FILE");

        options.SecretKey = ResolveConfigValue(
            builder.Configuration,
            "Storage:SecretKey",
            "Storage:SecretKey_FILE");
    });

builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var storageOptions = sp.GetRequiredService<IOptions<StorageOptions>>().Value;

    return new MinioClient()
        .WithEndpoint(storageOptions.Endpoint)
        .WithCredentials(storageOptions.AccessKey, storageOptions.SecretKey)
        .WithSSL(storageOptions.UseSsl)
        .Build();
});

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(name: "database")
    .AddCheck<MinioHealthCheck>("minio");

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 10,
                SegmentsPerWindow = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Too many login attempts. Please try again later." },
            cancellationToken);
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("FrontendClient");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

await ApplyMigrationsAsync(app);
await EnsureSecuritySeedAsync(app);
await app.RunAsync();

static string ResolveConfigValue(
    IConfiguration configuration,
    string valueKey,
    string fileKey)
{
    var filePath = configuration[fileKey];
    if (!string.IsNullOrWhiteSpace(filePath))
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Secret file was not found for '{fileKey}'.",
                filePath);
        }

        return File.ReadAllText(filePath).Trim();
    }

    return configuration[valueKey] ?? string.Empty;
}

static bool IsAllowedDevelopmentOrigin(
    string? origin,
    IEnumerable<string> configuredOrigins)
{
    if (string.IsNullOrWhiteSpace(origin))
    {
        return false;
    }

    if (configuredOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
    {
        return true;
    }

    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return false;
    }

    if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address);
}

static void ValidateProductionConfiguration(
    IConfiguration configuration,
    IWebHostEnvironment environment,
    JwtOptions jwtOptions)
{
    if (environment.IsDevelopment())
    {
        return;
    }

    if (string.Equals(jwtOptions.SigningKey, "ChangeMe-This-Is-Not-A-Secure-Production-Key", StringComparison.Ordinal) ||
        string.Equals(jwtOptions.SigningKey, "RestoreMe-Development-Only-Replace-Before-Production-12345", StringComparison.Ordinal) ||
        Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
    {
        throw new InvalidOperationException("Production JWT signing key must be configured with a strong secret.");
    }

    var enrollmentToken = configuration["AgentEnrollment:EnrollmentToken"];
    if (string.IsNullOrWhiteSpace(enrollmentToken) ||
        string.Equals(enrollmentToken, "change-me-enrollment-token", StringComparison.Ordinal) ||
        string.Equals(enrollmentToken, "restoreme-agent-enrollment-dev-token", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Production agent enrollment token must be configured with a non-default secret.");
    }
}

static async Task ApplyMigrationsAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");

    logger.LogInformation("Applying database migrations...");
    await dbContext.Database.MigrateAsync();
    logger.LogInformation("Database migrations applied successfully.");
}

static async Task EnsureSecuritySeedAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var seeder = scope.ServiceProvider.GetRequiredService<SecuritySeedService>();
    await seeder.EnsureSeedUsersAsync();
}

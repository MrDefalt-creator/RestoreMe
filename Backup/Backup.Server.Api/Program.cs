using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Backup.Server.Api.Filters;
using Backup.Server.Api.HealthChecks;
using Backup.Server.Api.HostedServices;
using Backup.Server.Api.Middleware;
using Backup.Server.Api.Security;
using Backup.Server.Api.Services;
using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Application.Notifications;
using Backup.Server.Infrastructure.Configuration;
using Backup.Server.Domain.Options;
using Backup.Server.Infrastructure.Options;
using Backup.Server.Infrastructure.Services;
using Backup.Server.Infrastructure.Services.Adapters;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
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
builder.Services.AddProblemDetails();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ExceptionToStatusFilter>();
});

// Persist DataProtection keys so any payload that uses Protect/Unprotect
// (antiforgery in the future, encrypted cookies, etc.) keeps working
// across container restarts. SetApplicationName isolates the key ring
// from the agent and from other instances sharing the volume.
var serverKeysDir = Path.Combine(AppContext.BaseDirectory, "keys");
Directory.CreateDirectory(serverKeysDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(serverKeysDir))
    .SetApplicationName("RestoreMe.Server");
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendClient", policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            // The frontend authenticates via an httpOnly access_token cookie
            // (see AuthController.Login + axios withCredentials:true). Browsers
            // require Access-Control-Allow-Credentials:true to accept the
            // Set-Cookie response and to attach the cookie on subsequent
            // cross-origin requests. Without this the entire login flow fails
            // silently with "preflight does not pass access control check".
            .AllowCredentials()
            // Browsers cache CORS preflight responses up to PreflightMaxAge;
            // every cached preflight saves one RTT before the real request.
            // 10 minutes matches typical session lifetimes without holding
            // stale CORS state for too long after a config change.
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));

        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin => IsAllowedDevelopmentOrigin(origin, corsOrigins));
            return;
        }

        policy.WithOrigins(corsOrigins);
    });
});

// Lock the JSON request body to 64 KiB. Backup payloads upload directly
// to MinIO via presigned URLs — none of the API endpoints take more
// than a small JSON envelope, so any body bigger than this is a DoS
// signal rather than a legitimate request.
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 64 * 1024;
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
builder.Services.AddScoped<DashboardMetricsService>();
builder.Services.AddScoped<DashboardSummaryService>();
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
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<IRestoreJobRepository, RestoreJobRepository>();
builder.Services.AddScoped<IAgentInstallTokenRepository, AgentInstallTokenRepository>();
builder.Services.AddScoped<INotificationChannelRepository, NotificationChannelRepository>();
builder.Services.AddScoped<IIntegrityScrubSettingsRepository, IntegrityScrubSettingsRepository>();
builder.Services.AddScoped<AgentInstallTokenService>();
builder.Services.AddHostedService<AgentInstallTokenCleanupService>();
builder.Services.AddScoped<RestoreJobsService>();

// One HttpClient timeout caps every adapter the same way — slow chat
// platforms can't stall the failure-reporting path. Each adapter gets
// its own typed client so HttpClientFactory can pool connections per
// destination (Telegram, Slack, Discord all sit on different hosts).
static void CapAdapterTimeout(HttpClient client)
{
    client.Timeout = TimeSpan.FromSeconds(10);
}

builder.Services.AddHttpClient<INotificationChannelAdapter, GenericWebhookAdapter>(CapAdapterTimeout);
builder.Services.AddHttpClient<INotificationChannelAdapter, TelegramAdapter>(CapAdapterTimeout);
builder.Services.AddHttpClient<INotificationChannelAdapter, SlackAdapter>(CapAdapterTimeout);
builder.Services.AddHttpClient<INotificationChannelAdapter, DiscordAdapter>(CapAdapterTimeout);
builder.Services.AddScoped<INotificationService, NotificationDispatcher>();
builder.Services.AddScoped<NotificationDispatcher>();
builder.Services.AddScoped<NotificationChannelsService>();
builder.Services.AddScoped<IntegritySettingsService>();
builder.Services.AddScoped<AgentHealthService>();
builder.Services.AddHostedService<AgentHealthSweepService>();
builder.Services.AddSingleton<BucketReadyState>();
// In-process fan-out to connected admin SSE streams (EventsController).
// Singleton so every scoped service publishes into the same subscriber set.
builder.Services.AddSingleton<IAdminEventBroadcaster, AdminEventBroadcaster>();
builder.Services.AddScoped<IStorageAccessService, StorageAccessService>();
builder.Services.AddHostedService<MinioBucketInitializer>();
builder.Services.AddHostedService<RetentionCleanupService>();
builder.Services.AddHostedService<IntegrityScrubService>();
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
ValidateProductionConfiguration(builder.Configuration, builder.Environment, jwtOptions, corsOrigins);
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
var agentSigningKey = !string.IsNullOrWhiteSpace(jwtOptions.AgentSigningKey)
    ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.AgentSigningKey))
    : signingKey;

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
            // Pick the key by inspecting the token_type claim *before*
            // verifying the signature. Reading the claim from the unverified
            // payload is fine — the wrong key will simply make the signature
            // check fail in the next pipeline step.
            IssuerSigningKeyResolver = (token, _, _, _) =>
            {
                try
                {
                    var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
                    var tokenType = jwt.Claims.FirstOrDefault(c => c.Type == AuthConstants.TokenTypeClaim)?.Value;
                    return tokenType == AuthConstants.AgentTokenType
                        ? [agentSigningKey]
                        : [signingKey];
                }
                catch
                {
                    return [signingKey, agentSigningKey];
                }
            },
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
            },
            OnTokenValidated = Backup.Server.Api.Security.SecurityStampValidator.ValidateAsync,
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

builder.Services
    .AddOptions<RetentionOptions>()
    .Bind(builder.Configuration.GetSection(RetentionOptions.SectionName));

builder.Services
    .AddOptions<IntegrityOptions>()
    .Bind(builder.Configuration.GetSection(IntegrityOptions.SectionName));

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

builder.Services.AddMemoryCache();

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

    // Authenticated agent endpoints (heartbeat, job completion, upload
    // tickets, etc.). Per-agent partition keyed off the JWT subject so a
    // compromised agent token can't DoS the backend for the whole fleet.
    options.AddPolicy("agent-write", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.User.TryGetAgentId()?.ToString()
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 60,
                SegmentsPerWindow = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    // Enrollment endpoints (anonymous + only bound by the shared/install
    // token in a header). Per-IP partition. Tighter than agent-write
    // because legitimate use is just a few requests per agent during
    // first-time setup.
    options.AddPolicy("enrollment-public", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 20,
                SegmentsPerWindow = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    // Per-user policy for admin-minted install tokens. A compromised
    // admin session shouldn't be able to mint hundreds of tokens
    // instantly.
    options.AddPolicy("install-token-create", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.User.TryGetUserId()?.ToString()
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 10,
                SegmentsPerWindow = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too Many Requests",
                Detail = "Too many requests. Please slow down and try again."
            },
            options: null,
            contentType: "application/problem+json",
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

if (app.Environment.IsProduction())
{
    // HSTS pin: 30 days, includes subdomains. Operators terminating TLS
    // at a reverse proxy (typical RestoreMe deployment) get the header
    // forwarded to the browser. If Kestrel is exposed directly with TLS,
    // UseHttpsRedirection upgrades any naked-HTTP request.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors("FrontendClient");
// Serve the install-agent scripts (baked into wwwroot at image build time)
// and the agent binaries (mounted from the `agent_binaries` volume in
// compose) anonymously. Placed BEFORE UseAuthentication so the install
// wizard one-liner doesn't need an auth header to fetch the .ps1/.sh,
// and so a fresh agent host has no chicken-and-egg auth requirement
// before it has a token. `ServeUnknownFileTypes=true` lets us hand out
// the binary files (no extension on Linux) with octet-stream.
app.UseStaticFiles(new StaticFileOptions
{
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream",
});
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
    JwtOptions jwtOptions,
    string[] corsOrigins)
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

    // If operator opted into a dedicated agent key, validate it the same way.
    // An empty value is legitimate — it just means agents share SigningKey.
    if (!string.IsNullOrWhiteSpace(jwtOptions.AgentSigningKey))
    {
        if (string.Equals(jwtOptions.AgentSigningKey, jwtOptions.SigningKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Jwt:AgentSigningKey must differ from Jwt:SigningKey when configured.");
        }

        if (Encoding.UTF8.GetByteCount(jwtOptions.AgentSigningKey) < 32)
        {
            throw new InvalidOperationException("Production Jwt:AgentSigningKey must be at least 32 bytes.");
        }
    }

    var enrollmentToken = configuration["AgentEnrollment:EnrollmentToken"];
    if (string.IsNullOrWhiteSpace(enrollmentToken) ||
        string.Equals(enrollmentToken, "change-me-enrollment-token", StringComparison.Ordinal) ||
        string.Equals(enrollmentToken, "restoreme-agent-enrollment-dev-token", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Production agent enrollment token must be configured with a non-default secret.");
    }

    if (corsOrigins.Length == 0)
    {
        throw new InvalidOperationException(
            "Production Cors:AllowedOrigins must be configured explicitly (e.g. via CORS_ORIGIN in docker-compose.prod.yml).");
    }

    if (corsOrigins.Any(IsLoopbackOrigin))
    {
        throw new InvalidOperationException(
            "Production Cors:AllowedOrigins must not contain loopback addresses (localhost/127.0.0.1).");
    }
}

static bool IsLoopbackOrigin(string origin)
{
    if (string.IsNullOrWhiteSpace(origin))
    {
        return false;
    }

    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
    {
        return origin.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
               origin.Contains("127.0.0.1", StringComparison.Ordinal);
    }

    if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    return IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address);
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

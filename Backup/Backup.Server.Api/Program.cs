using System.Net;
using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Infrastructure.Configuration;
using Backup.Server.Infrastructure.Options;
using Backup.Server.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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

builder.Services.AddScoped<IAgentRepository, AgentRepository>();
builder.Services.AddScoped<IPolicyRepository, PolicyRepository>();
builder.Services.AddScoped<IPendingAgentsRepository, PendingAgentsRepository>();
builder.Services.AddScoped<IBackupJobRepository, BackupJobRepository>();
builder.Services.AddScoped<IBackupArtifactRepository, BackupArtifactRepository>();
builder.Services.AddScoped<IStorageAccessService, StorageAccessService>();

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("FrontendClient");
app.MapControllers();

await ApplyMigrationsAsync(app);
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

static async Task ApplyMigrationsAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");

    logger.LogInformation("Applying database migrations...");
    await dbContext.Database.MigrateAsync();
    logger.LogInformation("Database migrations applied successfully.");
}

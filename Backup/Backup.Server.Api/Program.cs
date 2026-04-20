using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Infrastructure.Configuration;
using Backup.Server.Infrastructure.Options;
using Backup.Server.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Minio;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"))
);

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

builder.Services.AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName));


builder.Services.AddSingleton<IMinioClient>(sp =>
{
    var storageOptions = sp.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<StorageOptions>>().Value;

    return new MinioClient()
        .WithEndpoint(storageOptions.Endpoint)
        .WithCredentials(storageOptions.AccessKey, storageOptions.SecretKey)
        .WithSSL(storageOptions.UseSsl)
        .Build();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseHttpsRedirection();
app.MapControllers();

app.Run();
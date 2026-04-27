using Backup.Agent.Worker;
using Backup.Agent.Worker.ApiClients;
using Backup.Agent.Worker.Interfaces;
using Backup.Agent.Worker.Options;
using Backup.Agent.Worker.Services;
using Backup.Agent.Worker.State;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<ApiOptions>().Bind(builder.Configuration.GetSection(ApiOptions.SectionName));
builder.Services.AddOptions<AgentOptions>().Bind(builder.Configuration.GetSection(AgentOptions.SectionName));

builder.Services.AddSingleton<IAgentState, FileAgentStore>();
builder.Services.AddSingleton<IApiEndpointResolver, ApiEndpointResolver>();

builder.Services.AddHttpClient<IAgentApiClient, AgentApiClient>((sp, client) =>
{
    var apiEndpointResolver = sp.GetRequiredService<IApiEndpointResolver>();
    var resolvedEndpoint = apiEndpointResolver.ResolveAsync(CancellationToken.None).GetAwaiter().GetResult();
    client.BaseAddress = new Uri(resolvedEndpoint.BaseUrl);
});

builder.Services.AddHttpClient<IBackupApiClient, BackupApiClient>((sp, client) =>
    {
        var apiEndpointResolver = sp.GetRequiredService<IApiEndpointResolver>();
        var resolvedEndpoint = apiEndpointResolver.ResolveAsync(CancellationToken.None).GetAwaiter().GetResult();
        client.BaseAddress = new Uri(resolvedEndpoint.BaseUrl);
    }
);

builder.Services.AddHttpClient<IMinioStorageClient, MinioStorageClient>();
builder.Services.AddSingleton<IArchiveService, ArchiveService>();
builder.Services.AddSingleton<IChecksumService, ChecksumService>();
builder.Services.AddSingleton<ILogicalBackupService, LogicalBackupService>();
builder.Services.AddTransient<IBackupExecutor, BackupExecuter>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

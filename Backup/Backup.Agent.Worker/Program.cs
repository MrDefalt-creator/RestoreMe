using Backup.Agent.Worker;
using Backup.Agent.Worker.Options;
using Backup.Agent.Worker.Services;
using Backup.Agent.Worker.State;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<ApiOptions>().Bind(builder.Configuration.GetSection(ApiOptions.SectionName));
builder.Services.AddOptions<AgentOptions>().Bind(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.AddHttpClient<IAgentApiClient, AgentApiClient>((sp, client) =>
{
    var apiOptions = sp
        .GetRequiredService<IOptions<ApiOptions>>()
        .Value;

    if (string.IsNullOrWhiteSpace(apiOptions.BaseUrl))
    {
        throw new InvalidOperationException("Api:BaseUrl is not configured.");
    }

    client.BaseAddress = new Uri(apiOptions.BaseUrl);
});

builder.Services.AddSingleton<IAgentState, FileAgentStore>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

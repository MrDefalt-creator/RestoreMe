using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Shared.Contracts.DTOs.Policies;

namespace Backup.Server.Application.Services;

public class PoliciesService
{
    private readonly IPolicyRepository _policyRepository;
    public PoliciesService(IPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository;
    }

    public async Task<BackupPolicy> CreatePolicy(
        Guid agentId,
        string type,
        string name,
        string? sourcePath,
        int interval,
        BackupPolicyDatabaseSettingsDto? databaseSettingsDto)
    {
        name = name.Trim();
        var policyType = ParsePolicyType(type);
        sourcePath = NormalizeSourcePath(policyType, sourcePath);

        var policy = await _policyRepository.GetPolicyByName(agentId, name);

        if (policy != null)
        {
            throw new InvalidOperationException("Policy with the same name already exists for this agent.");
        }

        policy = new BackupPolicy
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            Type = policyType,
            Name = name,
            SourcePath = sourcePath,
            IntervalSeconds =  interval,
            NextRunAt = DateTime.UtcNow
        };

        policy.DatabaseSettings = BuildDatabaseSettings(policyType, databaseSettingsDto, policy.Id);
        
        await _policyRepository.AddPolicy(policy);
        
        await _policyRepository.SaveChangesAsync();

        return policy;
    }

    public async Task<List<BackupPolicy>> GetAllPolicies(Guid agentId)
    {
        var policies = await _policyRepository.GetAllPolicies(agentId);
        
        return policies;
    }
    
    public async Task<List<BackupPolicy>> GetAllPolicies()
    {
        return await _policyRepository.GetAllPoliciesAsync();
    }

    public async Task<BackupPolicy> GetPolicyById(Guid policyId)
    {
        var policy = await _policyRepository.GetPolicyById(policyId);

        if (policy == null)
        {
            throw new KeyNotFoundException("Policy not found");
        }
        
        return policy;
    }
    
    public async Task<BackupPolicy> UpdatePolicy(
        Guid policyId,
        Guid agentId,
        string type,
        string name,
        string? sourcePath,
        int intervalSeconds,
        bool isEnabled,
        BackupPolicyDatabaseSettingsDto? databaseSettingsDto)
    {
        var policy = await _policyRepository.GetPolicyById(policyId);
        if (policy == null)
        {
            throw new KeyNotFoundException("Policy not found");
        }
        
        var policyType = ParsePolicyType(type);
        sourcePath = NormalizeSourcePath(policyType, sourcePath);
        
        policy.AgentId = agentId;
        policy.Type = policyType;
        policy.Name = name.Trim();
        policy.SourcePath = sourcePath;
        policy.IntervalSeconds = intervalSeconds;
        policy.IsEnabled = isEnabled;
        policy.NextRunAt = DateTime.UtcNow.AddSeconds(intervalSeconds);
        policy.DatabaseSettings = BuildDatabaseSettings(policyType, databaseSettingsDto, policy.Id);
        
        await _policyRepository.UpdatePolicy(policy);
        await _policyRepository.SaveChangesAsync();

        return policy;
    }
    
    public async Task<BackupPolicy> TogglePolicy(Guid policyId)
    {
        var policy = await _policyRepository.GetPolicyById(policyId);
        if (policy == null)
        {
            throw new KeyNotFoundException("Policy not found");
        }
        
        policy.IsEnabled = !policy.IsEnabled;
        
        await _policyRepository.UpdatePolicy(policy);
        await _policyRepository.SaveChangesAsync();

        return policy;
    }
    
    public async Task MarkPolicyExecuted(Guid policyId)
    {
        var policy = await _policyRepository.GetPolicyById(policyId);

        if (policy == null)
        {
            throw new KeyNotFoundException("Policy not found");
        }
        
        policy.LastRunAt = DateTime.UtcNow;
        policy.NextRunAt = DateTime.UtcNow.AddSeconds(policy.IntervalSeconds);
        await _policyRepository.UpdatePolicy(policy);
        await _policyRepository.SaveChangesAsync();
    }
    
    private static string NormalizeSourcePath(BackupPolicyType policyType, string? path)
    {
        if (policyType != BackupPolicyType.FileSystem)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Source path is required for filesystem policies.");
        }

        path = path.Trim();
        path = path.Replace('\\', '/');

        while (path.Contains("//"))
            path = path.Replace("//", "/");

        return path;
    }

    private static BackupPolicyType ParsePolicyType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return BackupPolicyType.FileSystem;
        }

        return type.Trim().ToLowerInvariant() switch
        {
            "filesystem" => BackupPolicyType.FileSystem,
            "postgres" or "postgresql" or "postgresqldump" => BackupPolicyType.PostgreSqlDump,
            "mysql" or "mysqldump" => BackupPolicyType.MySqlDump,
            _ => throw new InvalidOperationException($"Unsupported policy type '{type}'.")
        };
    }

    private static BackupPolicyDatabaseSettings? BuildDatabaseSettings(
        BackupPolicyType policyType,
        BackupPolicyDatabaseSettingsDto? dto,
        Guid policyId)
    {
        if (policyType == BackupPolicyType.FileSystem)
        {
            return null;
        }

        if (dto == null)
        {
            throw new InvalidOperationException("Database settings are required for logical database backup policies.");
        }

        var engine = ParseDatabaseEngine(dto.Engine);
        ValidateEngineMatchesPolicyType(policyType, engine);

        var authMode = ParseAuthMode(dto.AuthMode);
        var host = string.IsNullOrWhiteSpace(dto.Host) ? null : dto.Host.Trim();
        var databaseName = dto.DatabaseName?.Trim();
        var username = string.IsNullOrWhiteSpace(dto.Username) ? null : dto.Username.Trim();
        var password = string.IsNullOrWhiteSpace(dto.Password) ? null : dto.Password;

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("Database name is required for logical database backup policies.");
        }

        if (engine == DatabaseEngine.MySql && authMode != DatabaseDumpAuthMode.Credentials)
        {
            throw new InvalidOperationException("MySQL logical backups currently require credentials authentication mode.");
        }

        if (authMode == DatabaseDumpAuthMode.Credentials)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new InvalidOperationException("Username is required when credentials authentication mode is selected.");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("Password is required when credentials authentication mode is selected.");
            }
        }

        return new BackupPolicyDatabaseSettings
        {
            PolicyId = policyId,
            Engine = engine,
            AuthMode = authMode,
            Host = host,
            Port = dto.Port,
            DatabaseName = databaseName,
            Username = username,
            Password = password
        };
    }

    private static DatabaseEngine ParseDatabaseEngine(string engine)
    {
        return engine.Trim().ToLowerInvariant() switch
        {
            "postgres" or "postgresql" => DatabaseEngine.PostgreSql,
            "mysql" => DatabaseEngine.MySql,
            _ => throw new InvalidOperationException($"Unsupported database engine '{engine}'.")
        };
    }

    private static DatabaseDumpAuthMode ParseAuthMode(string authMode)
    {
        return authMode.Trim().ToLowerInvariant() switch
        {
            "integrated" => DatabaseDumpAuthMode.Integrated,
            "credentials" => DatabaseDumpAuthMode.Credentials,
            _ => throw new InvalidOperationException($"Unsupported database auth mode '{authMode}'.")
        };
    }

    private static void ValidateEngineMatchesPolicyType(BackupPolicyType policyType, DatabaseEngine engine)
    {
        if (policyType == BackupPolicyType.PostgreSqlDump && engine != DatabaseEngine.PostgreSql)
        {
            throw new InvalidOperationException("PostgreSQL policy type requires PostgreSQL database settings.");
        }

        if (policyType == BackupPolicyType.MySqlDump && engine != DatabaseEngine.MySql)
        {
            throw new InvalidOperationException("MySQL policy type requires MySQL database settings.");
        }
    }
}

using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Shared.Contracts.DTOs.Policies;

namespace Backup.Server.Application.Services;

public class PoliciesService
{
    private readonly IPolicyRepository _policyRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IAdminEventBroadcaster _eventBroadcaster;

    public PoliciesService(
        IPolicyRepository policyRepository,
        IAuditLogRepository auditLogRepository,
        IAdminEventBroadcaster eventBroadcaster)
    {
        _policyRepository = policyRepository;
        _auditLogRepository = auditLogRepository;
        _eventBroadcaster = eventBroadcaster;
    }

    public async Task<BackupPolicy> CreatePolicy(
        Guid agentId,
        string type,
        string name,
        string? sourcePath,
        PolicyScheduleInput schedule,
        BackupPolicyDatabaseSettingsDto? databaseSettingsDto,
        int? retentionDays,
        int? retentionMaxCount,
        long? retentionMaxTotalBytes,
        Guid actorUserId)
    {
        var normalized = PolicyScheduleValidator.Validate(schedule);

        name = name.Trim();
        var policyType = ParsePolicyType(type);
        sourcePath = NormalizeSourcePath(policyType, sourcePath);
        ValidateRetention(retentionDays, retentionMaxCount, retentionMaxTotalBytes);

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
            ScheduleKind = normalized.Kind,
            IntervalSeconds = normalized.IntervalSeconds,
            CronExpression = normalized.CronExpression,
            TimeZoneId = normalized.TimeZoneId,
            WindowStartMinutes = normalized.WindowStartMinutes,
            WindowEndMinutes = normalized.WindowEndMinutes,
            RetentionDays = retentionDays,
            RetentionMaxCount = retentionMaxCount,
            RetentionMaxTotalBytes = retentionMaxTotalBytes
        };
        policy.NextRunAt = PolicyScheduleCalculator.ComputeFirstRun(policy, DateTime.UtcNow);

        policy.DatabaseSettings = BuildDatabaseSettings(policyType, databaseSettingsDto, policy.Id);

        await _policyRepository.AddPolicy(policy);
        await _auditLogRepository.AddAsync(Audit(
            actorUserId,
            "policy.create",
            policy.Id,
            $"agent={agentId} name={policy.Name} type={MapPolicyTypeForAudit(policyType)} schedule={normalized.Kind} interval={normalized.IntervalSeconds} cron={normalized.CronExpression}"));

        await _policyRepository.SaveChangesAsync();

        _eventBroadcaster.Publish(AdminEventTopic.Policies);

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
        PolicyScheduleInput schedule,
        bool isEnabled,
        BackupPolicyDatabaseSettingsDto? databaseSettingsDto,
        int? retentionDays,
        int? retentionMaxCount,
        long? retentionMaxTotalBytes,
        Guid actorUserId)
    {
        var policy = await _policyRepository.GetPolicyById(policyId);
        if (policy == null)
        {
            throw new KeyNotFoundException("Policy not found");
        }

        var normalized = PolicyScheduleValidator.Validate(schedule);

        var policyType = ParsePolicyType(type);
        sourcePath = NormalizeSourcePath(policyType, sourcePath);
        ValidateRetention(retentionDays, retentionMaxCount, retentionMaxTotalBytes);

        var reEnabling = !policy.IsEnabled && isEnabled;

        policy.AgentId = agentId;
        policy.Type = policyType;
        policy.Name = name.Trim();
        policy.SourcePath = sourcePath;
        policy.ScheduleKind = normalized.Kind;
        policy.IntervalSeconds = normalized.IntervalSeconds;
        policy.CronExpression = normalized.CronExpression;
        policy.TimeZoneId = normalized.TimeZoneId;
        policy.WindowStartMinutes = normalized.WindowStartMinutes;
        policy.WindowEndMinutes = normalized.WindowEndMinutes;
        policy.IsEnabled = isEnabled;
        policy.NextRunAt = PolicyScheduleCalculator.ComputeNextRun(policy, DateTime.UtcNow);
        policy.RetentionDays = retentionDays;
        policy.RetentionMaxCount = retentionMaxCount;
        policy.RetentionMaxTotalBytes = retentionMaxTotalBytes;
        policy.DatabaseSettings = BuildDatabaseSettings(policyType, databaseSettingsDto, policy.Id, policy.DatabaseSettings);

        if (reEnabling)
        {
            policy.ConsecutiveFailureCount = 0;
            policy.LastFailureReason = null;
            policy.AutoDisabledAt = null;
        }

        await _policyRepository.UpdatePolicy(policy);
        await _auditLogRepository.AddAsync(Audit(
            actorUserId,
            "policy.update",
            policy.Id,
            $"name={policy.Name} type={MapPolicyTypeForAudit(policyType)} enabled={isEnabled}"));
        await _policyRepository.SaveChangesAsync();

        _eventBroadcaster.Publish(AdminEventTopic.Policies);

        return policy;
    }

    public async Task<BackupPolicy> TogglePolicy(Guid policyId, Guid actorUserId)
    {
        var policy = await _policyRepository.GetPolicyById(policyId);
        if (policy == null)
        {
            throw new KeyNotFoundException("Policy not found");
        }

        policy.IsEnabled = !policy.IsEnabled;

        // Manual re-enable acts as the operator's "I fixed it" signal —
        // clear the auto-disable bookkeeping so the next failure starts
        // a fresh streak instead of immediately tripping the threshold.
        if (policy.IsEnabled)
        {
            policy.ConsecutiveFailureCount = 0;
            policy.LastFailureReason = null;
            policy.AutoDisabledAt = null;
        }

        await _policyRepository.UpdatePolicy(policy);
        await _auditLogRepository.AddAsync(Audit(
            actorUserId,
            "policy.toggle",
            policy.Id,
            $"name={policy.Name} enabled={policy.IsEnabled}"));
        await _policyRepository.SaveChangesAsync();

        _eventBroadcaster.Publish(AdminEventTopic.Policies);

        return policy;
    }

    public async Task DeletePolicy(Guid policyId, Guid actorUserId)
    {
        var policy = await _policyRepository.GetPolicyById(policyId)
            ?? throw new KeyNotFoundException("Policy not found");

        await _policyRepository.DeletePolicy(policy);
        await _auditLogRepository.AddAsync(Audit(
            actorUserId,
            "policy.delete",
            policy.Id,
            $"name={policy.Name} agent={policy.AgentId}"));
        await _policyRepository.SaveChangesAsync();

        _eventBroadcaster.Publish(AdminEventTopic.Policies);
    }

    public async Task MarkPolicyExecuted(Guid policyId)
    {
        var policy = await _policyRepository.GetPolicyById(policyId);

        if (policy == null)
        {
            throw new KeyNotFoundException("Policy not found");
        }

        policy.LastRunAt = DateTime.UtcNow;
        policy.NextRunAt = PolicyScheduleCalculator.ComputeNextRun(policy, DateTime.UtcNow);
        await _policyRepository.UpdatePolicy(policy);
        await _policyRepository.SaveChangesAsync();
    }

    internal static string NormalizeSourcePath(BackupPolicyType policyType, string? path)
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

        if (path.Split('/').Any(segment => segment == ".."))
            throw new InvalidOperationException("Source path must not contain directory traversal sequences.");

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

    private static string MapPolicyTypeForAudit(BackupPolicyType type) => type switch
    {
        BackupPolicyType.FileSystem => "filesystem",
        BackupPolicyType.PostgreSqlDump => "postgres",
        BackupPolicyType.MySqlDump => "mysql",
        _ => type.ToString().ToLowerInvariant()
    };

    private static AuditLog Audit(Guid actorId, string action, Guid? targetId = null, string? details = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            Action = action,
            TargetId = targetId,
            Details = details,
            OccurredAt = DateTime.UtcNow
        };

    private static BackupPolicyDatabaseSettings? BuildDatabaseSettings(
        BackupPolicyType policyType,
        BackupPolicyDatabaseSettingsDto? dto,
        Guid policyId,
        BackupPolicyDatabaseSettings? existingSettings = null)
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
        var password = string.IsNullOrWhiteSpace(dto.Password) ? existingSettings?.Password : dto.Password;

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
        else
        {
            password = null;
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

    private static void ValidateRetention(int? retentionDays, int? retentionMaxCount, long? retentionMaxTotalBytes)
    {
        if (retentionDays.HasValue && retentionDays.Value < 1)
        {
            throw new InvalidOperationException("Retention days must be at least 1 when set.");
        }

        if (retentionMaxCount.HasValue && retentionMaxCount.Value < 1)
        {
            throw new InvalidOperationException("Retention max count must be at least 1 when set.");
        }

        if (retentionMaxTotalBytes.HasValue && retentionMaxTotalBytes.Value < 1)
        {
            throw new InvalidOperationException("Retention max total bytes must be at least 1 when set.");
        }
    }
}

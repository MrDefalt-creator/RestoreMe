using Backup.Server.Application.Services;
using Backup.Server.Domain.Enums;

namespace Backup.Server.Tests.Security;

public class NormalizeSourcePathTests
{
    [Theory]
    [InlineData("/var/log/app", "/var/log/app")]
    [InlineData("/var//log///app", "/var/log/app")]
    [InlineData("C:\\Data\\Backups", "C:/Data/Backups")]
    public void Filesystem_paths_are_normalised(string input, string expected)
    {
        var actual = PoliciesService.NormalizeSourcePath(BackupPolicyType.FileSystem, input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("/var/log/../../etc/passwd")]
    [InlineData("..")]
    [InlineData("foo/../bar")]
    [InlineData("..\\..\\Windows\\System32")]
    public void Filesystem_paths_with_traversal_are_rejected(string input)
    {
        Assert.Throws<InvalidOperationException>(() =>
            PoliciesService.NormalizeSourcePath(BackupPolicyType.FileSystem, input));
    }

    [Fact]
    public void Filesystem_requires_non_empty_path()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PoliciesService.NormalizeSourcePath(BackupPolicyType.FileSystem, "   "));
    }

    [Theory]
    [InlineData(BackupPolicyType.PostgreSqlDump)]
    [InlineData(BackupPolicyType.MySqlDump)]
    public void Logical_dump_policies_ignore_source_path(BackupPolicyType type)
    {
        var actual = PoliciesService.NormalizeSourcePath(type, "anything goes here");
        Assert.Equal(string.Empty, actual);
    }
}

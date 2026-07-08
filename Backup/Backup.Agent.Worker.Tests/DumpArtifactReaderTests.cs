using System.Text;
using Backup.Agent.Worker.Services;

namespace Backup.Agent.Worker.Tests;

public sealed class DumpArtifactReaderTests
{
    private static readonly byte[] Sql =
        Encoding.UTF8.GetBytes("-- dump\nCREATE TABLE t (id int);\n");

    [Fact]
    public void IsZstd_TrueForMagic_FalseForSql()
    {
        Assert.True(DumpArtifactReader.IsZstd(DumpArtifactWriter.ZstdMagic));
        Assert.False(DumpArtifactReader.IsZstd(Sql));
        Assert.False(DumpArtifactReader.IsZstd([0x28, 0xB5]));
    }

    [Fact]
    public async Task OpenForRestore_DecompressesZstdArtifact()
    {
        var path = Path.Combine(Path.GetTempPath(), $"art_{Guid.NewGuid():N}.sql.zst");
        await DumpArtifactWriter.WriteAsync(new MemoryStream(Sql), path, compress: true, CancellationToken.None);
        try
        {
            using var stream = DumpArtifactReader.OpenForRestore(path);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            Assert.Equal(Sql, buffer.ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task OpenForRestore_PassesThroughPlainSql()
    {
        var path = Path.Combine(Path.GetTempPath(), $"art_{Guid.NewGuid():N}.sql");
        await File.WriteAllBytesAsync(path, Sql);
        try
        {
            using var stream = DumpArtifactReader.OpenForRestore(path);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            Assert.Equal(Sql, buffer.ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }
}

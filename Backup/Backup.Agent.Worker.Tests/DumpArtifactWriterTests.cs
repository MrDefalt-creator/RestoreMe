using System.Text;
using Backup.Agent.Worker.Services;
using ZstdSharp;

namespace Backup.Agent.Worker.Tests;

public sealed class DumpArtifactWriterTests
{
    private static readonly byte[] Payload =
        Encoding.UTF8.GetBytes("-- dump\nCREATE TABLE t (id int);\nINSERT INTO t VALUES (1);\n");

    [Fact]
    public async Task Compress_WritesZstdFrame_ThatDecompressesToSource()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dump_{Guid.NewGuid():N}.sql.zst");
        try
        {
            await DumpArtifactWriter.WriteAsync(new MemoryStream(Payload), path, compress: true, CancellationToken.None);

            var written = await File.ReadAllBytesAsync(path);
            Assert.Equal(DumpArtifactWriter.ZstdMagic, written[..4]);

            using var decompressor = new Decompressor();
            var round = decompressor.Unwrap(written).ToArray();
            Assert.Equal(Payload, round);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Passthrough_WritesRawBytes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dump_{Guid.NewGuid():N}.sql");
        try
        {
            await DumpArtifactWriter.WriteAsync(new MemoryStream(Payload), path, compress: false, CancellationToken.None);

            Assert.Equal(Payload, await File.ReadAllBytesAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}

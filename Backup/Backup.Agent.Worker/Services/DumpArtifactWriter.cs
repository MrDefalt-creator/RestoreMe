using ZstdSharp;

namespace Backup.Agent.Worker.Services;

/// <summary>
/// Writes a logical-dump stream to disk, optionally zstd-compressed. Kept
/// separate from process plumbing so the compress/passthrough decision is
/// unit-testable without a live pg_dump/mysqldump.
/// </summary>
public static class DumpArtifactWriter
{
    // zstd frame magic number (little-endian 0xFD2FB528).
    public static readonly byte[] ZstdMagic = [0x28, 0xB5, 0x2F, 0xFD];

    public static async Task WriteAsync(
        Stream source,
        string destinationPath,
        bool compress,
        CancellationToken cancellationToken)
    {
        await using var file = File.Create(destinationPath);
        if (compress)
        {
            await using var zstd = new CompressionStream(file);
            await source.CopyToAsync(zstd, cancellationToken);
        }
        else
        {
            await source.CopyToAsync(file, cancellationToken);
        }
    }
}

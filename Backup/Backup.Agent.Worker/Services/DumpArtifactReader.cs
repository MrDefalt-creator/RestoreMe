using ZstdSharp;

namespace Backup.Agent.Worker.Services;

/// <summary>
/// Opens a downloaded dump artifact for restore, transparently decompressing
/// zstd frames. Detection is by magic bytes on the artifact itself — never the
/// current policy flag — so an artifact written before the flag changed still
/// restores, and legacy plain-SQL artifacts keep working.
/// </summary>
public static class DumpArtifactReader
{
    public static bool IsZstd(ReadOnlySpan<byte> header) =>
        header.Length >= 4 && header[..4].SequenceEqual(DumpArtifactWriter.ZstdMagic);

    public static Stream OpenForRestore(string path)
    {
        var file = File.OpenRead(path);
        Span<byte> header = stackalloc byte[4];
        var read = file.Read(header);
        file.Position = 0;

        // leaveOpen:false so disposing the returned stream also closes the file
        // (ZstdSharp defaults leaveOpen to true).
        return read == 4 && IsZstd(header)
            ? new DecompressionStream(file, leaveOpen: false)
            : file;
    }
}

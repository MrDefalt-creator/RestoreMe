namespace Backup.Server.Application.Services;

// Singleton flag flipped by the startup bucket initializer; lets the
// per-request upload-ticket path skip the BucketExistsAsync round-trip
// once the bucket is known to be present.
public sealed class BucketReadyState
{
    private volatile bool _ready;

    public bool IsReady => _ready;

    public void MarkReady() => _ready = true;
}

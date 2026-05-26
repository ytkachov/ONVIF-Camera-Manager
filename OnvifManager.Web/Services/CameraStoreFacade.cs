using OnvifManager.Models;
using OnvifManager.Services;

namespace OnvifManager.Web.Services;

// Serializes read-modify-write cycles against the underlying ICameraStore so
// concurrent HTTP requests can't interleave and clobber each other's saves.
// Single-user deployment, so contention is rare but still possible (e.g. a
// double-click on the React save button).
public sealed class CameraStoreFacade
{
    private readonly ICameraStore _store;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public CameraStoreFacade(ICameraStore store)
    {
        _store = store;
    }

    public string StorePath => _store.StorePath;

    public IReadOnlyList<CameraDevice> List() => _store.Load();

    public CameraDevice? Find(string id)
    {
        return _store.Load().FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<TResult> MutateAsync<TResult>(Func<List<CameraDevice>, TResult> mutation, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var current = _store.Load().ToList();
            var result = mutation(current);
            await _store.SaveAsync(current, ct).ConfigureAwait(false);
            return result;
        }
        finally
        {
            _mutex.Release();
        }
    }
}

namespace StarFallMC.Services.Resources;

using StarFallMC.Entity.Enum;

/// <summary>
/// Thread-safe cache for successful community DTO pages. No UI objects, streams,
/// tasks, or cancellation sources are retained in an entry.
/// </summary>
public sealed class ResourceCache
{
    private readonly object _sync = new();
    private readonly Dictionary<ResourceCacheKey, CacheEntry> _entries = [];
    private readonly Dictionary<ResourceCacheKey, InflightRequest> _inflight = [];
    private readonly Dictionary<ResourceType, (ResourceQuery Query, ResourceCacheKey Key)> _lastQueries = [];
    private readonly TimeSpan _ttl;
    private readonly int _capacity;
    private readonly Func<DateTimeOffset> _clock;

    public ResourceCache(TimeSpan? ttl = null, int capacity = 32, Func<DateTimeOffset>? clock = null)
    {
        _ttl = ttl ?? TimeSpan.FromMinutes(10);
        _capacity = Math.Max(1, capacity);
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                ClearExpiredCore(_clock());
                return _entries.Count;
            }
        }
    }

    public bool TryGet(ResourceCacheKey key, out ResourcePageDto<CommunityResourceDto> value)
    {
        lock (_sync)
        {
            DateTimeOffset now = _clock();
            if (_entries.TryGetValue(key, out CacheEntry? entry) && entry.ExpiresAt > now)
            {
                entry.LastAccessed = now;
                value = Clone(entry.Value, fromCache: true);
                return true;
            }

            _entries.Remove(key);
            value = default!;
            return false;
        }
    }

    public void Set(ResourceCacheKey key, ResourcePageDto<CommunityResourceDto> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (_sync)
        {
            DateTimeOffset now = _clock();
            ClearExpiredCore(now);
            if (!_entries.ContainsKey(key) && _entries.Count >= _capacity)
            {
                ResourceCacheKey? oldest = _entries
                    .OrderBy(item => item.Value.LastAccessed)
                    .Select(item => (ResourceCacheKey?)item.Key)
                    .FirstOrDefault();
                if (oldest is ResourceCacheKey oldestKey) _entries.Remove(oldestKey);
            }

            _entries[key] = new CacheEntry(Clone(value, fromCache: false), now + _ttl, now);
        }
    }

    public async Task<ResourcePageDto<CommunityResourceDto>> GetOrCreateAsync(
        ResourceCacheKey key,
        Func<CancellationToken, Task<ResourcePageDto<CommunityResourceDto>>> factory,
        CancellationToken cancellationToken = default)
    {
        if (TryGet(key, out ResourcePageDto<CommunityResourceDto> cached)) return cached;

        InflightRequest request;
        lock (_sync)
        {
            if (TryGetCore(key, _clock(), out cached)) return cached;

            if (_inflight.TryGetValue(key, out request!))
            {
                request.WaiterCount++;
            }
            else
            {
                request = new InflightRequest();
                request.WaiterCount = 1;
                _inflight[key] = request;
                request.Task = CreateRequestAsync(key, factory, request);
            }
        }

        try
        {
            return await request.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ReleaseWaiter(key, request);
        }
    }

    public void ClearForVersion(string directoryVersion)
    {
        directoryVersion ??= string.Empty;
        var requestsToCancel = new List<InflightRequest>();
        lock (_sync)
        {
            foreach (ResourceCacheKey key in _entries.Keys
                         .Where(key => !string.Equals(key.DirectoryVersion, directoryVersion, StringComparison.Ordinal))
                         .ToList())
            {
                _entries.Remove(key);
            }
            foreach (ResourceType type in _lastQueries
                         .Where(item => !string.Equals(item.Value.Key.DirectoryVersion, directoryVersion, StringComparison.Ordinal))
                         .Select(item => item.Key)
                         .ToList())
            {
                _lastQueries.Remove(type);
            }
            foreach ((ResourceCacheKey key, InflightRequest request) in _inflight
                         .Where(item => !string.Equals(item.Key.DirectoryVersion, directoryVersion, StringComparison.Ordinal))
                         .ToList())
            {
                _inflight.Remove(key);
                requestsToCancel.Add(request);
            }
        }
        foreach (InflightRequest request in requestsToCancel) request.Cancel();
    }

    public void RememberQuery(ResourceQuery query, ResourceCacheKey key)
    {
        lock (_sync) _lastQueries[query.ResourceType] = (query, key);
    }

    public bool TryGetLastQuery(ResourceType resourceType, out ResourceQuery query, out ResourceCacheKey key)
    {
        lock (_sync)
        {
            if (_lastQueries.TryGetValue(resourceType, out var value))
            {
                query = value.Query;
                key = value.Key;
                return true;
            }
            query = default!;
            key = default!;
            return false;
        }
    }

    public void ClearExpired()
    {
        lock (_sync) ClearExpiredCore(_clock());
    }

    public void Clear()
    {
        List<InflightRequest> requestsToCancel;
        lock (_sync)
        {
            _entries.Clear();
            _lastQueries.Clear();
            requestsToCancel = _inflight.Values.ToList();
            _inflight.Clear();
        }
        foreach (InflightRequest request in requestsToCancel) request.Cancel();
    }

    private async Task<ResourcePageDto<CommunityResourceDto>> CreateRequestAsync(
        ResourceCacheKey key,
        Func<CancellationToken, Task<ResourcePageDto<CommunityResourceDto>>> factory,
        InflightRequest request)
    {
        try
        {
            ResourcePageDto<CommunityResourceDto> result = await factory(request.Token).ConfigureAwait(false);
            request.Token.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (!_inflight.TryGetValue(key, out InflightRequest? current) || !ReferenceEquals(current, request))
                {
                    throw new OperationCanceledException("The resource request was invalidated.", request.Token);
                }
                Set(key, result);
            }
            return result;
        }
        finally
        {
            lock (_sync)
            {
                if (_inflight.TryGetValue(key, out InflightRequest? current) && ReferenceEquals(current, request))
                {
                    _inflight.Remove(key);
                }
            }
            request.Dispose();
        }
    }

    private void ReleaseWaiter(ResourceCacheKey key, InflightRequest request)
    {
        bool cancel = false;
        lock (_sync)
        {
            request.WaiterCount--;
            if (request.WaiterCount == 0 && !request.Task.IsCompleted)
            {
                if (_inflight.TryGetValue(key, out InflightRequest? current) && ReferenceEquals(current, request))
                {
                    _inflight.Remove(key);
                }
                cancel = true;
            }
        }
        if (cancel) request.Cancel();
    }

    private void ClearExpiredCore(DateTimeOffset now)
    {
        foreach (ResourceCacheKey key in _entries
                     .Where(item => item.Value.ExpiresAt <= now)
                     .Select(item => item.Key)
                     .ToList())
        {
            _entries.Remove(key);
        }
    }

    private bool TryGetCore(
        ResourceCacheKey key,
        DateTimeOffset now,
        out ResourcePageDto<CommunityResourceDto> value)
    {
        if (_entries.TryGetValue(key, out CacheEntry? entry) && entry.ExpiresAt > now)
        {
            entry.LastAccessed = now;
            value = Clone(entry.Value, fromCache: true);
            return true;
        }

        _entries.Remove(key);
        value = default!;
        return false;
    }

    private static ResourcePageDto<CommunityResourceDto> Clone(
        ResourcePageDto<CommunityResourceDto> value,
        bool fromCache) => new(
        value.Items.Select(Clone).ToList(),
        value.TotalCount,
        value.Page,
        fromCache);

    private static CommunityResourceDto Clone(CommunityResourceDto value) => value with
    {
        Loaders = value.Loaders?.ToList(),
        GameVersions = value.GameVersions?.ToList(),
        Categories = value.Categories?.ToList()
    };

    private sealed class CacheEntry(
        ResourcePageDto<CommunityResourceDto> value,
        DateTimeOffset expiresAt,
        DateTimeOffset lastAccessed)
    {
        public ResourcePageDto<CommunityResourceDto> Value { get; } = value;
        public DateTimeOffset ExpiresAt { get; } = expiresAt;
        public DateTimeOffset LastAccessed { get; set; } = lastAccessed;
    }

    private sealed class InflightRequest : IDisposable
    {
        private readonly object _sync = new();
        private readonly CancellationTokenSource _cancellation = new();
        private bool _disposed;

        public CancellationToken Token => _cancellation.Token;
        public Task<ResourcePageDto<CommunityResourceDto>> Task { get; set; } = null!;
        public int WaiterCount { get; set; }

        public void Cancel()
        {
            lock (_sync)
            {
                if (!_disposed) _cancellation.Cancel();
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                _cancellation.Dispose();
            }
        }
    }
}

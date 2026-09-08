using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StarFallMC.Util;

public static class ImageLoader
{
    private const int DefaultIconCapacity = 192;
    private const int DefaultNetworkCapacity = 128;
    private const int DefaultBackgroundCapacity = 2;
    private const long MaxIconSourceBytes = 16L * 1024 * 1024;
    private const long MaxNetworkThumbnailBytes = 8L * 1024 * 1024;
    private const long MaxBackgroundSourceBytes = 64L * 1024 * 1024;
    private const long MaxNetworkBackgroundBytes = 32L * 1024 * 1024;
    private const long MaxDecodedSourcePixels = 1_000_000_000;

    // These dictionaries contain active loads only; completion removes every entry.
    private static readonly ConcurrentDictionary<ImageCacheKey, Lazy<ImageSource>> LocalLoads = new();
    private static readonly ConcurrentDictionary<ImageCacheKey, SharedNetworkLoad> NetworkLoads = new();
    // Decoded images are retained only by the explicit capacities above.
    private static BoundedImageCache iconCache = new(DefaultIconCapacity);
    private static BoundedImageCache networkCache = new(DefaultNetworkCapacity);
    private static BoundedImageCache backgroundCache = new(DefaultBackgroundCapacity);

    public static ImageSource Placeholder { get; } = CreatePlaceholder();

    public static ImageSource LoadLocal(
        string path,
        int decodePixelWidth,
        int decodePixelHeight,
        string? versionKey = null)
    {
        return LoadLocalCore(
            path,
            decodePixelWidth,
            decodePixelHeight,
            versionKey,
            dpiScale: 1,
            ImageCacheClass.Icon,
            MaxIconSourceBytes);
    }

    internal static ImageSource LoadArchiveEntry(
        string archivePath,
        string entryName,
        int decodePixelWidth,
        int decodePixelHeight,
        string? versionKey = null)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || string.IsNullOrWhiteSpace(entryName)) {
            return Placeholder;
        }

        try {
            var fullPath = Path.GetFullPath(archivePath);
            var archiveInfo = new FileInfo(fullPath);
            if (!archiveInfo.Exists || archiveInfo.Length > MaxBackgroundSourceBytes) {
                return Placeholder;
            }

            long entryLength;
            long compressedLength;
            using (var archive = ZipFile.OpenRead(fullPath)) {
                var entry = FindEntry(archive, entryName);
                if (entry is null || !IsArchiveEntryAllowed(entry)) {
                    return Placeholder;
                }

                entryLength = entry.Length;
                compressedLength = entry.CompressedLength;
            }

            var source = NormalizePathForKey(fullPath) + "|" + entryName.ToUpperInvariant();
            var version = versionKey ??
                $"{archiveInfo.Length}:{archiveInfo.LastWriteTimeUtc.Ticks}:{entryLength}:{compressedLength}";
            var key = CreateKey(ImageCacheClass.Icon, source, version, decodePixelWidth, decodePixelHeight, 1);
            return GetOrLoad(iconCache, key, () => {
                using var archive = ZipFile.OpenRead(fullPath);
                var entry = FindEntry(archive, entryName);
                if (entry is null || !IsArchiveEntryAllowed(entry)) {
                    return Placeholder;
                }

                using var entryStream = entry.Open();
                var bytes = ReadLimited(entryStream, MaxIconSourceBytes);
                return DecodeBitmap(() => new MemoryStream(bytes, writable: false), decodePixelWidth, decodePixelHeight);
            });
        }
        catch (Exception exception) when (IsImageLoadException(exception)) {
            Debug.WriteLine($"Archive image load failed: {exception.GetType().Name}");
            return Placeholder;
        }
    }

    internal static Task<ImageSource> LoadSourceAsync(
        string source,
        int decodePixelWidth,
        int decodePixelHeight,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source)) {
            return Task.FromResult(Placeholder);
        }

        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) {
            return LoadNetworkAsync(source, decodePixelWidth, decodePixelHeight, ImageCacheClass.Network, cancellationToken);
        }

        if (Path.IsPathRooted(source)) {
            return Task.Run(
                () => LoadLocal(source, decodePixelWidth, decodePixelHeight),
                cancellationToken);
        }

        return Task.FromResult(LoadPackResource(source, decodePixelWidth, decodePixelHeight));
    }

    internal static Task<ImageSource> LoadBackgroundAsync(
        string source,
        int decodePixelWidth,
        int decodePixelHeight,
        double dpiScale,
        CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) {
            return LoadNetworkAsync(source, decodePixelWidth, decodePixelHeight, ImageCacheClass.Background,
                cancellationToken, dpiScale);
        }

        return Task.Run(
            () => LoadLocalCore(
                source,
                decodePixelWidth,
                decodePixelHeight,
                versionKey: null,
                dpiScale,
                ImageCacheClass.Background,
                MaxBackgroundSourceBytes),
            cancellationToken);
    }

    internal static bool IsPlaceholder(ImageSource? imageSource) => ReferenceEquals(imageSource, Placeholder);

    internal static ImageCacheStatistics IconCacheStatistics => iconCache.GetStatistics();
    internal static ImageCacheStatistics NetworkCacheStatistics => networkCache.GetStatistics();
    internal static ImageCacheStatistics BackgroundCacheStatistics => backgroundCache.GetStatistics();

    internal static void ResetCachesForTests(
        int iconCapacity = DefaultIconCapacity,
        int networkCapacity = DefaultNetworkCapacity,
        int backgroundCapacity = DefaultBackgroundCapacity)
    {
        iconCache = new BoundedImageCache(iconCapacity);
        networkCache = new BoundedImageCache(networkCapacity);
        backgroundCache = new BoundedImageCache(backgroundCapacity);
        LocalLoads.Clear();
        var activeNetworkLoads = NetworkLoads.Values.ToArray();
        NetworkLoads.Clear();
        foreach (var load in activeNetworkLoads) {
            load.Cancel();
        }
    }

    internal static ImageDecodeSize CalculateDecodeSize(
        int sourceWidth,
        int sourceHeight,
        int maximumWidth,
        int maximumHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0) {
            throw new InvalidDataException("Image dimensions must be positive.");
        }

        var widthScale = maximumWidth > 0 ? (double)maximumWidth / sourceWidth : double.PositiveInfinity;
        var heightScale = maximumHeight > 0 ? (double)maximumHeight / sourceHeight : double.PositiveInfinity;
        var scale = Math.Min(1, Math.Min(widthScale, heightScale));
        if (double.IsPositiveInfinity(scale)) {
            scale = 1;
        }

        var width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        var height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        var axis = widthScale <= heightScale ? ImageDecodeAxis.Width : ImageDecodeAxis.Height;
        return new ImageDecodeSize(width, height, scale < 1 ? axis : ImageDecodeAxis.None);
    }

    private static ImageSource LoadLocalCore(
        string path,
        int decodePixelWidth,
        int decodePixelHeight,
        string? versionKey,
        double dpiScale,
        ImageCacheClass cacheClass,
        long maximumSourceBytes)
    {
        if (string.IsNullOrWhiteSpace(path)) {
            return Placeholder;
        }

        try {
            var fullPath = Path.GetFullPath(path);
            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists || fileInfo.Length <= 0 || fileInfo.Length > maximumSourceBytes) {
                return Placeholder;
            }

            var version = versionKey ?? $"{fileInfo.Length}:{fileInfo.LastWriteTimeUtc.Ticks}";
            var key = CreateKey(
                cacheClass,
                NormalizePathForKey(fullPath),
                version,
                decodePixelWidth,
                decodePixelHeight,
                dpiScale);
            var cache = cacheClass == ImageCacheClass.Background ? backgroundCache : iconCache;
            return GetOrLoad(cache, key, () => DecodeBitmap(
                () => new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete),
                decodePixelWidth,
                decodePixelHeight));
        }
        catch (Exception exception) when (IsImageLoadException(exception)) {
            Debug.WriteLine($"Local image load failed: {exception.GetType().Name}");
            return Placeholder;
        }
    }

    private static ImageSource LoadPackResource(string source, int decodePixelWidth, int decodePixelHeight)
    {
        try {
            var uri = new Uri(source, UriKind.RelativeOrAbsolute);
            if (!uri.IsAbsoluteUri || uri.Scheme == "pack") {
                var resource = Application.GetResourceStream(uri);
                if (resource?.Stream != null) {
                    using (resource.Stream) {
                        var bytes = ReadLimited(resource.Stream, MaxIconSourceBytes);
                        var key = CreateKey(
                            ImageCacheClass.Icon,
                            source,
                            "package",
                            decodePixelWidth,
                            decodePixelHeight,
                            1);
                        return GetOrLoad(iconCache, key, () => DecodeBitmap(
                            () => new MemoryStream(bytes, writable: false),
                            decodePixelWidth,
                            decodePixelHeight));
                    }
                }
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache | BitmapCreateOptions.PreservePixelFormat;
            bitmap.UriSource = uri;
            if (decodePixelWidth > 0) {
                bitmap.DecodePixelWidth = decodePixelWidth;
            }
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception exception) when (IsImageLoadException(exception)) {
            Debug.WriteLine($"Package image load failed: {exception.GetType().Name}");
            return Placeholder;
        }
    }

    private static async Task<ImageSource> LoadNetworkAsync(
        string source,
        int decodePixelWidth,
        int decodePixelHeight,
        ImageCacheClass cacheClass,
        CancellationToken cancellationToken,
        double dpiScale = 1)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) {
            return Placeholder;
        }

        var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)));
        var key = CreateKey(cacheClass, sourceHash, "network", decodePixelWidth, decodePixelHeight, dpiScale);
        var cache = cacheClass == ImageCacheClass.Background ? backgroundCache : networkCache;
        if (cache.TryGet(key, out var cached)) {
            return cached;
        }

        var maximumBytes = cacheClass == ImageCacheClass.Background
            ? MaxNetworkBackgroundBytes
            : MaxNetworkThumbnailBytes;
        var candidate = new SharedNetworkLoad(token => DownloadAndCacheNetworkImageAsync(
            uri,
            decodePixelWidth,
            decodePixelHeight,
            maximumBytes,
            cache,
            key,
            token));
        var load = NetworkLoads.GetOrAdd(key, candidate);
        if (ReferenceEquals(load, candidate)) {
            _ = RemoveNetworkLoadWhenCompleteAsync(key, load);
        }
        else {
            candidate.Dispose();
        }

        try {
            return await load.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
    }

    private static async Task<ImageSource> DownloadAndCacheNetworkImageAsync(
        Uri uri,
        int decodePixelWidth,
        int decodePixelHeight,
        long maximumBytes,
        BoundedImageCache cache,
        ImageCacheKey key,
        CancellationToken cancellationToken)
    {
        try {
            var image = await DownloadNetworkImageAsync(
                uri,
                decodePixelWidth,
                decodePixelHeight,
                maximumBytes,
                cancellationToken).ConfigureAwait(false);
            cache.Add(key, image, DateTimeOffset.UtcNow + TimeSpan.FromHours(6));
            return image;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch (Exception exception) when (IsImageLoadException(exception)) {
            Debug.WriteLine($"Network image load failed: {exception.GetType().Name}");
            cache.Add(key, Placeholder, DateTimeOffset.UtcNow + TimeSpan.FromMinutes(5));
            return Placeholder;
        }
    }

    private static async Task RemoveNetworkLoadWhenCompleteAsync(ImageCacheKey key, SharedNetworkLoad load)
    {
        try {
            await load.Task.ConfigureAwait(false);
        }
        catch (Exception) {
            // The active caller observes the original failure or cancellation.
        }
        finally {
            NetworkLoads.TryRemove(new KeyValuePair<ImageCacheKey, SharedNetworkLoad>(key, load));
            load.Dispose();
        }
    }

    private static async Task<ImageSource> DownloadNetworkImageAsync(
        Uri uri,
        int decodePixelWidth,
        int decodePixelHeight,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("User-Agent", PropertiesUtil.UserAgent);
        using var response = await HttpRequestUtil.client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token);
        response.EnsureSuccessStatusCode();

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(mediaType) || !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException("Network resource is not an image.");
        }

        if (response.Content.Headers.ContentLength is long contentLength && contentLength > maximumBytes) {
            throw new InvalidDataException("Network image exceeds the response limit.");
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(timeout.Token);
        var bytes = await ReadLimitedAsync(responseStream, maximumBytes, timeout.Token);
        return DecodeBitmap(
            () => new MemoryStream(bytes, writable: false),
            decodePixelWidth,
            decodePixelHeight);
    }

    private static ImageSource GetOrLoad(BoundedImageCache cache, ImageCacheKey key, Func<ImageSource> factory)
    {
        if (cache.TryGet(key, out var cached)) {
            return cached;
        }

        var lazy = LocalLoads.GetOrAdd(key, _ => new Lazy<ImageSource>(factory, LazyThreadSafetyMode.ExecutionAndPublication));
        try {
            var image = lazy.Value;
            cache.Add(key, image, expiration: null);
            return image;
        }
        finally {
            LocalLoads.TryRemove(new KeyValuePair<ImageCacheKey, Lazy<ImageSource>>(key, lazy));
        }
    }

    private static ImageSource DecodeBitmap(Func<Stream> streamFactory, int maximumWidth, int maximumHeight)
    {
        int sourceWidth;
        int sourceHeight;
        using (var metadataStream = streamFactory()) {
            var decoder = BitmapDecoder.Create(
                metadataStream,
                BitmapCreateOptions.DelayCreation | BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.None);
            if (decoder.Frames.Count == 0) {
                throw new InvalidDataException("Image contains no frames.");
            }

            sourceWidth = decoder.Frames[0].PixelWidth;
            sourceHeight = decoder.Frames[0].PixelHeight;
        }

        if (sourceWidth <= 0 || sourceHeight <= 0 ||
            (long)sourceWidth * sourceHeight > MaxDecodedSourcePixels) {
            throw new InvalidDataException("Image dimensions exceed the allowed range.");
        }

        var decodeSize = CalculateDecodeSize(sourceWidth, sourceHeight, maximumWidth, maximumHeight);
        using var stream = streamFactory();
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        bitmap.StreamSource = stream;
        if (decodeSize.Axis == ImageDecodeAxis.Width) {
            bitmap.DecodePixelWidth = decodeSize.Width;
        }
        else if (decodeSize.Axis == ImageDecodeAxis.Height) {
            bitmap.DecodePixelHeight = decodeSize.Height;
        }
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] ReadLimited(Stream stream, long maximumBytes)
    {
        using var memory = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try {
            long total = 0;
            int count;
            while ((count = stream.Read(buffer, 0, buffer.Length)) > 0) {
                total += count;
                if (total > maximumBytes) {
                    throw new InvalidDataException("Image source exceeds the byte limit.");
                }

                memory.Write(buffer, 0, count);
            }

            return memory.ToArray();
        }
        finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<byte[]> ReadLimitedAsync(
        Stream stream,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try {
            long total = 0;
            int count;
            while ((count = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0) {
                total += count;
                if (total > maximumBytes) {
                    throw new InvalidDataException("Image source exceeds the byte limit.");
                }

                await memory.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }

            return memory.ToArray();
        }
        finally {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static bool IsArchiveEntryAllowed(ZipArchiveEntry entry)
    {
        if (entry.Length <= 0 || entry.Length > MaxIconSourceBytes || entry.CompressedLength > MaxIconSourceBytes) {
            return false;
        }

        return entry.CompressedLength == 0 || entry.Length / Math.Max(1, entry.CompressedLength) <= 1000;
    }

    private static ZipArchiveEntry? FindEntry(ZipArchive archive, string entryName)
    {
        return archive.Entries.FirstOrDefault(entry =>
            string.Equals(entry.FullName, entryName, StringComparison.OrdinalIgnoreCase));
    }

    private static ImageCacheKey CreateKey(
        ImageCacheClass cacheClass,
        string source,
        string version,
        int decodePixelWidth,
        int decodePixelHeight,
        double dpiScale)
    {
        return new ImageCacheKey(
            cacheClass,
            source,
            version,
            Math.Max(0, decodePixelWidth),
            Math.Max(0, decodePixelHeight),
            Math.Max(1, (int)Math.Round(dpiScale * 100)));
    }

    private static string NormalizePathForKey(string fullPath) => fullPath.ToUpperInvariant();

    private static bool IsImageLoadException(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or InvalidDataException or
            NotSupportedException or ArgumentException or HttpRequestException or OperationCanceledException;
    }

    private static ImageSource CreatePlaceholder()
    {
        var bitmap = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[4],
            4);
        bitmap.Freeze();
        return bitmap;
    }

    private enum ImageCacheClass
    {
        Icon,
        Network,
        Background
    }

    internal enum ImageDecodeAxis
    {
        None,
        Width,
        Height
    }

    internal readonly record struct ImageDecodeSize(int Width, int Height, ImageDecodeAxis Axis);

    private readonly record struct ImageCacheKey(
        ImageCacheClass CacheClass,
        string Source,
        string Version,
        int Width,
        int Height,
        int DpiScaleBucket)
    {
        public bool IsSameSourceAndSize(ImageCacheKey other)
        {
            return CacheClass == other.CacheClass &&
                   Source == other.Source &&
                   Width == other.Width &&
                   Height == other.Height &&
                   DpiScaleBucket == other.DpiScaleBucket;
        }
    }

    private sealed class SharedNetworkLoad : IDisposable
    {
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly Lazy<Task<ImageSource>> task;
        private int waiterCount;
        private int disposed;

        public SharedNetworkLoad(Func<CancellationToken, Task<ImageSource>> factory)
        {
            task = new Lazy<Task<ImageSource>>(
                () => factory(cancellationTokenSource.Token),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public Task<ImageSource> Task => task.Value;

        public async Task<ImageSource> WaitAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref waiterCount);
            try {
                return await Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally {
                if (Interlocked.Decrement(ref waiterCount) == 0 && !Task.IsCompleted) {
                    Cancel();
                }
            }
        }

        public void Cancel()
        {
            try {
                cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException) {
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) {
                return;
            }

            cancellationTokenSource.Dispose();
        }
    }

    private sealed class BoundedImageCache
    {
        private readonly object gate = new();
        private readonly int capacity;
        private readonly Dictionary<ImageCacheKey, LinkedListNode<CacheEntry>> entries = new();
        private readonly LinkedList<CacheEntry> leastRecentlyUsed = new();
        private long hits;
        private long misses;
        private long evictions;

        public BoundedImageCache(int capacity)
        {
            this.capacity = Math.Max(1, capacity);
        }

        public bool TryGet(ImageCacheKey key, out ImageSource image)
        {
            lock (gate) {
                if (entries.TryGetValue(key, out var node)) {
                    if (node.Value.Expiration is DateTimeOffset expiration && expiration <= DateTimeOffset.UtcNow) {
                        RemoveNode(node);
                    }
                    else {
                        leastRecentlyUsed.Remove(node);
                        leastRecentlyUsed.AddFirst(node);
                        hits++;
                        image = node.Value.Image;
                        return true;
                    }
                }

                misses++;
                image = null!;
                return false;
            }
        }

        public void Add(ImageCacheKey key, ImageSource image, DateTimeOffset? expiration)
        {
            lock (gate) {
                if (entries.TryGetValue(key, out var existing)) {
                    existing.Value = new CacheEntry(key, image, expiration);
                    leastRecentlyUsed.Remove(existing);
                    leastRecentlyUsed.AddFirst(existing);
                    return;
                }

                var stale = entries
                    .Where(pair => pair.Key.IsSameSourceAndSize(key) && pair.Key.Version != key.Version)
                    .Select(pair => pair.Value)
                    .ToArray();
                foreach (var node in stale) {
                    RemoveNode(node);
                }

                var added = leastRecentlyUsed.AddFirst(new CacheEntry(key, image, expiration));
                entries[key] = added;
                while (entries.Count > capacity && leastRecentlyUsed.Last is { } last) {
                    RemoveNode(last);
                    evictions++;
                }
            }
        }

        public ImageCacheStatistics GetStatistics()
        {
            lock (gate) {
                return new ImageCacheStatistics(hits, misses, entries.Count, evictions, capacity);
            }
        }

        private void RemoveNode(LinkedListNode<CacheEntry> node)
        {
            leastRecentlyUsed.Remove(node);
            entries.Remove(node.Value.Key);
        }

        private sealed record CacheEntry(ImageCacheKey Key, ImageSource Image, DateTimeOffset? Expiration);
    }
}

internal readonly record struct ImageCacheStatistics(
    long Hits,
    long Misses,
    int Count,
    long Evictions,
    int Capacity);

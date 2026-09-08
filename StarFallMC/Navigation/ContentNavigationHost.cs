using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace StarFallMC.Navigation;

internal sealed class ContentNavigationHost : IAsyncDisposable
{
    private readonly ContentControl _contentControl;
    private readonly SemaphoreSlim _navigationLock = new(1, 1);
    private FrameworkElement? _currentPage;
    private object? _currentKey;
    private long _latestRequest;
    private bool _disposed;

    public ContentNavigationHost(ContentControl contentControl)
    {
        _contentControl = contentControl ?? throw new ArgumentNullException(nameof(contentControl));
    }

    public void SetInitialPage(object pageKey, FrameworkElement page)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(pageKey);
        ArgumentNullException.ThrowIfNull(page);
        if (_currentPage != null || _contentControl.Content != null)
        {
            throw new InvalidOperationException("The navigation host already has content.");
        }

        _currentKey = pageKey;
        _currentPage = page;
        _contentControl.Content = CreatePagePresenter(page);
    }

    internal FrameworkElement? CurrentPage => _currentPage;

    public Task ShowAsync(
        Func<FrameworkElement> pageFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageFactory);
        return ShowAsync(pageFactory, pageFactory, cancellationToken);
    }

    public async Task ShowAsync(
        object pageKey,
        Func<FrameworkElement> pageFactory,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(pageKey);
        ArgumentNullException.ThrowIfNull(pageFactory);

        var request = Interlocked.Increment(ref _latestRequest);
        await _navigationLock.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (request != Volatile.Read(ref _latestRequest) || Equals(_currentKey, pageKey))
            {
                return;
            }

            var oldPage = _currentPage;
            var oldKey = _currentKey;
            if (oldPage is IPageLifecycle oldLifecycle)
            {
                await oldLifecycle.DeactivateAsync().ConfigureAwait(true);
            }

            _contentControl.Content = null;
            _currentPage = null;
            _currentKey = null;
            if (request != Volatile.Read(ref _latestRequest))
            {
                return;
            }

            FrameworkElement? newPage = null;
            try
            {
                newPage = pageFactory();
                _contentControl.Content = CreatePagePresenter(newPage);
                _currentPage = newPage;
                _currentKey = pageKey;

                if (newPage is IPageLifecycle newLifecycle)
                {
                    await newLifecycle.ActivateAsync(cancellationToken).ConfigureAwait(true);
                }
            }
            catch
            {
                if (newPage is IPageLifecycle failedLifecycle)
                {
                    await TryDeactivateAsync(failedLifecycle).ConfigureAwait(true);
                }

                _contentControl.Content = oldPage == null ? null : CreatePagePresenter(oldPage);
                _currentPage = oldPage;
                _currentKey = oldKey;
                if (oldPage is IPageLifecycle lifecycleToRestore)
                {
                    await lifecycleToRestore.ActivateAsync(CancellationToken.None).ConfigureAwait(true);
                }

                throw;
            }
        }
        finally
        {
            _navigationLock.Release();
        }
    }

    public async Task ClearAsync()
    {
        if (_disposed)
        {
            return;
        }

        Interlocked.Increment(ref _latestRequest);
        await _navigationLock.WaitAsync().ConfigureAwait(true);
        try
        {
            if (_currentPage is IPageLifecycle lifecycle)
            {
                await lifecycle.DeactivateAsync().ConfigureAwait(true);
            }

            _contentControl.Content = null;
            _currentPage = null;
            _currentKey = null;
        }
        finally
        {
            _navigationLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await ClearAsync().ConfigureAwait(true);
        _disposed = true;
        _navigationLock.Dispose();
    }

    private static async Task TryDeactivateAsync(IPageLifecycle lifecycle)
    {
        try
        {
            await lifecycle.DeactivateAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to deactivate a page after activation failed: {exception}");
        }
    }

    internal static FrameworkElement CreatePagePresenter(FrameworkElement page)
    {
        if (page is not Page)
        {
            return page;
        }

        return new Frame
        {
            NavigationUIVisibility = NavigationUIVisibility.Hidden,
            JournalOwnership = JournalOwnership.OwnsJournal,
            Content = page
        };
    }
}

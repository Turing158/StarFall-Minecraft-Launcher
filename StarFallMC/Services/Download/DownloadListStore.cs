using System.Collections.ObjectModel;
using StarFallMC.Entity;

namespace StarFallMC.Services.Download;

internal sealed class DownloadListStore {
    private readonly Dictionary<long, DownloadListItem> itemsById = new();
    private long? sessionId;

    public DownloadListStore(ObservableCollection<DownloadListItem>? items = null) {
        Items = items ?? new ObservableCollection<DownloadListItem>();
    }

    public ObservableCollection<DownloadListItem> Items { get; }

    public bool Apply(DownloadProgressSnapshot snapshot) {
        if (sessionId != snapshot.SessionId) {
            sessionId = snapshot.SessionId;
            itemsById.Clear();
            Items.Clear();
        }

        var stateChanged = false;
        var seenIds = new HashSet<long>();
        for (var index = 0; index < snapshot.Files.Count; index++) {
            var file = snapshot.Files[index];
            seenIds.Add(file.Id);
            if (!itemsById.TryGetValue(file.Id, out var item)) {
                item = new DownloadListItem(file);
                itemsById[file.Id] = item;
                Items.Insert(Math.Min(index, Items.Count), item);
                continue;
            }

            stateChanged |= item.UpdateFrom(file);
            if (index < Items.Count && ReferenceEquals(Items[index], item)) {
                continue;
            }

            var currentIndex = Items.IndexOf(item);
            if (currentIndex >= 0) {
                Items.Move(currentIndex, Math.Min(index, Items.Count - 1));
            }
        }

        for (var index = Items.Count - 1; index >= 0; index--) {
            var item = Items[index];
            if (!seenIds.Contains(item.Id)) {
                Items.RemoveAt(index);
                itemsById.Remove(item.Id);
            }
        }

        return stateChanged;
    }

    public void Clear() {
        sessionId = null;
        itemsById.Clear();
        Items.Clear();
    }
}

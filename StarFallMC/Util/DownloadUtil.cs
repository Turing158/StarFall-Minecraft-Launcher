using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using StarFallMC.Entity;

namespace StarFallMC.Util;

public class DownloadUtil {
    
    private static List<ThreadDownloader> downloaders = new ();
    private static ConcurrentQueue<DownloadFile> waitDownloadFiles;
    public static ConcurrentBag<DownloadFile> errorDownloadFiles = new ();
    private static int TotalCount, RetryCount;
    private static int FinishCount => finishCount;
    private static int finishCount;

    private static TaskCompletionSource<bool> downloadCompletionSource;
    private static readonly object downloadLock = new object();
    private static CancellationTokenSource globalCts;
    private static bool isCancelld;
    
    public static async Task StartDownload(List<DownloadFile> downloadFiles) {
        isCancelld = false;
        waitDownloadFiles = new ConcurrentQueue<DownloadFile>(downloadFiles);
        errorDownloadFiles.Clear();
        globalCts = new CancellationTokenSource();
        downloadCompletionSource = new TaskCompletionSource<bool>();
        TotalCount = downloadFiles.Count;
        finishCount = 0;
        DownloadPage.ProgressInit?.Invoke(downloadFiles);
        Home.DownloadState?.Invoke(true);
        DownloadFilesFunc();
        await downloadCompletionSource.Task;
    }

    private static async Task DownloadFilesFunc() {
        if (downloaders.Count == 0) {
            Console.WriteLine("下载线程未初始化，请先调用 DownloadUtil.init()");
            return;
        }
        if (isCancelld) {
            return;
        }
        lock (downloadLock) {
            if (downloaders.All(i => i.isRunning)) {
                return;
            }
            for (int i = 0; i < downloaders.Count; i++) {
                if (waitDownloadFiles.Count == 0) {
                    Console.WriteLine("下载队列已空");
                    if (FinishCount + errorDownloadFiles.Count == TotalCount) {
                        Home.DownloadState?.Invoke(false);
                        downloadCompletionSource?.TrySetResult(true);
                    }
                    return;
                }
                if (downloaders[i].isRunning) {
                    continue;
                }
                downloaders[i].isRunning = true;
                waitDownloadFiles.TryDequeue(out DownloadFile item);
                _ = downloaders[i].DownloadFileFunc(item,globalCts.Token).ConfigureAwait(false);;
            }
        }
    }

    public static void init(int AsyncDownloadCount,int retryCount) {
        if (downloaders != null) {
            downloaders.Clear();
        }
        for (int i = 0; i < AsyncDownloadCount; i++) {
            downloaders.Add(new ThreadDownloader());
        }

        RetryCount = retryCount;
    }

    public static void CancelDownload() {
        if (!isCancelld) {
            // Console.WriteLine($"下载任务取消    总共：{TotalCount} | 完成：{FinishCount} | 失败：{errorDownloadFiles.Count}");
            Home.DownloadState?.Invoke(false);
            isCancelld = false;
            globalCts.Cancel();
            waitDownloadFiles?.Clear();
            errorDownloadFiles?.Clear();
            for (int i = 0; i < downloaders.Count; i++) {
                downloaders[i].isRunning = false;
            }
        }
    }
    private static readonly HttpClient httpClient = new HttpClient();
    public static async Task SingalDownload(DownloadFile downloadFile) {
        var dir = Path.GetDirectoryName(downloadFile.FilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
            Directory.CreateDirectory(dir);
        }
        if (string.IsNullOrEmpty(downloadFile.Url) && string.IsNullOrEmpty(downloadFile.UrlPath)) {
            return;
        }
        for (int i = 0; i < RetryCount * 2; i++) {
            try {
                using var download =
                    await httpClient.GetAsync(downloadFile.UrlPath, HttpCompletionOption.ResponseHeadersRead);
                download.EnsureSuccessStatusCode();
                using var fileStream = new FileStream(downloadFile.FilePath, FileMode.Create);
                await download.Content.CopyToAsync(fileStream);
                return;
            }
            catch (Exception e){
                if (i == RetryCount) {
                    if (!string.IsNullOrEmpty(downloadFile.Url)) {
                        downloadFile.UrlPath = downloadFile.Url;
                        downloadFile.Url = "";
                        continue;
                    }
                    return;
                }
            }
        }
    }
    
    public class ThreadDownloader {
        private HttpClient httpClient = new ();
        public bool isRunning { get; set; } = false;

        public ThreadDownloader() {
        }
        public async Task DownloadFileFunc(DownloadFile item, CancellationToken ct) {
            if (isCancelld) {
                return;
            }
            item.State = DownloadFile.StateType.Downloading;
            DownloadPage.ProgressUpdate?.Invoke(item,finishCount,errorDownloadFiles.Count);
            // Console.WriteLine($"当前进度    还剩：{TotalCount-FinishCount} | 成功：{FinishCount} | 失败：{errorDownloadFiles.Count}");
            var dir = Path.GetDirectoryName(item.FilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }
            if (string.IsNullOrEmpty(item.Url) && string.IsNullOrEmpty(item.UrlPath)) {
                return;
            }

            try {
                ct.ThrowIfCancellationRequested();
                // if (item.Size == 0) {
                //     using var getSizeReq = new HttpRequestMessage(HttpMethod.Head, item.UrlPath);
                //     using var getSize = await httpClient.SendAsync(getSizeReq, ct);
                //     getSize.EnsureSuccessStatusCode();
                //     long size = getSize.Content.Headers.ContentLength.Value / 1024;
                //     // Console.WriteLine(size);
                // }
                using var download =
                    await httpClient.GetAsync(item.UrlPath, HttpCompletionOption.ResponseHeadersRead, ct);
                download.EnsureSuccessStatusCode();

                using var fileStream = new FileStream(item.FilePath, FileMode.Create);
                await download.Content.CopyToAsync(fileStream);
                Interlocked.Increment(ref finishCount);
                // Console.WriteLine("下载完成 ：" + item.FilePath);
                item.State = DownloadFile.StateType.Finished;
                DownloadPage.ProgressUpdate?.Invoke(item, finishCount, errorDownloadFiles.Count);
            }
            catch (OperationCanceledException) {
                // Console.WriteLine($"下载取消：{item.UrlPath}");
                if (File.Exists(item.FilePath)) {
                    File.Delete(item.FilePath);
                }
            }
            catch (Exception e) {
                // Console.WriteLine("下载失败 ：" + item.UrlPath);
                item.ErrorMessage = e.Message;
                if (item.RetryCount > RetryCount) {
                    item.RetryCount++;
                    item.State = DownloadFile.StateType.Waiting;
                    DownloadPage.ProgressUpdate?.Invoke(item, finishCount, errorDownloadFiles.Count);
                    waitDownloadFiles.Enqueue(item);
                }
                else {
                    if (string.IsNullOrEmpty(item.Url)) {
                        item.State = DownloadFile.StateType.Error;
                        DownloadPage.ProgressUpdate?.Invoke(item, finishCount, errorDownloadFiles.Count);
                        errorDownloadFiles.Add(item);
                    }
                    else {
                        item.UrlPath = item.Url;
                        item.Url = "";
                        item.RetryCount = 1;
                        item.State = DownloadFile.StateType.Waiting;
                        DownloadPage.ProgressUpdate?.Invoke(item, finishCount, errorDownloadFiles.Count);
                        waitDownloadFiles.Enqueue(item);
                    }
                }
            }
            isRunning = false;
            if (FinishCount + errorDownloadFiles.Count == TotalCount) {
                Home.DownloadState?.Invoke(false);
                downloadCompletionSource?.TrySetResult(true);
                Console.WriteLine($"下载任务完成    总共：{TotalCount} | 完成：{FinishCount} | 失败：{errorDownloadFiles.Count}");
                return;
            }
            if (!isCancelld && waitDownloadFiles.Count > 0) {
                _ = Task.Delay(100);
                _ = DownloadFilesFunc().ConfigureAwait(false);
            }
        }
    }
    
    public static string FormatBytes(long bytes) {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double len = bytes;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
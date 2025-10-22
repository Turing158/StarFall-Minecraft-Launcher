using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using StarFallMC.Component;
using StarFallMC.Entity;

namespace StarFallMC.Util;

public class DownloadUtil {
    public enum DownloadMode{
        Single,
        Append
    }
    
    private static List<ThreadDownloader> downloaders = new ();
    public static ConcurrentQueue<DownloadFile> waitDownloadFiles { get; private set; }
    public static ConcurrentBag<DownloadFile> errorDownloadFiles { get; private set; } = new ();
    public static int TotalCount { get; private set; }
    public static int RetryCount { get;private set; }
    public static int FinishCount {
        get => finishCount;
    }
    private static int finishCount;
    public static bool IsFinished {
        get => FinishCount + errorDownloadFiles.Count == TotalCount;
    }

    private static TaskCompletionSource<bool> downloadCompletionSource;
    private static readonly object downloadLock = new object();
    private static CancellationTokenSource globalCts;
    public static bool IsCancel { get; private set; }

    public static async Task StartDownload(List<DownloadFile> downloadFiles) {
        DownloadMode mode = DownloadMode.Single;
        bool isDownload = true;
        if (waitDownloadFiles != null && waitDownloadFiles.Count != 0) {
            Console.WriteLine(waitDownloadFiles.Count);
            await MessageBox.ShowAsync("下载队列不为空，请选择你想要的操作。\n    1.单独下载：将当前的下载队列取消，只下载当前任务。\n    2.下载：将下载任务追加到正在下载的队列中。\n    3.取消：暂时不下载",
                "提示",MessageBox.BtnType.ConfirmAndCancelAndCustom,
                callback:r => {
                    if (r == MessageBox.Result.Confirm) {
                        mode = DownloadMode.Single;
                    }
                    else if (r == MessageBox.Result.Custom) {
                        mode = DownloadMode.Append;
                    }
                    else {
                        isDownload = false;
                    }
                },confirmBtnText:"单独下载",customBtnText:"下载");
        }
        if (isDownload) {
            await StartDownloadFunc(downloadFiles, mode);
        }
    }

    private static async Task StartDownloadFunc(List<DownloadFile> downloadFiles, DownloadMode mode = DownloadMode.Single) {
        Console.WriteLine("开始下载任务");
        IsCancel = false;
        DownloadPage.DownloadingAnimState?.Invoke(true);
        if (mode == DownloadMode.Single) {
            waitDownloadFiles = new ConcurrentQueue<DownloadFile>(downloadFiles);
            errorDownloadFiles.Clear();
            globalCts?.Cancel();
            globalCts = new CancellationTokenSource();
            finishCount = 0;
        }
        else if (mode == DownloadMode.Append) {
            if (globalCts == null) {
                globalCts = new CancellationTokenSource();
            }
            else {
                globalCts.TryReset();
            }
            if (waitDownloadFiles == null) {
                waitDownloadFiles = new ConcurrentQueue<DownloadFile>(downloadFiles);
            }
            else {
                foreach (var i in downloadFiles) {
                    if (waitDownloadFiles.Any(j => j.FilePath == i.FilePath)) {
                        continue;
                    }
                    waitDownloadFiles.Enqueue(i);
                }
            }
            
        }
        
        Console.WriteLine(waitDownloadFiles.Count);
        TotalCount = waitDownloadFiles.Count;
        DownloadPage.ProgressInit?.Invoke(mode == DownloadMode.Single ? downloadFiles : waitDownloadFiles.ToList(), mode == DownloadMode.Single);
        downloadCompletionSource = new TaskCompletionSource<bool>();
        Home.DownloadState?.Invoke(true);
        DownloadFilesFunc();
        await downloadCompletionSource.Task;
    }

    private static async Task DownloadFilesFunc() {
        if (downloaders.Count == 0) {
            Console.WriteLine("下载线程未初始化，请先调用 DownloadUtil.init()");
            return;
        }
        if (IsCancel) {
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
                        MessageTips.Show("下载任务完成");
                        DownloadPage.DownloadingAnimState?.Invoke(false);
                        Home.DownloadState?.Invoke(false);
                        downloadCompletionSource?.TrySetResult(true);
                    }
                    return;
                }
                if (downloaders[i].isRunning) {
                    continue;
                }
                downloaders[i].isRunning = true;
                if (waitDownloadFiles.TryDequeue(out DownloadFile item)) {
                    _ = downloaders[i].DownloadFileFunc(item, globalCts.Token);
                }
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
        if (!IsCancel) {
            IsCancel = true;
            MessageTips.Show("下载取消", MessageTips.MessageType.Warning);
            // Console.WriteLine($"下载任务取消    总共：{TotalCount} | 完成：{FinishCount} | 失败：{errorDownloadFiles.Count}");
            DownloadPage.DownloadingAnimState?.Invoke(false);
            Home.DownloadState?.Invoke(false);
            globalCts?.Cancel();
            for (int i = 0; i < downloaders.Count; i++) {
                downloaders[i].isRunning = false;
            }
        }
    }
    
    public static void RetryDownload() {
        foreach (var i in errorDownloadFiles) {
            i.UrlPath = i.UrlPaths[0];
            i.State = DownloadFile.StateType.Downloading;
            waitDownloadFiles.Enqueue(i);
            DownloadPage.ProgressUpdate?.Invoke(i, finishCount, errorDownloadFiles.Count);
        }
        errorDownloadFiles.Clear();
        _ = DownloadFilesFunc();
    }

    public static void ContinueDownload() {
        if (IsCancel) {
            IsCancel = false;
            globalCts?.Dispose();
            globalCts = new CancellationTokenSource();
            Home.DownloadState?.Invoke(true);
            _ = DownloadFilesFunc();
        }
    }

    public static void ClearDownload() {
        IsCancel = false;
        downloadCompletionSource?.TrySetResult(false);
        downloadCompletionSource = null;
        globalCts?.Cancel();
        waitDownloadFiles?.Clear();
        errorDownloadFiles?.Clear();
        foreach (var downloader in downloaders) {
            downloader.isRunning = false;
        }
    }
    
    
    private static readonly HttpClient httpClient = new HttpClient();
    public static async Task<bool> SingalDownload(DownloadFile downloadFile) {
        var dir = Path.GetDirectoryName(downloadFile.FilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
            Directory.CreateDirectory(dir);
        }
        if (string.IsNullOrEmpty(downloadFile.UrlPath)) {
            return false;
        }
        for (int i = 0; i < RetryCount * 2; i++) {
            try {
                using var req = new HttpRequestMessage(HttpMethod.Get, downloadFile.UrlPath);
                req.Headers.Add("User-Agent", PropertiesUtil.UserAgent);
                using var download =
                    await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                download.EnsureSuccessStatusCode();
                using var fileStream = new FileStream(downloadFile.FilePath, FileMode.Create);
                await download.Content.CopyToAsync(fileStream);
                return true;
            }
            catch (Exception e){
                if (i == RetryCount) {
                    if (downloadFile.UrlPaths != null && downloadFile.UrlPaths.Count != 0 && downloadFile.UrlPaths[^1].Equals(downloadFile.UrlPath)) {
                        downloadFile.UrlPath = downloadFile.UrlPaths[downloadFile.UrlPaths.IndexOf(downloadFile.UrlPath)];
                        continue;
                    }
                    downloadFile.ErrorMessage = e.Message;
                    return false;
                }
            }
        }
        return false;
    }
    
    public class ThreadDownloader {
        private HttpClient httpClient = new ();
        public bool isRunning { get; set; } = false;

        public ThreadDownloader() {
        }
        public async Task DownloadFileFunc(DownloadFile item, CancellationToken ct) {
            if (IsCancel) {
                return;
            }
            try {
                ct.ThrowIfCancellationRequested();
                item.State = DownloadFile.StateType.Downloading;
                DownloadPage.ProgressUpdate?.Invoke(item,finishCount,errorDownloadFiles.Count);
                // Console.WriteLine($"当前进度    还剩：{TotalCount-FinishCount} | 成功：{FinishCount} | 失败：{errorDownloadFiles.Count}");
                if (!DirFileUtil.IsValidFilePath(item.FilePath)) {
                    throw new Exception("路径不合法");
                }
                var dir = Path.GetDirectoryName(item.FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                    Directory.CreateDirectory(dir);
                }
                if (string.IsNullOrEmpty(item.UrlPath)) {
                    throw new Exception("下载地址为空");
                }
                ct.ThrowIfCancellationRequested();
                // if (item.Size == 0) {
                //     using var getSizeReq = new HttpRequestMessage(HttpMethod.Head, item.UrlPath);
                //     using var getSize = await httpClient.SendAsync(getSizeReq, ct);
                //     getSize.EnsureSuccessStatusCode();
                //     long size = getSize.Content.Headers.ContentLength.Value / 1024;
                //     // Console.WriteLine(size);
                // }
                // 添加User-Agent
                using var req = new HttpRequestMessage(HttpMethod.Get, item.UrlPath);
                req.Headers.Add("User-Agent", PropertiesUtil.UserAgent);
                using var download =
                    await httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
                
                ct.ThrowIfCancellationRequested();
                download.EnsureSuccessStatusCode();
                ct.ThrowIfCancellationRequested();
                using var fileStream = new FileStream(item.FilePath, FileMode.Create);
                await download.Content.CopyToAsync(fileStream);
                Interlocked.Increment(ref finishCount);
                // Console.WriteLine("下载完成 ：" + item.FilePath);
                item.State = DownloadFile.StateType.Finished;
                DownloadPage.ProgressUpdate?.Invoke(item, finishCount, errorDownloadFiles.Count);
            }
            catch (OperationCanceledException) {
                // Console.WriteLine($"下载取消：{item.UrlPath}");
                // 下载取消，删除未完成的文件【以免文件不完整】
                if (File.Exists(item.FilePath)) {
                    File.Delete(item.FilePath);
                }
                item.State = DownloadFile.StateType.Waiting;
                waitDownloadFiles.Enqueue(item);
                DownloadPage.ProgressUpdate?.Invoke(item, finishCount, errorDownloadFiles.Count);
            }
            catch (Exception e) {
                // Console.WriteLine("下载失败 ：" + item.UrlPath);
                item.ErrorMessage = e.Message;
                if (e.Message == "路径不合法" || e.Message == "下载地址为空") {
                    item.State = DownloadFile.StateType.Error;
                    DownloadPage.ProgressUpdate?.Invoke(item, finishCount, errorDownloadFiles.Count);
                    errorDownloadFiles.Add(item);
                }
                else {
                    if (item.RetryCount > RetryCount) {
                        item.RetryCount++;
                        item.State = DownloadFile.StateType.Waiting;
                        DownloadPage.ProgressUpdate?.Invoke(item, finishCount, errorDownloadFiles.Count);
                        waitDownloadFiles.Enqueue(item);
                    }
                    else {
                        if (item.UrlPaths.Count == 0 || item.UrlPaths[^1].Equals(item.UrlPath)) {
                            item.State = DownloadFile.StateType.Error;
                            DownloadPage.ProgressUpdate?.Invoke(item, finishCount, errorDownloadFiles.Count);
                            errorDownloadFiles.Add(item);
                        }
                        else {
                            item.UrlPath = item.UrlPaths[item.UrlPaths.IndexOf(item.UrlPath)+1];
                            item.RetryCount = 1;
                            item.State = DownloadFile.StateType.Waiting;
                            DownloadPage.ProgressUpdate?.Invoke(item, finishCount, errorDownloadFiles.Count);
                            waitDownloadFiles.Enqueue(item);
                        }
                    }
                }
            }
            isRunning = false;
            if (FinishCount + errorDownloadFiles.Count == TotalCount) {
                MessageTips.Show("下载任务完成");
                Home.DownloadState?.Invoke(false);
                DownloadPage.DownloadingAnimState?.Invoke(false);
                downloadCompletionSource?.TrySetResult(true);
                Console.WriteLine($"下载任务完成    总共：{TotalCount} | 完成：{FinishCount} | 失败：{errorDownloadFiles.Count}");
                return;
            }
            if (!IsCancel && waitDownloadFiles.Count > 0) {
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
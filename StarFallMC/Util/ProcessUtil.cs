using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace StarFallMC.Util;

public static class ProcessUtil
{
    private static readonly ProcessRunner Runner = new(outputCapacity: 1500, maxLineLength: 16 * 1024);
    // ActiveRuns owns live processes only and removes each entry when its completion settles.
    private static readonly ConcurrentDictionary<int, ProcessRun> ActiveRuns = new();

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetWindowText(IntPtr hWnd, string lpString);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>
    /// Runs a command and returns a bounded output snapshot. The process and
    /// all output readers are disposed before this method completes.
    /// </summary>
    public static async Task<ProcessResult> RunCmdCaptureAsync(
        string cmd,
        string arg,
        CancellationToken cancellationToken = default)
    {
        var processInfo = CreateStartInfo(cmd, arg);
        Console.WriteLine($"运行命令: {cmd}");
        await using var run = Runner.Start(processInfo, cancellationToken);
        return await run.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    }

    public static Task<ProcessResult> GetCompletion(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
        return ActiveRuns.TryGetValue(process.Id, out var run)
            ? run.Completion
            : process.WaitForExitAsync().ContinueWith(
                _ => new ProcessResult(process.ExitCode, Array.Empty<string>()),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }

    /// <summary>Releases a process that has already exited without killing it.</summary>
    public static void ReleaseProcess(Process? process)
    {
        if (process == null) {
            return;
        }
        int processId;
        try {
            processId = process.Id;
        }
        catch (InvalidOperationException) {
            return;
        }
        if (ActiveRuns.TryRemove(processId, out var run)) {
            run.Dispose();
        }
        else {
            process.Dispose();
        }
    }

    // 运行Minecraft，使用Java命令和参数
    public static Process RunMinecraft(string java, string arg)
    {
        var run = Runner.Start(CreateStartInfo(java, arg));
        ActiveRuns[run.Process.Id] = run;
        return run.Process;
    }

    // 停止并等待由启动器拥有的进程。
    public static async Task StopProcessAsync(Process? process)
    {
        if (process == null)
        {
            return;
        }

        int processId;
        try {
            processId = process.Id;
        }
        catch (InvalidOperationException) {
            return;
        }
        if (ActiveRuns.TryRemove(processId, out var run))
        {
            await run.DisposeAsync().ConfigureAwait(false);
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch (InvalidOperationException)
        {
        }

        process.Dispose();
    }

    // 获取Java进程的ID
    public static int GetChildrenJavaProcessIds(int processId)
    {
        string query = $"SELECT ProcessId, Name FROM Win32_Process WHERE ParentProcessId = {processId}";
        using var searcher = new ManagementObjectSearcher(query);
        foreach (ManagementObject item in searcher.Get())
        {
            try
            {
                if (item["Name"]?.ToString()?.Contains("java", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return Convert.ToInt32(item["ProcessId"]);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }

        return -1;
    }

    // 检查是否有Java进程的窗口
    public static bool HasProcessWindow(int processId)
    {
        if (processId == -1)
        {
            return false;
        }

        using Process javaProcess = Process.GetProcessById(processId);
        bool isFound = false;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
            {
                return true;
            }

            GetWindowThreadProcessId(hWnd, out int windowProcessId);
            if (javaProcess.Id == windowProcessId)
            {
                isFound = true;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return isFound;
    }

    // 设置窗口标题
    public static void SetWindowTitle(int processId, string title)
    {
        using Process process = Process.GetProcessById(processId);
        IntPtr hWnd = process.MainWindowHandle;
        if (hWnd != IntPtr.Zero && !string.IsNullOrEmpty(title))
        {
            SetWindowText(hWnd, title);
        }
    }

    private static ProcessStartInfo CreateStartInfo(string cmd, string arg) => new(cmd)
    {
        Arguments = arg,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
}

using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text; //Nuget安装 System.Management

namespace StarFallMC.Util;

public class ProcessUtil {
    
    // 辅助函数，用于枚举所有窗口
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    
    // 获取窗口的线程和进程ID
    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);
    
    // 检查窗口是否可见
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    
    // 设置窗口标题
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetWindowText(IntPtr hWnd, string lpString);
    
    // 委托类型，用于枚举窗口的回调函数
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    
    public static StringBuilder lastOutput;
    
    // 运行命令行命令，分为两个部分，命令和参数，命令如：java，参数如：-jar minecraft.jar
    public static async Task<Process> RunCmd(string cmd,string arg, bool showOutput = true, bool waitExit = true) {
        lastOutput = new StringBuilder();
        var processInfo = new ProcessStartInfo(cmd) {
            Arguments = arg,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        var process = new Process();
        process.StartInfo = processInfo;
        process.EnableRaisingEvents = true;
        process.OutputDataReceived += (s, e) => {
            if (!string.IsNullOrEmpty(e.Data)) {
                string output = $"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}-输出] {e.Data}";
                Console.WriteLine(output);
                lastOutput.AppendLine($"\n{e.Data}");
            }
        };
        process.ErrorDataReceived += (s, e) => {
            if (!string.IsNullOrEmpty(e.Data)) {
                string output = $"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}-错误] {e.Data}";
                Console.WriteLine(output);
                lastOutput.AppendLine($"\n{e.Data}");
            }
        };
        process.Start();
        if (showOutput) {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        if (waitExit) {
            await process.WaitForExitAsync();
        }
        return process;
    }

    // 运行Minecraft，使用Java命令和参数
    public static Process RunMinecraft(string java,string arg) {
        return RunCmd(java,arg,true,false).Result;
    }

    // 停止进程，使用Kill方法
    public static void StopProcess(Process process) {
        process?.Kill(entireProcessTree: true);
        process?.Dispose();
    }

    // 获取Java进程的ID
    public static int GetChildrenJavaProcessIds(int processId) {
        string query = $"SELECT ProcessId, Name FROM Win32_Process WHERE ParentProcessId = {processId}";
        using (var searcher = new ManagementObjectSearcher(query)) {
            foreach (ManagementObject i in searcher.Get()) {
                try {
                    if (i["Name"].ToString().Contains("java")) {
                        return Convert.ToInt32(i["ProcessId"]);
                    }
                }
                catch (Exception e){
                    Console.WriteLine(e);
                }
            }
        }
        return -1;
    }

    // 检查是否有Java进程的窗口
    public static bool HasProcessWindow(int processId) {
        if (processId == -1) {
            return false;
        }
        Process javaProcess;
        try {
            javaProcess = Process.GetProcessById(processId);
        }
        catch (Exception e) {
            return false; // 进程不存在
        }
        bool isFound = false;
        EnumWindows((hWnd, lParam) => {
            if (!IsWindowVisible(hWnd)) {
                return true;
            }
            GetWindowThreadProcessId(hWnd, out int windowProcessId);
            if (javaProcess.Id == windowProcessId) {
                isFound = true;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        javaProcess.Dispose();
        return isFound;
    }

    // 设置窗口标题
    public static void SetWindowTitle(int processId,string title) {
        Process process = Process.GetProcessById(processId);
        IntPtr hWnd = process.MainWindowHandle;
        if (hWnd != IntPtr.Zero && !string.IsNullOrEmpty(title)) {
            SetWindowText(hWnd, title);
        }
    }
    
}
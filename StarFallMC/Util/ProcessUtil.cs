using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices; //Nuget安装 System.Management

namespace StarFallMC.Util;

public class ProcessUtil {
    
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    
    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);
    
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetWindowText(IntPtr hWnd, string lpString);
    
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public static Process RunMinecraft(string java,string cmd) {
        var processInfo = new ProcessStartInfo(java) {
            Arguments = cmd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        var process = new Process();
        process.StartInfo = processInfo;
        process.OutputDataReceived += (s, e) => {
            if (!string.IsNullOrEmpty(e.Data)) {
                Console.WriteLine("[输出] " + e.Data);
            }
                
        };
        process.ErrorDataReceived += (s, e) => {
            if (!string.IsNullOrEmpty(e.Data)) {
                Console.WriteLine("[错误] " + e.Data);
            }
                
        };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    public static void StopProcess(Process process) {
        process.Kill(entireProcessTree: true);
        process.Dispose();
    }

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

    public static void SetWindowTitle(int processId,string title) {
        Process process = Process.GetProcessById(processId);
        IntPtr hWnd = process.MainWindowHandle;
        if (hWnd != IntPtr.Zero && !string.IsNullOrEmpty(title)) {
            SetWindowText(hWnd, title);
        }
    }
    
}
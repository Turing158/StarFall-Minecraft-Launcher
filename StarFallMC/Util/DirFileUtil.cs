using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace StarFallMC.Util;

public class DirFileUtil {
    
    public static string CurrentDirPosition = System.AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
    
    
    
    
    //通过资源管理器打开文件夹
    public static bool openDirByExplorer(string path) {
        if (!Directory.Exists(path)) {
            //提示
            return false;
        }
        try {
            Process.Start(new ProcessStartInfo{
                Verb = "open",
                UseShellExecute = true,
                FileName = path,
            });
            return true;
        } catch (Exception e) {
            Console.WriteLine(e);
            return false;
        }
    }

    public static string GetParentPath(string path) {
        string[] paths = Path.GetFullPath(path).Split("\\");
        if (paths.Length == 0 || paths.Length == 2) {
            return path;
        }
        var newPaths = paths.SkipLast(1).ToList();
        StringBuilder newPath = new StringBuilder();
        for (int i = 0 ; i < newPaths.Count ; i++) {
            newPath.Append(newPaths[i]);
            if (i != newPaths.Count -1) {
                newPath.Append("/");
            }
        }
        return Path.GetFullPath(newPath.ToString());
    }
    
    public static string GetParentDirName(string path) {
        string[] paths = Path.GetFullPath(path).Split("\\");
        if (paths.Length == 0 || paths.Length == 2) {
            return path;
        }
        var newPaths = paths.SkipLast(1).ToList();
        return newPaths.Last();
    }
}
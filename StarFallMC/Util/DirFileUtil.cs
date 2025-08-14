using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;

namespace StarFallMC.Util;

public class DirFileUtil {
    
    public static string CurrentDirPosition = System.AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
    public static string LauncherSettingsDir = Path.Combine(CurrentDirPosition, "SFMCL");
    
    
    
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

    public static void DeleteDirAllContent(string path) {
        if (Directory.Exists(path)) {
            Directory.Delete(path,recursive:true);
        }
    }
    
    public static void CompressZip(string path,string orderPath) {
        if (!Directory.Exists(orderPath)) {
            Directory.CreateDirectory(orderPath);
        }
        ZipFile.ExtractToDirectory(path,orderPath,true);
    }

    public static bool GetZipFileToOrder(string zipPath, string orderFilePathInZip, string orderPath) {
        using FileStream zipStream = new FileStream(zipPath, FileMode.Open);
        using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read)) {
            ZipArchiveEntry entry = archive.GetEntry(orderFilePathInZip);
            if (entry == null) {
                Console.WriteLine($"未找到压缩包中的文件：{orderFilePathInZip}");
                return false;
            }
            using (Stream entryStream = entry.Open()) {
                using FileStream fileStream = new FileStream(orderPath, FileMode.Create, FileAccess.Write);
                entryStream.CopyTo(fileStream);
            }
        }
        return true;
    }
    
    public static bool IsValidFileName(string fileName) {
        return !string.IsNullOrEmpty(fileName) &&
               fileName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
    }
    
    public static bool IsValidFilePath(string filePath) {
        return !string.IsNullOrEmpty(filePath) &&
               (filePath.Split(":\\").Length == 2 || filePath.Split(":/").Length == 2) &&
               IsValidFileName(Path.GetFileNameWithoutExtension(filePath));

    }
}
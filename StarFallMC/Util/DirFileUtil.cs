using System.Diagnostics;
using System.IO;
using System.IO.Compression;

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
        if (string.IsNullOrEmpty(path)) {
            return path; 
        }
        try {
            string result = Path.GetDirectoryName(Path.GetFullPath(path));
            return result ?? path;
        }
        catch (Exception e){
            Console.WriteLine(e);
            return path; 
        }
    }
    
    public static string GetParentDirName(string path) {
        if (string.IsNullOrEmpty(path)) {
            return path;
        }
        try {
            string result = Path.GetDirectoryName(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(result)) {
                return path;
            }
            return Path.GetFileName(result);
        }
        catch (Exception e){
            Console.WriteLine(e);
            return path;
        }
    }

    public static string GetAbsolutePathInLauncherSettingDir(string relativePath) {
        string path = LauncherSettingsDir;
        var sourceSplit =  relativePath.Split("../");
        for (int i = 0; i < sourceSplit.Length -1; i++) {
            string oldPath = path;
            path = GetParentPath(path);
            if (path == oldPath) {
                break;
            }
        }
        return Path.GetFullPath(Path.Combine(path, sourceSplit[^1]));
    }

    public static void DeleteDirAllContent(string path) {
        if (Directory.Exists(path)) {
            Directory.Delete(path,recursive:true);
        }
    }
    
    public static void CompressZip(string path,string orderPath,bool overwrite = false) {
        if (!Directory.Exists(orderPath)) {
            Directory.CreateDirectory(orderPath);
        }
        if (overwrite) {
            ZipFile.ExtractToDirectory(path,orderPath,true);
        }
        else {
            using (ZipArchive archive = ZipFile.Open(path,ZipArchiveMode.Read)) {
                foreach (var entry in archive.Entries) {
                    string fileName = Path.GetFullPath(Path.Combine(orderPath, entry.FullName));
                    if (Path.GetFileName(fileName).Length == 0) {
                        continue;
                    }

                    if (!File.Exists(fileName)) {
                        string dirPath = Path.GetDirectoryName(fileName);
                        if (!Directory.Exists(dirPath)) {
                            Directory.CreateDirectory(dirPath);
                        }
                        entry.ExtractToFile(fileName);
                    }
                }
            }
        }
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
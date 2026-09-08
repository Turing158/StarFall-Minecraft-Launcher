using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Navigation;
using Newtonsoft.Json.Linq;
using StarFallMC.Entity;

namespace StarFallMC.Util;

public class NetworkUtil {
    public static readonly CookieContainer _cookieContainer = new CookieContainer();
    
    public static void OpenUrl(string url) {
        Process.Start(new ProcessStartInfo {
            FileName = url,
            UseShellExecute = true,
        });
    }
    
    public static bool IsValidUrl(string urlString) {
        if (string.IsNullOrWhiteSpace(urlString)) {
            return false;
        }
        return Uri.TryCreate(urlString, UriKind.Absolute, out Uri uriResult) 
               && (uriResult.Scheme == Uri.UriSchemeHttp 
                   || uriResult.Scheme == Uri.UriSchemeHttps
                   || uriResult.Scheme == Uri.UriSchemeFtp);
    }

    public static List<T> GetPageList<T>(List<T> downloaders,int page, int pageSize) {
        if (downloaders == null || downloaders.Count == 0) {
            return new List<T>();
        }
        return downloaders.Skip((page - 1) * pageSize).Take(pageSize).ToList();
    }
    
    public static List<string> SortVersions(List<string> sortedVersions) {
        return sortedVersions.Select(v => new { Original = v, Version = ParseVersion(v) })
            .OrderBy(x => x.Version)
            .Select(x => x.Original)
            .ToList();
    }
        
    private static Version ParseVersion(string versionString){
        var parts = versionString.Split('.');
        if (parts.Length == 1)
            return new Version(int.Parse(parts[0]), 0, 0, 0);
        else if (parts.Length == 2)
            return new Version(int.Parse(parts[0]), int.Parse(parts[1]), 0, 0);
        else
            return new Version(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), 0);
    }

    public static async Task<UpdateInfo> GetUpdateInfo() {
        var result = await HttpRequestUtil.Get(KeyUtil.UPDATE_INFO_URL);
        if (result.IsSuccess) {
            var root = JObject.Parse(result.Content);
            var data = root["data"][0];
            var updateInfo = new UpdateInfo() {
                Version = data["version"].ToString(),
                Contents = data["contents"].ToString(),
                Title = data["title"].ToString(),
                UpdateDate = data["create_date"].ToString(),
                UpdateUrl = data["url"].ToString(),
            };
            return updateInfo;
        }
        Console.WriteLine("获取更新信息失败");
        return null;
    }
    
    public static bool IsValidVersion(string input){
        if (string.IsNullOrEmpty(input))
            return false;
        Regex regex = new Regex(@"\d+\.");
        return regex.IsMatch(input);
    }

    
    public static string GetNewerVersion(string version1, string version2) {
        if (string.IsNullOrEmpty(version1)) return version2;
        if (string.IsNullOrEmpty(version2)) return version1;
        try {
            var v1 = new Version(version1);
            var v2 = new Version(version2);
        
            return v1 > v2 ? version1 : version2;
        }
        catch {
            return CompareVersionStrings(version1, version2);
        }
    }

    private static string CompareVersionStrings(string v1, string v2) {
        string[] parts1 = v1.Split('.');
        string[] parts2 = v2.Split('.');
        int maxLength = Math.Max(parts1.Length, parts2.Length);
        for (int i = 0; i < maxLength; i++)
        {
            int num1 = i < parts1.Length ? GetNumberPart(parts1[i]) : 0;
            int num2 = i < parts2.Length ? GetNumberPart(parts2[i]) : 0;
        
            if (num1 > num2) return v1;
            if (num1 < num2) return v2;
        }
        return v1;
    }

    private static int GetNumberPart(string part) {
        if (int.TryParse(System.Text.RegularExpressions.Regex.Match(part, @"^\d+").Value, out int result))
            return result;
        return 0;
    }
    
    public static async Task<string> GetUrlCookie(string uri) {
        if (!IsValidUrl(uri)) {
            return string.Empty;
        }
        var uriObj = new Uri(uri);
        var cookieHeader = _cookieContainer.GetCookieHeader(uriObj);
        if (string.IsNullOrEmpty(cookieHeader)) {
            try {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{uriObj.Scheme}://{uriObj.Host}");
                using var client = new HttpClient();
                var response = await client.SendAsync(req);
                var newCookieHeader = response.Headers.GetValues("Set-Cookie").FirstOrDefault();
                if (!string.IsNullOrEmpty(newCookieHeader)) {
                    _cookieContainer.SetCookies(uriObj, newCookieHeader);
                    foreach (var i in _cookieContainer.GetCookies(uriObj)) {
                        return i.ToString();
                    }
                }
                return String.Empty;
            }
            catch (Exception e) {
                Console.WriteLine($"获取cookie失败：{e.Message}");
                return string.Empty;
            }
        }
        Console.WriteLine("cookie存在，返回");
        foreach (var i in _cookieContainer.GetCookies(uriObj)) {
            return i.ToString();
        }

        return string.Empty;
    }

    public static void ExportErrorFile(IReadOnlyCollection<DownloadFile>? errorFiles = null) {
        StringBuilder sb = new StringBuilder();
        foreach (var file in errorFiles ?? Array.Empty<DownloadFile>()) {
            sb.Append($"文件名:{Path.GetFileName(file.FilePath)}\n");
            sb.Append($"文件位置:{file.FilePath}\n");
            sb.Append($"下载链接:{file.UrlPath}\n");
            List<string> outUrlPathList = new List<string>();
            if (file.UrlPaths != null && file.UrlPaths.Count > 0) {
                foreach (var i in file.UrlPaths) {
                    if (!i.Equals(file.UrlPath)) {
                        outUrlPathList.Add(i);
                    }
                }
            }
            sb.Append($"备用链接:\n{string.Join("\n", outUrlPathList)}\n");
            sb.Append($"错误信息:{file.ErrorMessage}\n");
            sb.Append($"[如果有需要，请复制\"{file.UrlPath}\"到浏览器或下载器下载，并移动到\"{Path.GetDirectoryName(file.FilePath)}\"文件夹中，若下载链接无法下载，请复制备用链接下载！]\n");
            sb.Append("\n");
        }

        if (!Directory.Exists(DirFileUtil.LauncherSettingsDir)) {
            Directory.CreateDirectory(DirFileUtil.LauncherSettingsDir);
        }

        string filePath = Path.Combine(DirFileUtil.LauncherSettingsDir, "ErrorDownloadFiles.txt");
        File.WriteAllText(filePath, sb.ToString());
        DirFileUtil.OpenContainingFolder(filePath);
    }
    
    public async static Task<string> GetNeedJavaScriptRedirectUrl(string url,string redirectKeyword,int timeout = 5000) {
        using var brower = new WebBrowser();
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        NavigatingCancelEventHandler? navigatingHandler = null;
        try {
            navigatingHandler = (sender, args) => {
                string uri = args.Uri.ToString();
                if (uri.ToLower().Contains(redirectKeyword.ToLower())) {
                    args.Cancel = true;
                    tcs.TrySetResult(uri);
                }
            };
            brower.Navigating += navigatingHandler;
            brower.Navigate(url);
            var timeoutTask = Task.Delay(timeout);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            if (completedTask == tcs.Task) {
                return await tcs.Task;
            }
        }
        catch (Exception e){
            Console.WriteLine(e);
        }
        finally {
            if (navigatingHandler != null) {
                brower.Navigating -= navigatingHandler;
            }
        }
        return url;
    }
}

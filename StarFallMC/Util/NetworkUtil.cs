using System.Diagnostics;
using Newtonsoft.Json.Linq;
using StarFallMC.Entity;

namespace StarFallMC.Util;

public class NetworkUtil {
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
}
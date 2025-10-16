using System.Diagnostics;

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
        return downloaders.Skip((page - 1) * pageSize).Take(pageSize).ToList();
    }
}